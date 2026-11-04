using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation
{
    // 정점 색은 1에서 잘려 곱하는 색으로는 밝게 만들 수 없다. 머티리얼 색은 잘리지 않으므로 밝기가 바뀐 동안만 복사본을 쓴다.
    public sealed class UIImageBrightness
    {
        private const string ColorProperty = "_Color";
        // 입력 반응과 FX가 같은 복사본에 각자의 밝기를 곱한다.
        private static readonly Dictionary<Image, UIImageBrightness> Shared = new Dictionary<Image, UIImageBrightness>();

        private readonly Image _image;
        private Material _copy;
        private Material _original;
        private Color _tint;
        private float _written;
        private bool _applied;
        private float _pointer = 1f;
        private float _fx = 1f;
        private int _clients = 1;
        private bool _shared;

        public static UIImageBrightness Acquire(Image image)
        {
            if (Shared.TryGetValue(image, out UIImageBrightness value)) { value._clients++; return value; }
            value = new UIImageBrightness(image) { _shared = true };
            Shared.Add(image, value);
            return value;
        }

        public UIImageBrightness(Image image)
        {
            _image = image;
        }

        public void Set(float brightness)
        {
            _pointer = Mathf.Max(0f, brightness);
            Apply(_pointer * _fx);
        }

        public void SetFx(float brightness)
        {
            _fx = Mathf.Max(0f, brightness);
            Apply(_pointer * _fx);
        }

        private void Apply(float brightness)
        {
            if (_image == null) return;
            if (Mathf.Approximately(brightness, 1f))
            {
                RestoreMaterial();
            }
            else if ((_applied || Begin()) && brightness != _written)
            {
                _written = brightness;
                // Mask 안에서는 스텐실용으로 한 번 더 복사되므로 실제로 그려지는 쪽에 쓴다.
                Color color = new Color(_tint.r * brightness, _tint.g * brightness, _tint.b * brightness, _tint.a);
                _copy.color = color;
                _image.materialForRendering.color = color;
            }
        }

        public void Clear()
        {
            Set(1f);
        }

        private void RestoreMaterial()
        {
            if (_applied)
            {
                _applied = false;
                if (_image != null) _image.material = _original;
            }
        }

        public void Release()
        {
            if (--_clients > 0) return;
            if (_shared) Shared.Remove(_image);
            RestoreMaterial();
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
            else
            {
                _copy.shader = current.shader;
                _copy.CopyPropertiesFromMaterial(current);
            }
            _image.material = _copy;
            _written = 1f;
            _applied = true;
            return true;
        }
    }
}
