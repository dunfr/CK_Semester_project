using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation
{
    public enum UIAnimationStyle
    {
        Pop,
        Fade,
        BouncePop,
        ExpandHorizontal,
        ExpandVertical,
        PersonaDrift,
        PersonaSlash,
        StarRailSlide,
        StarRailWipe,
        ZenlessTv,
        ZenlessSnap
    }

    public enum UIStayStyle
    {
        Still,
        Float,
        Pulse
    }

    public enum UIPointerStyle
    {
        Grow,
        Nudge,
        Punch,
        Glow
    }

    // 인스펙터에서 고치는 값 전체다. Controller 안의 하위 에셋으로 저장되어 파일이 늘지 않고,
    // 같은 Controller를 쓰는 Image끼리 공유된다. 저장한 프리셋은 독립 에셋으로도 남는다.
    public sealed class UIAnimationPreset : ScriptableObject
    {
        public const string IsOpenParameter = "IsOpen";
        public const string OffsetField = "_animatedOffset";
        public const string RotationField = "_animatedRotation";

        [SerializeField] private Vector3 _baseScale = Vector3.one;
        [SerializeField] private Color _baseColor = Color.white;
        [SerializeField] private bool _imageEnabled = true;
        [SerializeField] private Image.Type _imageType = Image.Type.Simple;
        [SerializeField] private bool _fadeChildren;
        [SerializeField] private UIWipeDirection _wipeDirection;
        [SerializeField] private bool _reactToSelection = true;
        [SerializeField] private UIAnimationStateSettings _open = new UIAnimationStateSettings();
        [SerializeField] private UIAnimationStateSettings _stay = new UIAnimationStateSettings();
        [SerializeField] private UIAnimationStateSettings _close = new UIAnimationStateSettings();
        [SerializeField] private UIPointerEffectSettings _hover = new UIPointerEffectSettings();
        [SerializeField] private UIPointerEffectSettings _exit = new UIPointerEffectSettings();
        [SerializeField] private UIPointerEffectSettings _pressed = new UIPointerEffectSettings();

        public Vector3 BaseScale => _baseScale;
        public Color BaseColor => _baseColor;

        // 자식만 보여 주는 틀처럼 Image를 꺼 둔 경우 재생 중에도 꺼진 채로 둔다.
        public bool ImageEnabled => _imageEnabled;

        // 와이프를 끄면 Image를 이 방식으로 되돌린다.
        public Image.Type ImageType => _imageType;

        // 켜면 알파를 CanvasGroup에 넣어 자식까지 같이 사라진다.
        public bool FadeChildren
        {
            get => _fadeChildren;
            set => _fadeChildren = value;
        }

        public UIWipeDirection WipeDirection
        {
            get => _wipeDirection;
            set => _wipeDirection = value;
        }

        // 켜면 키보드·패드로 고른 항목도 Hover처럼 반응한다.
        public bool ReactToSelection
        {
            get => _reactToSelection;
            set => _reactToSelection = value;
        }

        public bool AnimatesOffset => _open.Offset.Use || _stay.Offset.Use || _close.Offset.Use;
        public bool AnimatesScale => _open.Scale.Use || _stay.Scale.Use || _close.Scale.Use;
        public bool AnimatesRotation => _open.Rotation.Use || _stay.Rotation.Use || _close.Rotation.Use;
        public bool AnimatesColor => _open.Color.Use || _stay.Color.Use || _close.Color.Use;
        public bool AnimatesWipe => _open.Wipe.Use || _stay.Wipe.Use || _close.Wipe.Use;
        public bool HasPointerEffects => _hover.Enabled || _exit.Enabled || _pressed.Enabled;

        // 값이 변하지 않는 Stay는 Animator를 세워 Canvas가 매 프레임 다시 그려지지 않게 한다.
        public bool IsStayStatic
        {
            get
            {
                UIAnimationSpan span = GetSpan(UIAnimationState.Stay);
                return span.Offset.From == span.Offset.To
                    && span.Scale.From == span.Scale.To
                    && Mathf.Approximately(span.Rotation.From, span.Rotation.To)
                    && span.Color.From == span.Color.To
                    && Mathf.Approximately(span.Wipe.From, span.Wipe.To);
            }
        }

        public UIAnimationStateSettings Get(UIAnimationState state)
        {
            switch (state)
            {
                case UIAnimationState.Open: return _open;
                case UIAnimationState.Stay: return _stay;
                default: return _close;
            }
        }

        public UIPointerEffectSettings Get(UIPointerEffect effect)
        {
            switch (effect)
            {
                case UIPointerEffect.Hover: return _hover;
                case UIPointerEffect.Exit: return _exit;
                default: return _pressed;
            }
        }

        // Open만 시작값을 직접 고른다. Stay와 Close는 Open이 끝난 값에서 이어져 튀지 않는다.
        public UIAnimationSpan GetSpan(UIAnimationState state)
        {
            UIRange<Vector2> offset = First(_open.Offset, Vector2.zero);
            UIRange<Vector2> scale = First(_open.Scale, _baseScale);
            UIRange<float> rotation = First(_open.Rotation, 0f);
            UIRange<Color> color = First(_open.Color, _baseColor);
            UIRange<float> wipe = First(_open.Wipe, 1f);
            if (state != UIAnimationState.Open)
            {
                UIAnimationStateSettings settings = Get(state);
                offset = Next(settings.Offset, offset.To);
                scale = Next(settings.Scale, scale.To);
                rotation = Next(settings.Rotation, rotation.To);
                color = Next(settings.Color, color.To);
                wipe = Next(settings.Wipe, wipe.To);
            }
            return new UIAnimationSpan(offset, scale, rotation, color, wipe);
        }

        // 지금 Image의 크기와 색을 기준으로 기본값을 채운다.
        public void Capture(Image image, bool fadeChildren, UIPointerEffectMask pointerEffects)
        {
            _baseScale = image.rectTransform.localScale;
            _baseColor = image.color;
            _imageEnabled = image.enabled;
            _imageType = image.type;
            _fadeChildren = fadeChildren;
            if (fadeChildren)
            {
                _baseColor.a = 1f;
            }

            _stay.Time = 1f;
            _stay.ClearMotion();
            _close.KeepEnabled = false;
            ApplyStyle(UIAnimationStyle.Pop);

            SetPointerEffects(pointerEffects);
            ApplyPointerStyle(UIPointerStyle.Grow);
        }

        public void SetPointerEffects(UIPointerEffectMask pointerEffects)
        {
            _hover.Enabled = (pointerEffects & UIPointerEffectMask.Hover) != 0;
            _exit.Enabled = (pointerEffects & UIPointerEffectMask.Exit) != 0;
            _pressed.Enabled = (pointerEffects & UIPointerEffectMask.Pressed) != 0;
        }

        public void ApplyStyle(UIAnimationStyle style)
        {
            Vector2 full = _baseScale;
            Color shown = _baseColor;
            Color hidden = shown;
            hidden.a = 0f;

            _open.ClearMotion();
            _close.ClearMotion();
            _open.Color.Set(hidden, shown, UIEase.EaseOutCubic);
            _close.Color.Set(shown, hidden, UIEase.EaseInCubic);
            switch (style)
            {
                case UIAnimationStyle.Pop:
                    SetTimes(0.25f, 0.2f);
                    SetScale(full * 0.9f, full, UIEase.EaseOutBack, UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.Fade:
                    SetTimes(0.25f, 0.2f);
                    break;
                case UIAnimationStyle.BouncePop:
                    SetTimes(0.6f, 0.2f);
                    SetScale(full * 0.5f, full, UIEase.EaseOutElastic, UIEase.EaseInBack);
                    break;
                case UIAnimationStyle.ExpandHorizontal:
                    SetTimes(0.3f, 0.2f);
                    SetScale(new Vector2(0f, full.y), full, UIEase.EaseOutCubic, UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.ExpandVertical:
                    SetTimes(0.3f, 0.2f);
                    SetScale(new Vector2(full.x, 0f), full, UIEase.EaseOutCubic, UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.PersonaDrift:
                    SetTimes(0.28f, 0.18f);
                    _open.Offset.Set(new Vector2(-80f, 20f), Vector2.zero, UIEase.EaseOutCubic);
                    _close.Offset.Set(Vector2.zero, new Vector2(-40f, 10f), UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.PersonaSlash:
                    SetTimes(0.24f, 0.16f);
                    _open.Color.Set(hidden, shown, UIEase.EaseOutQuad);
                    _open.Offset.Set(new Vector2(120f, 0f), Vector2.zero, UIEase.EaseOutBack);
                    _open.Rotation.Set(-8f, 0f, UIEase.EaseOutBack);
                    _close.Offset.Set(Vector2.zero, new Vector2(-120f, 0f), UIEase.EaseInCubic);
                    _close.Rotation.Set(0f, 4f, UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.StarRailSlide:
                    SetTimes(0.18f, 0.12f);
                    _open.Offset.Set(new Vector2(0f, -20f), Vector2.zero, UIEase.EaseOutCubic);
                    _close.Offset.Set(Vector2.zero, new Vector2(0f, -10f), UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.StarRailWipe:
                    SetTimes(0.26f, 0.18f);
                    _open.Color.Off();
                    _close.Color.Off();
                    _open.Wipe.Set(0f, 1f, UIEase.EaseInOutCubic);
                    _close.Wipe.Set(1f, 0f, UIEase.EaseInOutCubic);
                    break;
                case UIAnimationStyle.ZenlessTv:
                    SetTimes(0.14f, 0.09f);
                    _open.Color.Off();
                    _close.Color.Off();
                    SetScale(new Vector2(full.x, full.y * 0.02f), full, UIEase.EaseOutExpo, UIEase.EaseInExpo);
                    break;
                case UIAnimationStyle.ZenlessSnap:
                    SetTimes(0.18f, 0.12f);
                    _open.StepsPerSecond = 24;
                    _close.StepsPerSecond = 24;
                    _open.Color.Set(hidden, shown, UIEase.EaseOutExpo);
                    _open.Scale.Set(full * 1.18f, full, UIEase.EaseOutBack);
                    _close.Scale.Set(full, full * 0.92f, UIEase.EaseInQuad);
                    _open.Rotation.Set(2.5f, 0f, UIEase.EaseOutBack);
                    break;
            }
        }

        // Stay에서 계속 움직이는 대기 동작이다. 값이 있으면 Animator가 멈추지 않는다.
        public void ApplyStayStyle(UIStayStyle style)
        {
            _stay.ClearMotion();
            switch (style)
            {
                case UIStayStyle.Float:
                    _stay.Time = 3.6f;
                    _stay.Offset.Set(Vector2.zero, new Vector2(0f, 6f), UIEase.EaseInOutSine);
                    break;
                case UIStayStyle.Pulse:
                    Color dim = GetSpan(UIAnimationState.Open).Color.To;
                    dim.a *= 0.75f;
                    _stay.Time = 3.6f;
                    _stay.Color.Set(dim, dim, UIEase.EaseInOutSine);
                    break;
                default:
                    _stay.Time = 1f;
                    break;
            }
        }

        public void ApplyPointerStyle(UIPointerStyle style)
        {
            Color pressedTint = new Color(0.85f, 0.85f, 0.85f, 1f);
            _hover.ClearMotion();
            _exit.ClearMotion();
            _pressed.ClearMotion();
            _exit.Time = 0.12f;
            _exit.Scale.Set(Vector2.one, Vector2.one, UIEase.EaseOutCubic);
            _exit.Color.Set(Color.white, Color.white, UIEase.EaseOutCubic);
            switch (style)
            {
                case UIPointerStyle.Grow:
                    _hover.Time = 0.1f;
                    _hover.Scale.Set(Vector2.one, Vector2.one * 1.04f, UIEase.EaseOutCubic);
                    _pressed.Time = 0.06f;
                    _pressed.Scale.Set(Vector2.one, Vector2.one * 0.96f, UIEase.EaseOutQuad);
                    _pressed.Color.Set(Color.white, pressedTint, UIEase.EaseOutQuad);
                    break;
                case UIPointerStyle.Nudge:
                    _hover.Time = 0.1f;
                    _hover.Offset.Set(Vector2.zero, new Vector2(14f, 0f), UIEase.EaseOutCubic);
                    _exit.Offset.Set(Vector2.zero, Vector2.zero, UIEase.EaseOutCubic);
                    _pressed.Time = 0.05f;
                    _pressed.Offset.Set(Vector2.zero, new Vector2(6f, 0f), UIEase.EaseOutQuad);
                    _pressed.Color.Set(Color.white, pressedTint, UIEase.EaseOutQuad);
                    break;
                case UIPointerStyle.Punch:
                    _hover.Time = 0.14f;
                    _hover.Scale.Set(Vector2.one, Vector2.one * 1.07f, UIEase.EaseOutBack);
                    _hover.Rotation.Set(0f, -1.5f, UIEase.EaseOutBack);
                    _exit.Rotation.Set(0f, 0f, UIEase.EaseOutCubic);
                    _pressed.Time = 0.05f;
                    _pressed.Scale.Set(Vector2.one, Vector2.one * 0.9f, UIEase.EaseOutQuad);
                    _pressed.Color.Set(Color.white, pressedTint, UIEase.EaseOutQuad);
                    break;
                case UIPointerStyle.Glow:
                    _hover.Time = 0.1f;
                    _hover.Color.Set(Color.white, Color.white, UIEase.EaseOutCubic);
                    _hover.Brightness = 1.3f;
                    _pressed.Time = 0.05f;
                    _pressed.Scale.Set(Vector2.one, Vector2.one * 0.98f, UIEase.EaseOutQuad);
                    _pressed.Color.Set(Color.white, Color.white, UIEase.EaseOutQuad);
                    _pressed.Brightness = 1.1f;
                    break;
            }
        }

        // Open을 거꾸로 돌린 Close를 만든다. 곡선도 In과 Out을 맞바꿔 대칭으로 보이게 한다.
        public void MirrorOpenToClose()
        {
            _close.Time = _open.Time * 0.8f;
            _close.Speed = _open.Speed;
            _close.StepsPerSecond = _open.StepsPerSecond;
            Mirror(_open.Offset, _close.Offset);
            Mirror(_open.Scale, _close.Scale);
            Mirror(_open.Rotation, _close.Rotation);
            Mirror(_open.Wipe, _close.Wipe);
            if (_open.Color.Use)
            {
                _close.Color.Set(_open.Color.To, _open.Color.From, UIEasing.Mirror(_open.Color.Curve.Ease));
            }
            else
            {
                _close.Color.Off();
            }
        }

        // 저장한 프리셋을 불러온다. 크기는 이 Image의 기준 크기에 맞춰 환산한다.
        public void CopyMotionFrom(UIAnimationPreset source)
        {
            Vector3 baseScale = _baseScale;
            Color baseColor = _baseColor;
            bool imageEnabled = _imageEnabled;
            Image.Type imageType = _imageType;
            Vector2 ratio = new Vector2(
                source._baseScale.x != 0f ? baseScale.x / source._baseScale.x : 1f,
                source._baseScale.y != 0f ? baseScale.y / source._baseScale.y : 1f);

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), this);
            _baseScale = baseScale;
            _baseColor = baseColor;
            _imageEnabled = imageEnabled;
            _imageType = imageType;
            _open.Scale.Rescale(ratio);
            _stay.Scale.Rescale(ratio);
            _close.Scale.Rescale(ratio);
        }

        private void SetTimes(float open, float close)
        {
            _open.Time = open;
            _close.Time = close;
        }

        private void SetScale(Vector2 hidden, Vector2 full, UIEase openEase, UIEase closeEase)
        {
            _open.Scale.Set(hidden, full, openEase);
            _close.Scale.Set(full, hidden, closeEase);
        }

        private static void Mirror(UIVector2Track open, UIVector2Track close)
        {
            if (open.Use)
            {
                close.Set(open.To, open.From, UIEasing.Mirror(open.Curve.Ease));
            }
            else
            {
                close.Off();
            }
        }

        private static void Mirror(UIFloatTrack open, UIFloatTrack close)
        {
            if (open.Use)
            {
                close.Set(open.To, open.From, UIEasing.Mirror(open.Curve.Ease));
            }
            else
            {
                close.Off();
            }
        }

        private static UIRange<Vector2> First(UIVector2Track track, Vector2 rest)
        {
            return track.Use ? new UIRange<Vector2>(track.From, track.To) : new UIRange<Vector2>(rest, rest);
        }

        private static UIRange<float> First(UIFloatTrack track, float rest)
        {
            return track.Use ? new UIRange<float>(track.From, track.To) : new UIRange<float>(rest, rest);
        }

        private static UIRange<Color> First(UIColorTrack track, Color rest)
        {
            return track.Use ? new UIRange<Color>(track.From, track.To) : new UIRange<Color>(rest, rest);
        }

        private static UIRange<Vector2> Next(UIVector2Track track, Vector2 from)
        {
            return new UIRange<Vector2>(from, track.Use ? track.To : from);
        }

        private static UIRange<float> Next(UIFloatTrack track, float from)
        {
            return new UIRange<float>(from, track.Use ? track.To : from);
        }

        private static UIRange<Color> Next(UIColorTrack track, Color from)
        {
            return new UIRange<Color>(from, track.Use ? track.To : from);
        }
    }
}
