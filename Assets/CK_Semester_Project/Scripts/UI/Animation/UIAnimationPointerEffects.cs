using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation
{
    // Hover·Exit·Pressed 입력 반응이다. 버튼처럼 조작하는 Image에만 붙는다.
    // Animator 결과 위에 얹으므로 Open/Close가 도는 중에도 값이 튀지 않는다.
    // 키보드·패드로 고른 항목도 같은 반응을 쓴다.
    // 애니메이션과 하이라이트는 프리셋에, 씬 오브젝트를 가리키는 트리거는 이 컴포넌트에 있다.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(UIAnimationPlayer))]
    [AddComponentMenu("UI/UI Animation Pointer Effects")]
    public sealed class UIAnimationPointerEffects : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] private UnityEvent _onHover = new UnityEvent();
        [SerializeField] private UnityEvent _onExit = new UnityEvent();
        [SerializeField] private UnityEvent _onPressed = new UnityEvent();

        private UIAnimationPlayer _player;
        private Image _image;
        private UIImageBrightness _imageBrightness;
        private Selectable _selectable;
        private bool _pointerInside;
        private bool _selected;
        private bool _pressed;
        private bool _closing;
        private bool _hoverRaised;
        private float _submitWait;

        private UIPointerEffectSettings _effect;
        private float _effectElapsed;
        private bool _effectApplied;
        private Vector2 _scale = Vector2.one;
        private Vector2 _scaleFrom = Vector2.one;
        private Vector2 _offset;
        private Vector2 _offsetFrom;
        private float _rotation;
        private float _rotationFrom;
        private Color _tint = Color.white;
        private Color _tintFrom = Color.white;
        private float _brightness = 1f;
        private float _brightnessFrom = 1f;
        private Vector3 _animatedScale = Vector3.one;
        private Vector3 _writtenScale = Vector3.positiveInfinity;

        public bool HasTriggers =>
            _onHover.GetPersistentEventCount() + _onExit.GetPersistentEventCount() + _onPressed.GetPersistentEventCount() > 0;

        private UIAnimationPreset Preset => _player.Preset;
        private bool IsHighlighted => _pointerInside || _selected;

        private bool CanReact =>
            Preset != null && !_player.IsClosing && (_selectable == null || _selectable.IsInteractable());

        private void Awake()
        {
            _player = GetComponent<UIAnimationPlayer>();
            _image = GetComponent<Image>();
            _imageBrightness = _image != null ? UIImageBrightness.Acquire(_image) : null;
            _selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            // 꺼지면 Exit 입력이 오지 않는다. Hover에서 켠 것이 남지 않게 짝을 맞춘다.
            // 씬을 내리는 중이면 연결된 오브젝트도 사라지는 중이라 건너뛴다.
            if (_hoverRaised && IsOn(UIPointerEffect.Exit) && gameObject.scene.isLoaded)
            {
                _onExit.Invoke();
            }

            if (_image != null)
            {
                _image.overrideSprite = null;
            }
            if (_effectApplied)
            {
                transform.localScale = _animatedScale;
                if (_image != null)
                {
                    _image.canvasRenderer.SetColor(Color.white);
                    _imageBrightness.Clear();
                }
            }
            _player.SetPointerPose(Vector2.zero, 0f);
            _effect = null;
            _effectApplied = false;
            _scale = Vector2.one;
            _offset = Vector2.zero;
            _rotation = 0f;
            _tint = Color.white;
            _brightness = 1f;
            _writtenScale = Vector3.positiveInfinity;
            _pointerInside = false;
            _selected = false;
            _pressed = false;
            _closing = false;
            _hoverRaised = false;
            _submitWait = 0f;
        }

        private void OnDestroy()
        {
            _imageBrightness?.Release();
        }

        public UnityEvent Trigger(UIPointerEffect effect)
        {
            switch (effect)
            {
                case UIPointerEffect.Hover: return _onHover;
                case UIPointerEffect.Exit: return _onExit;
                default: return _onPressed;
            }
        }

        // 모습만 바꾼다. 효과음과 트리거는 입력이 실제로 일어났을 때 Raise에서 실행한다.
        public void PlayEffect(UIPointerEffect effect)
        {
            if (Preset == null)
            {
                return;
            }

            UIPointerEffectSettings settings = Preset.Get(effect);
            SwapSprite(effect, settings.Enabled ? settings.Sprite : null);
            if (settings.Enabled)
            {
                _effect = settings;
                _effectElapsed = 0f;
                _scaleFrom = _scale;
                _offsetFrom = _offset;
                _rotationFrom = _rotation;
                _tintFrom = _tint;
                _brightnessFrom = _brightness;
            }
            else if (effect != UIPointerEffect.Pressed)
            {
                _effect = null;
                _scale = Vector2.one;
                _offset = Vector2.zero;
                _rotation = 0f;
                _tint = Color.white;
                _brightness = 1f;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            if (CanReact)
            {
                EnterHighlight(!_pressed && !_selected);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            ReturnFromHighlight();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && CanReact)
            {
                _pressed = true;
                PlayEffect(UIPointerEffect.Pressed);
                Raise(UIPointerEffect.Pressed);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressed || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            _pressed = false;
            if (!_player.IsClosing)
            {
                PlayEffect(IsHighlighted ? UIPointerEffect.Hover : UIPointerEffect.Exit);
            }
        }

        // 마우스 클릭으로 선택될 때는 무시한다. 그대로 받으면 마우스가 나가도 Hover 모습이 남는다.
        public void OnSelect(BaseEventData eventData)
        {
            if (eventData is PointerEventData || Preset == null || !Preset.ReactToSelection)
            {
                return;
            }
            _selected = true;
            if (CanReact)
            {
                EnterHighlight(!_pressed);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (_selected)
            {
                _selected = false;
                ReturnFromHighlight();
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (CanReact && Preset.ReactToSelection && Preset.Get(UIPointerEffect.Pressed).Enabled)
            {
                PlayEffect(UIPointerEffect.Pressed);
                Raise(UIPointerEffect.Pressed);
                _submitWait = Preset.Get(UIPointerEffect.Pressed).Duration;
            }
        }

        private bool IsOn(UIPointerEffect effect)
        {
            return Preset != null && Preset.Get(effect).Enabled;
        }

        private void Raise(UIPointerEffect effect)
        {
            if (IsOn(effect))
            {
                UIAnimationSound.Play(Preset.Get(effect).Sound);
                Trigger(effect).Invoke();
            }
        }

        // 마우스와 키보드가 같은 Image에 겹쳐도 Hover와 Exit는 한 번씩만 실행한다.
        private void EnterHighlight(bool playEffect)
        {
            if (playEffect)
            {
                PlayEffect(UIPointerEffect.Hover);
            }
            if (!_hoverRaised)
            {
                _hoverRaised = true;
                Raise(UIPointerEffect.Hover);
            }
        }

        private void ReturnFromHighlight()
        {
            if (IsHighlighted)
            {
                return;
            }
            if (!_pressed && !_player.IsClosing)
            {
                PlayEffect(UIPointerEffect.Exit);
            }
            RaiseExit();
        }

        private void RaiseExit()
        {
            if (_hoverRaised)
            {
                _hoverRaised = false;
                Raise(UIPointerEffect.Exit);
            }
        }

        // Pressed에 이미지가 없으면 Hover 이미지를 그대로 둔다.
        private void SwapSprite(UIPointerEffect effect, Sprite sprite)
        {
            if (_image != null && (sprite != null || effect != UIPointerEffect.Pressed))
            {
                _image.overrideSprite = sprite;
            }
        }

        private void LateUpdate()
        {
            if (_player.IsClosing && !_closing)
            {
                _pressed = false;
                PlayEffect(UIPointerEffect.Exit);
                RaiseExit();
            }
            _closing = _player.IsClosing;

            if (_submitWait > 0f)
            {
                _submitWait -= Time.unscaledDeltaTime;
                if (_submitWait <= 0f && !_player.IsClosing)
                {
                    PlayEffect(IsHighlighted ? UIPointerEffect.Hover : UIPointerEffect.Exit);
                }
            }
            TickEffect();
        }

        private void TickEffect()
        {
            if (_effect != null)
            {
                _effectElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_effectElapsed / _effect.Duration);
                UIVector2Track scale = _effect.Scale;
                UIVector2Track offset = _effect.Offset;
                UIFloatTrack rotation = _effect.Rotation;
                UIColorTrack color = _effect.Color;
                _scale = scale.Evaluate(_scaleFrom, scale.Use ? scale.To : Vector2.one, t);
                _offset = offset.Evaluate(_offsetFrom, offset.Use ? offset.To : Vector2.zero, t);
                _rotation = Mathf.LerpUnclamped(_rotationFrom, rotation.Use ? rotation.To : 0f, rotation.Curve.Evaluate(t));
                _tint = Color.LerpUnclamped(_tintFrom, color.Use ? color.To : Color.white, color.Curve.Evaluate(t));
                float brightness = color.Use ? _effect.Brightness : 1f;
                _brightness = Mathf.Max(0f, Mathf.LerpUnclamped(_brightnessFrom, brightness, color.Curve.Evaluate(t)));
                if (t >= 1f)
                {
                    _effect = null;
                }
            }

            bool active = _effect != null || _scale != Vector2.one || _offset != Vector2.zero
                || !Mathf.Approximately(_rotation, 0f) || _tint != Color.white || !Mathf.Approximately(_brightness, 1f);
            if (active || _effectApplied)
            {
                WriteEffect();
                _effectApplied = active;
            }
        }

        private void WriteEffect()
        {
            Vector3 current = transform.localScale;
            if (current != _writtenScale)
            {
                _animatedScale = current;
            }
            _writtenScale = Vector3.Scale(_animatedScale, new Vector3(_scale.x, _scale.y, 1f));
            transform.localScale = _writtenScale;
            _player.SetPointerPose(_offset, _rotation);
            if (_image != null)
            {
                _image.canvasRenderer.SetColor(_tint);
                _imageBrightness.Set(_brightness);
            }
        }
    }
}
