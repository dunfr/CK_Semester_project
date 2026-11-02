using TMPro;
using UnityEngine;

namespace CK.SemesterProject.UIAnimation
{
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class UIAnimationFxText : MonoBehaviour
    {
        private TMP_Text _text;
        public UIAnimationFx Owner { get; private set; }

        public void Bind(UIAnimationFx owner, TMP_Text text)
        {
            if (Owner == owner && _text == text) return;
            if (_text != null) _text.OnPreRenderText -= Modify;
            Dirty();
            Owner = owner;
            _text = text;
            if (_text != null && isActiveAndEnabled) _text.OnPreRenderText += Modify;
            Dirty();
        }

        public void Dirty()
        {
            if (_text == null) return;
            _text.havePropertiesChanged = true;
            _text.SetVerticesDirty();
        }

        private void OnEnable()
        {
            if (_text != null) { _text.OnPreRenderText -= Modify; _text.OnPreRenderText += Modify; Dirty(); }
        }

        private void OnDisable()
        {
            if (_text != null) { _text.OnPreRenderText -= Modify; Dirty(); }
        }

        private void Modify(TMP_TextInfo info)
        {
            if (Owner == null || Owner.IsRest) return;
            UIAnimationFxValues values = Owner.Values;
            if (values.Skew == 0f && values.Slice == 0f && values.Wipe >= 1f && values.Dissolve == 0f && values.Scan == 0f) return;
            UIAnimationFxShape shape = Owner.Shape;
            Matrix4x4 toRoot = Owner.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            Matrix4x4 fromRoot = transform.worldToLocalMatrix * Owner.transform.localToWorldMatrix;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                TMP_MeshInfo mesh = info.meshInfo[character.materialReferenceIndex];
                int first = character.vertexIndex;
                Vector3 center = toRoot.MultiplyPoint3x4((mesh.vertices[first] + mesh.vertices[first + 2]) * 0.5f);
                float alpha = UIAnimationFxGeometry.TextAlpha(center, values, shape);
                for (int k = 0; k < 4; k++)
                {
                    int index = first + k;
                    Vector3 position = toRoot.MultiplyPoint3x4(mesh.vertices[index]);
                    mesh.vertices[index] = fromRoot.MultiplyPoint3x4(UIAnimationFxGeometry.Move(position, center.y, values, shape));
                    Color32 color = mesh.colors32[index];
                    color.a = (byte)Mathf.RoundToInt(color.a * alpha);
                    mesh.colors32[index] = color;
                }
            }
        }
    }
}
