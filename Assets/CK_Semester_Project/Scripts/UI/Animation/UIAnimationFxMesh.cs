using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation
{
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class UIAnimationFxMesh : BaseMeshEffect
    {
        private readonly List<UIVertex> _stream = new List<UIVertex>(128);
        private readonly UIAnimationFxGeometry.Workspace _workspace = new UIAnimationFxGeometry.Workspace();
        public UIAnimationFx Owner { get; private set; }
        public bool IsTargetImage => Owner != null && transform == Owner.transform && graphic is Image;

        public void Bind(UIAnimationFx owner)
        {
            if (Owner == owner) return;
            Owner = owner;
            Dirty();
        }
        public void Dirty() { if (graphic != null) graphic.SetVerticesDirty(); }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Owner == null || Owner.IsRest || vh.currentVertCount == 0) return;
            UIAnimationFxValues values = Owner.Values;
            if (!IsTargetImage) { values.Sheen = 0f; values.Flash = 0f; }
            if (values.IsRest) return;
            _stream.Clear();
            vh.GetUIVertexStream(_stream);
            if (_stream.Count > 64000) return;
            Matrix4x4 toRoot = Owner.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            Matrix4x4 fromRoot = transform.worldToLocalMatrix * Owner.transform.localToWorldMatrix;
            TransformVertices(toRoot);
            UIAnimationFxGeometry.Apply(_stream, values, Owner.Shape, IsTargetImage, _workspace);
            if (_stream.Count > 64000) return;
            TransformVertices(fromRoot);
            vh.Clear();
            vh.AddUIVertexTriangleStream(_stream);
        }

        private void TransformVertices(Matrix4x4 matrix)
        {
            for (int i = 0; i < _stream.Count; i++)
            {
                UIVertex vertex = _stream[i];
                vertex.position = matrix.MultiplyPoint3x4(vertex.position);
                _stream[i] = vertex;
            }
        }
    }
}
