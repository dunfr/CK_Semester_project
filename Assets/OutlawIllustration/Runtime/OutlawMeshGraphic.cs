using UnityEngine;
using UnityEngine.UI;

namespace OutlawIllustration
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OutlawMeshGraphic : MaskableGraphic
    {
        [SerializeField, HideInInspector] private OutlawRigPlayer owner;
        [SerializeField, HideInInspector] private OutlawLayerData layer;
        [SerializeField, HideInInspector] private Texture2D layerTexture;

        public override Texture mainTexture
        {
            get { return layerTexture != null ? layerTexture : Texture2D.whiteTexture; }
        }

        public void Initialize(OutlawRigPlayer player, OutlawLayerData data, Texture2D texture)
        {
            owner = player; layer = data; layerTexture = texture;
            raycastTarget = false;
            maskable = true;
            color = Color.white;
            SetAllDirty();
        }

        public void Refresh()
        {
            if (owner == null || owner.Rig == null || layer == null) return;
            bool shouldRender = owner.LayerVisible(layer);
            if (enabled != shouldRender) enabled = shouldRender;
            if (shouldRender && !layer.IsBackground) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (owner == null || owner.Rig == null || layer == null || layerTexture == null) return;
            if (!owner.LayerVisible(layer)) return;
            OutlawRigData rig = owner.Rig;
            float[] source = layer.SourceRect(rig.width, rig.height);
            if (source[2] <= 0 || source[3] <= 0) return;
            int cols = layer.IsBackground ? 1 : Mathf.Clamp(layer.meshCols > 0 ? layer.meshCols : owner.meshColumns, 2, 64);
            int rows = layer.IsBackground ? 1 : Mathf.Clamp(layer.meshRows > 0 ? layer.meshRows : owner.meshRows, 2, 64);
            Rect target = rectTransform.rect;
            Color32 tint = color;
            for (int row = 0; row <= rows; row++)
            {
                float v = (float)row / rows;
                float py = source[1] + source[3] * v;
                for (int col = 0; col <= cols; col++)
                {
                    float u = (float)col / cols;
                    float px = source[0] + source[2] * u;
                    OutlawRigMath.Point point = OutlawRigMath.Evaluate(px, py, owner.CurrentTime, rig, layer, owner.intensity);
                    Vector3 position = new Vector3(target.xMin + point.x / rig.width * target.width,
                                                   target.yMax - point.y / rig.height * target.height, 0);
                    vh.AddVert(position, tint, new Vector2(u, 1 - v));
                }
            }
            int stride = cols + 1;
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    int a = row * stride + col;
                    vh.AddTriangle(a, a + 1, a + stride);
                    vh.AddTriangle(a + 1, a + stride + 1, a + stride);
                }
        }
    }
}
