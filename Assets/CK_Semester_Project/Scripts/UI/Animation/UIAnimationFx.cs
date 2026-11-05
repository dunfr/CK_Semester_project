using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation
{
    // 프리셋이 쓸 때만 도구가 붙이고 떼므로 메뉴에 두지 않는다.
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class UIAnimationFx : MonoBehaviour
    {
        [SerializeField, HideInInspector] private float _skew;
        [SerializeField, HideInInspector] private float _wipe = 1f;
        [SerializeField, HideInInspector] private float _slice;
        [SerializeField, HideInInspector] private float _split;
        [SerializeField, HideInInspector] private float _scan;
        [SerializeField, HideInInspector] private float _dissolve;
        [SerializeField, HideInInspector] private float _sheen;
        [SerializeField, HideInInspector] private float _flash;
        [SerializeField, HideInInspector] private float _seed;

        private readonly List<UIAnimationFxMesh> _meshes = new List<UIAnimationFxMesh>();
        private readonly List<UIAnimationFxText> _texts = new List<UIAnimationFxText>();
        private UIAnimationPlayer _player;
        private UIImageBrightness _brightness;
        private UIAnimationFxValues _written = new UIAnimationFxValues { Wipe = 1f };
        private bool _connected;
#if UNITY_EDITOR
        private bool _watchingAnimationMode;
#endif

        public UIAnimationFxValues Values => new UIAnimationFxValues
        {
            Skew = _skew, Wipe = _wipe, Slice = _slice, Split = _split, Scan = _scan,
            Dissolve = _dissolve, Sheen = _sheen, Flash = _flash, Seed = _seed
        };
        public bool IsRest => !isActiveAndEnabled || Values.IsRest;
        public RectTransform Root => (RectTransform)transform;
        public UIAnimationPreset Preset
        {
            get
            {
                if (_player == null) _player = GetComponent<UIAnimationPlayer>();
                return _player != null ? _player.Preset : null;
            }
        }

        public UIAnimationFxShape Shape
        {
            get
            {
                UIAnimationPreset preset = Preset;
                float direction = 0f;
                if (preset != null)
                {
                    switch (preset.WipeDirection)
                    {
                        case UIWipeDirection.RightToLeft: direction = 180f; break;
                        case UIWipeDirection.BottomToTop: direction = 90f; break;
                        case UIWipeDirection.TopToBottom: direction = 270f; break;
                    }
                }
                return new UIAnimationFxShape
                {
                    Bounds = Root.rect,
                    WipeAxis = UIAnimationFxGeometry.Axis(direction + (preset != null ? preset.WipeAngle : 0f)),
                    WipeSoftness = preset != null ? preset.WipeSoftness : 0f,
                    Bands = preset != null ? preset.GlitchBands : 12,
                    CellSize = preset != null ? preset.DissolveCellSize : 16f,
                    SheenAxis = UIAnimationFxGeometry.Axis(preset != null ? preset.SheenAngle : 25f),
                    SheenWidth = preset != null ? preset.SheenWidth : 0.2f,
                    SheenIntensity = preset != null ? preset.SheenIntensity : 1f
                };
            }
        }

        private void OnEnable()
        {
            UIAnimationFx parent = ParentFx();
            if (parent != null) parent.RefreshConnections();
            ApplyChangedValues();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= WatchAnimationMode;
            _watchingAnimationMode = false;
#endif
            ResetVisuals();
        }

        private void OnDestroy()
        {
            Disconnect();
            _brightness?.SetFx(1f);
            _brightness?.Release();
            _brightness = null;
        }

        private UIAnimationFx ParentFx()
        {
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                if (parent.TryGetComponent(out UIAnimationFx fx)) return fx;
            return null;
        }

        private void OnTransformChildrenChanged() => RefreshConnections();
        private void OnRectTransformDimensionsChange()
        {
            if (_connected) RefreshShape();
        }
        private void OnDidApplyAnimationProperties() => ApplyChangedValues();

        public void ApplyChangedValues()
        {
            if (!isActiveAndEnabled) return;
#if UNITY_EDITOR
            if (!Application.isPlaying && !_watchingAnimationMode && UnityEditor.AnimationMode.InAnimationMode())
            {
                _watchingAnimationMode = true;
                UnityEditor.EditorApplication.update += WatchAnimationMode;
            }
#endif
            UIAnimationFxValues values = Values;
            bool geometryChanged = !values.SameGeometry(_written);
            bool textChanged = !values.SameText(_written);
            bool childGeometryChanged = textChanged || values.Split != _written.Split;
            if (!values.IsRest && (!_connected || _written.IsRest)) ConnectGraphics(transform);
            if (geometryChanged)
                foreach (UIAnimationFxMesh mesh in _meshes)
                    if (mesh != null && (mesh.IsTargetImage || childGeometryChanged)) mesh.Dirty();
            if (textChanged)
                foreach (UIAnimationFxText text in _texts) if (text != null) text.Dirty();
            if (values.Flash != _written.Flash || values.Sheen != _written.Sheen) ApplyBrightness();
            _written = values;
        }

        public void RefreshShape()
        {
            if (!_connected && !IsRest) ConnectGraphics(transform);
            foreach (UIAnimationFxMesh mesh in _meshes) if (mesh != null) mesh.Dirty();
            foreach (UIAnimationFxText text in _texts) if (text != null) text.Dirty();
            ApplyBrightness();
        }

        private void ApplyBrightness()
        {
            float value = isActiveAndEnabled ? UIAnimationFxGeometry.Brightness(Values, Shape) : 1f;
            if (_brightness == null && value != 1f && TryGetComponent(out Image image))
                _brightness = UIImageBrightness.Acquire(image);
            _brightness?.SetFx(value);
        }

        private void RefreshConnections()
        {
            if (!_connected) return;
            for (int i = _meshes.Count - 1; i >= 0; i--)
            {
                UIAnimationFxMesh link = _meshes[i];
                UIAnimationFx nearest = link != null ? NearestFx(link.transform) : null;
                if (nearest == this) continue;
                if (link != null && link.Owner == this)
                {
                    link.Bind(nearest);
                    if (nearest != null)
                    {
                        if (!nearest._meshes.Contains(link)) nearest._meshes.Add(link);
                        nearest._connected = true;
                    }
                    else RemoveLink(link);
                }
                _meshes.RemoveAt(i);
            }
            for (int i = _texts.Count - 1; i >= 0; i--)
            {
                UIAnimationFxText link = _texts[i];
                UIAnimationFx nearest = link != null ? NearestFx(link.transform) : null;
                if (nearest == this) continue;
                if (link != null && link.Owner == this)
                {
                    link.Bind(nearest, nearest != null ? link.GetComponent<TMP_Text>() : null);
                    if (nearest != null)
                    {
                        if (!nearest._texts.Contains(link)) nearest._texts.Add(link);
                        nearest._connected = true;
                    }
                    else RemoveLink(link);
                }
                _texts.RemoveAt(i);
            }
            ConnectGraphics(transform);
        }

        private static UIAnimationFx NearestFx(Transform node)
        {
            for (; node != null; node = node.parent)
                if (node.TryGetComponent(out UIAnimationFx fx)) return fx;
            return null;
        }

        private void ConnectGraphics(Transform node)
        {
            _connected = true;
            if (node != transform && node.TryGetComponent(out UIAnimationFx _)) return;
            if (node.TryGetComponent(out TMP_Text tmp))
            {
                UIAnimationFxText link = node.GetComponent<UIAnimationFxText>();
                if (link == null) link = node.gameObject.AddComponent<UIAnimationFxText>();
                link.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
                link.Bind(this, tmp);
                if (!_texts.Contains(link)) _texts.Add(link);
            }
            else if (node.TryGetComponent(out Graphic graphic)
                && (graphic is Image || graphic is RawImage || graphic is Text))
            {
                UIAnimationFxMesh link = node.GetComponent<UIAnimationFxMesh>();
                if (link == null) link = node.gameObject.AddComponent<UIAnimationFxMesh>();
                link.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
                link.Bind(this);
                if (!_meshes.Contains(link)) _meshes.Add(link);
            }
            for (int i = 0; i < node.childCount; i++) ConnectGraphics(node.GetChild(i));
        }

        private void Disconnect()
        {
            foreach (UIAnimationFxMesh mesh in _meshes)
                if (mesh != null && mesh.Owner == this) { mesh.Bind(null); RemoveLink(mesh, true); }
            foreach (UIAnimationFxText text in _texts)
                if (text != null && text.Owner == this) { text.Bind(null, null); RemoveLink(text, true); }
            _meshes.Clear(); _texts.Clear(); _connected = false;
        }

        private static void RemoveLink(Object link, bool defer = false)
        {
            if (Application.isPlaying) Destroy(link);
#if UNITY_EDITOR
            // Inspector 갱신에 기대지 않고, 루트 파괴가 끝난 다음 편집기 업데이트에서 정리한다.
            else if (defer)
            {
                void Cleanup()
                {
                    UnityEditor.EditorApplication.update -= Cleanup;
                    if (link == null) return;
                    if (link is UIAnimationFxMesh mesh && mesh.Owner != null) return;
                    if (link is UIAnimationFxText text && text.Owner != null) return;
                    DestroyImmediate(link);
                }
                UnityEditor.EditorApplication.update += Cleanup;
            }
#endif
            else DestroyImmediate(link);
        }

        // AnimationMode가 필드를 복원해도 Canvas의 이전 메쉬는 남을 수 있어 함께 무효화한다.
        public void ResetVisuals()
        {
            _skew = _slice = _split = _scan = _dissolve = _sheen = _flash = _seed = 0f;
            _wipe = 1f;
            _written = Values;
            foreach (UIAnimationFxMesh mesh in _meshes) if (mesh != null) mesh.Dirty();
            foreach (UIAnimationFxText text in _texts) if (text != null) text.Dirty();
            _brightness?.SetFx(1f);
        }

#if UNITY_EDITOR
        private void WatchAnimationMode()
        {
            if (UnityEditor.AnimationMode.InAnimationMode()) return;
            UnityEditor.EditorApplication.update -= WatchAnimationMode;
            _watchingAnimationMode = false;
            ResetVisuals();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
#endif
    }
}
