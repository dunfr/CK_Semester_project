using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutlawIllustration
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Canvas))]
    public sealed class OutlawRigPlayer : MonoBehaviour
    {
        [Serializable]
        public sealed class TextureBinding
        {
            public string layerName;
            public Texture2D texture;
        }

        public TextAsset rigJson;
        public List<TextureBinding> textures = new List<TextureBinding>();
        [Range(0, 2)] public float intensity = 1f;
        [Range(0, 3)] public float speed = 1f;
        public bool playAutomatically = true;
        public bool useUnscaledTime = true;
        public bool showBackground = true;
        public bool enableBlink = true;
        [Range(2, 64)] public int meshColumns = 40;
        [Range(2, 64)] public int meshRows = 28;
        [HideInInspector] public float previewTime;
        [SerializeField, HideInInspector] private List<GameObject> generatedLayers = new List<GameObject>();
        private readonly List<OutlawMeshGraphic> uiLayers = new List<OutlawMeshGraphic>();
        private OutlawRigData rig;
        private float elapsed;
        private bool playing;
        private bool hasGeometrySnapshot;
        private float lastGeometryTime;
        private float lastGeometryIntensity;
        private bool lastShowBackground;
        private bool lastEnableBlink;
        private int lastMeshColumns;
        private int lastMeshRows;

        public OutlawRigData Rig { get { return rig; } }
        public float CurrentTime { get { return Application.isPlaying ? elapsed : previewTime; } }
        public bool IsPlaying { get { return playing; } }

        private void Awake()
        {
            playing = playAutomatically;
            if (!ReconnectGeneratedLayers()) Rebuild();
        }

        private void OnEnable()
        {
            if (rig == null && rigJson != null && !ReconnectGeneratedLayers()) Rebuild();
        }

        // 프리팹 인스턴스의 자식은 편집 모드에서 지울 수 없다.
        // 이미 만든 레이어가 리그와 같으면 다시 만들지 않고 연결만 해서 프리팹 안에서도 쓸 수 있게 한다.
        private bool ReconnectGeneratedLayers()
        {
            if (rigJson == null || generatedLayers == null || generatedLayers.Count == 0) return false;
            OutlawRigData data;
            try { data = JsonUtility.FromJson<OutlawRigData>(rigJson.text); }
            catch (Exception) { return false; }
            if (data == null) return false;
            data.Normalize();
            Array.Sort(data.layers, delegate(OutlawLayerData a, OutlawLayerData b) { return a.z.CompareTo(b.z); });
            var graphics = new List<OutlawMeshGraphic>();
            var layers = new List<OutlawLayerData>();
            var layerTextures = new List<Texture2D>();
            for (int i = 0; i < data.layers.Length; i++)
            {
                Texture2D texture = FindTexture(data.layers[i].name);
                if (texture == null) continue;
                if (graphics.Count >= generatedLayers.Count) return false;
                GameObject child = generatedLayers[graphics.Count];
                if (child == null || child.transform.parent != transform || child.name != data.layers[i].name) return false;
                OutlawMeshGraphic graphic = child.GetComponent<OutlawMeshGraphic>();
                if (graphic == null) return false;
                graphics.Add(graphic);
                layers.Add(data.layers[i]);
                layerTextures.Add(texture);
            }
            if (graphics.Count != generatedLayers.Count) return false;
            rig = data;
            uiLayers.Clear();
            for (int i = 0; i < graphics.Count; i++)
            {
                graphics[i].Initialize(this, layers[i], layerTextures[i]);
                uiLayers.Add(graphics[i]);
            }
            RefreshGeometry();
            return true;
        }

        private void Update()
        {
            if (rig == null) return;
            if (Application.isPlaying && playing && speed > 0)
            {
                elapsed += (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * Mathf.Max(0, speed);
                elapsed = Mathf.Repeat(elapsed, rig.duration);
            }
            if (GeometryChanged()) RefreshGeometry();
        }

        private void OnValidate()
        {
            intensity = Mathf.Clamp(intensity, 0, 2);
            speed = Mathf.Clamp(speed, 0, 3);
            meshColumns = Mathf.Clamp(meshColumns, 2, 64);
            meshRows = Mathf.Clamp(meshRows, 2, 64);
            RefreshGeometry();
        }

        public void Play() { playing = true; }
        public void Pause() { playing = false; }
        public void SetTime(float seconds)
        {
            float duration = rig == null ? 8 : rig.duration;
            elapsed = Mathf.Repeat(seconds, duration);
            previewTime = elapsed;
            RefreshGeometry();
        }

        public bool LayerVisible(OutlawLayerData layer)
        {
            if (layer.disabled) return false;
            if (layer.IsBackground) return showBackground;
            if (layer.IsBlink) return enableBlink && OutlawRigMath.BlinkClosed(CurrentTime, rig.duration, rig.blink);
            return true;
        }

        public Texture2D FindTexture(string layerName)
        {
            if (textures == null) return null;
            for (int i = 0; i < textures.Count; i++)
                if (textures[i] != null && textures[i].layerName == layerName) return textures[i].texture;
            return null;
        }

        [ContextMenu("Rebuild Illustration")]
        public void Rebuild()
        {
            if (rigJson == null) return;
            try { rig = JsonUtility.FromJson<OutlawRigData>(rigJson.text); }
            catch (Exception ex) { Debug.LogError("Invalid illustration rig JSON: " + ex.Message, this); return; }
            if (rig == null) return;
            rig.Normalize();
            // A nested Canvas isolates this animated illustration from the
            // surrounding UI batch. Sorting remains inherited so parent masks
            // and the established Canvas/Panel hierarchy continue to apply.
            Canvas localCanvas = GetComponent<Canvas>();
            if (localCanvas == null) localCanvas = gameObject.AddComponent<Canvas>();
            localCanvas.overrideSorting = false;
            ClearGenerated();
            Array.Sort(rig.layers, delegate(OutlawLayerData a, OutlawLayerData b) { return a.z.CompareTo(b.z); });
            for (int i = 0; i < rig.layers.Length; i++)
            {
                OutlawLayerData layer = rig.layers[i];
                Texture2D texture = FindTexture(layer.name);
                if (texture == null)
                {
                    Debug.LogWarning("Missing illustration texture for layer: " + layer.name, this);
                    continue;
                }
                GameObject child = new GameObject(layer.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(OutlawMeshGraphic));
                child.transform.SetParent(transform, false);
                RectTransform rect = (RectTransform)child.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                OutlawMeshGraphic graphic = child.GetComponent<OutlawMeshGraphic>();
                graphic.Initialize(this, layer, texture);
                uiLayers.Add(graphic);
                generatedLayers.Add(child);
            }
            RefreshGeometry();
        }

        public void RefreshGeometry()
        {
            if (rig == null) return;
            for (int i = 0; i < uiLayers.Count; i++)
                if (uiLayers[i] != null) uiLayers[i].Refresh();
            hasGeometrySnapshot = true;
            lastGeometryTime = CurrentTime;
            lastGeometryIntensity = intensity;
            lastShowBackground = showBackground;
            lastEnableBlink = enableBlink;
            lastMeshColumns = meshColumns;
            lastMeshRows = meshRows;
        }

        private bool GeometryChanged()
        {
            return !hasGeometrySnapshot || CurrentTime != lastGeometryTime
                || intensity != lastGeometryIntensity || showBackground != lastShowBackground
                || enableBlink != lastEnableBlink || meshColumns != lastMeshColumns
                || meshRows != lastMeshRows;
        }

        private void ClearGenerated()
        {
            for (int i = generatedLayers.Count - 1; i >= 0; i--)
                if (generatedLayers[i] != null)
                {
                    generatedLayers[i].SetActive(false);
                    if (Application.isPlaying) Destroy(generatedLayers[i]);
                    else DestroyImmediate(generatedLayers[i]);
                }
            generatedLayers.Clear(); uiLayers.Clear();
            hasGeometrySnapshot = false;
        }
    }
}
