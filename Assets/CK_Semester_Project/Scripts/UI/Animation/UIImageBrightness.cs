using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation
{
    // 정점 색은 1에서 잘려 곱하는 색으로는 밝게 만들 수 없다. 머티리얼 색은 잘리지 않으므로 밝기가 바뀐 동안만 복사본을 쓴다.
    public sealed class UIImageBrightness
    {
        private const string ColorProperty = "_Color";

        private readonly Image _image;
        private Material _copy;
        private Material _original;
        private Color _tint;
        private float _written;
        private bool _applied;

        public UIImageBrightness(Image image)
        {
            _image = image;
        }

        public void Set(float brightness)
        {
            if (Mathf.Approximately(brightness, 1f))
            {
                Clear();
            }
            else if ((_applied || Begin()) && brightness != _written)
            {
                _written = brightness;
                // Mask 안에서는 스텐실용으로 한 번 더 복사되므로 실제로 그려지는 쪽에 쓴다.
                _image.materialForRendering.color = new Color(_tint.r * brightness, _tint.g * brightness, _tint.b * brightness, _tint.a);
            }
        }

        public void Clear()
        {
            if (_applied)
            {
                _applied = false;
                _image.material = _original;
            }
        }

        public void Release()
        {
            Clear();
            if (_copy == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Object.Destroy(_copy);
            }
            else
            {
                Object.DestroyImmediate(_copy);
            }
        }

        private bool Begin()
        {
            Material current = _image.material;
            if (!current.HasProperty(ColorProperty))
            {
                return false;
            }

            _original = current == _image.defaultMaterial ? null : current;
            _tint = current.color;
            if (_copy == null)
            {
                _copy = new Material(current) { hideFlags = HideFlags.HideAndDontSave };
            }
            _image.material = _copy;
            _written = 1f;
            _applied = true;
            return true;
        }
    }
}
