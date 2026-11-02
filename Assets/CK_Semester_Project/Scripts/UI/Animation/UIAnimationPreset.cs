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
        ZenlessSnap,
        PersonaCutIn,
        PersonaList,
        StarRailHologram,
        StarRailWindow,
        ZenlessGlitch,
        ZenlessSticker,
        JrpgCommand,
        JrpgScroll,
        JrpgSummon,
        JrpgVictory,
        JrpgDialogue,
        JrpgItem
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
        [SerializeField] private string _statePresetName = string.Empty;
        [SerializeField] private string _statePresetDescription = string.Empty;
        [SerializeField] private string _statePresetEdited = string.Empty;
        [SerializeField] private string _stayPresetName = string.Empty;
        [SerializeField] private string _pointerPresetName = string.Empty;
        [SerializeField] private string _pointerPresetEdited = string.Empty;

        public string StatePresetName => _statePresetName;
        public string StatePresetDescription => _statePresetDescription;
        public string PointerPresetName => _pointerPresetName;
        public bool StatePresetEdited => !string.IsNullOrEmpty(_statePresetEdited);
        public bool PointerPresetEdited => !string.IsNullOrEmpty(_pointerPresetEdited);
        public string StatePresetLabel => PresetLabel(_statePresetName, StatePresetEdited);
        public string PointerPresetLabel => PresetLabel(_pointerPresetName, PointerPresetEdited);

        // Stay만 따로 고르면 이 이름을 보여 주고 전체 프리셋 이름은 그대로 둔다.
        public string StayPresetName => _stayPresetName;
        public string StayPresetLabel => PresetLabel(string.IsNullOrEmpty(_stayPresetName) ? _statePresetName : _stayPresetName, StatePresetEdited);

        public void SetStatePreset(string name, string description = "")
        {
            _statePresetName = name;
            _statePresetDescription = description;
            _statePresetEdited = string.Empty;
        }

        public void SetPointerPreset(string name)
        {
            _pointerPresetName = name;
            _pointerPresetEdited = string.Empty;
        }

        public void MarkStatePresetEdited() => _statePresetEdited = "세부 조정함";
        public void MarkPointerPresetEdited() => _pointerPresetEdited = "세부 조정함";

        private static string PresetLabel(string name, bool edited)
        {
            return "적용한 프리셋: " + (string.IsNullOrEmpty(name) ? "없음" : name + (edited ? " (세부 조정함)" : ""));
        }

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
            _open.ClearMotion();
            _stay.ClearMotion();
            _close.ClearMotion();
            _open.Speed = _stay.Speed = _close.Speed = 1f;
            SetStayPeriod(3.6f);
            _stayPresetName = string.Empty;
            _wipeDirection = UIWipeDirection.LeftToRight;

            switch (style)
            {
                case UIAnimationStyle.Pop:
                    Entrance(0.25f, Vector2.zero, new Vector2(0.88f, 0.88f), 0f, 0.04f);
                    break;
                case UIAnimationStyle.Fade:
                    Entrance(0.24f, Vector2.zero, Vector2.one, 0f);
                    break;
                case UIAnimationStyle.BouncePop:
                    Entrance(0.46f, new Vector2(0f, -32f), new Vector2(0.76f, 0.76f), 0f, 0.06f);
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Anticipate(0.06f, 0.12f, UIEase.EaseOutCubic));
                    SetScaleSettle(new Vector2(0.76f, 0.76f), 0.06f, 0.74f);
                    break;
                case UIAnimationStyle.ExpandHorizontal:
                    Entrance(0.34f, Vector2.zero, new Vector2(0.04f, 0.92f), 0f, 0.03f);
                    SplitScaleWindows(0f, 0.76f, 0.22f, 1f);
                    break;
                case UIAnimationStyle.ExpandVertical:
                    Entrance(0.34f, Vector2.zero, new Vector2(0.92f, 0.04f), 0f, 0.03f);
                    SplitScaleWindows(0.22f, 1f, 0f, 0.76f);
                    break;
                case UIAnimationStyle.PersonaDrift:
                    Entrance(0.38f, new Vector2(-64f, -24f), new Vector2(0.94f, 0.96f), -2.5f, 0.03f);
                    _open.Offset.SetYCurve(UIMotionCurves.Window(UIEase.EaseOutCubic, 0.12f, 1f));
                    SetFloatStay(4.4f, 4f);
                    break;
                case UIAnimationStyle.PersonaSlash:
                    Entrance(0.28f, new Vector2(120f, 0f), new Vector2(0.86f, 0.92f), -8f, 0.06f);
                    _open.Rotation.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuart, 0.12f, 1f));
                    break;
                case UIAnimationStyle.PersonaCutIn:
                    Entrance(0.20f, new Vector2(-132f, -32f), new Vector2(0.88f, 0.96f), -10f, 0.04f);
                    SetWipe(UIWipeDirection.LeftToRight, 0.02f, 0.38f);
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuint, 0.06f, 0.9f));
                    break;
                case UIAnimationStyle.PersonaList:
                    Entrance(0.32f, new Vector2(-112f, 24f), new Vector2(0.96f, 1f), 7f);
                    _open.Rotation.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuart, 0.16f, 1f));
                    break;
                case UIAnimationStyle.StarRailSlide:
                    Entrance(0.30f, new Vector2(48f, 0f), Vector2.one, 0f);
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, 0.02f, 1f));
                    break;
                case UIAnimationStyle.StarRailWipe:
                    Entrance(0.36f, new Vector2(24f, 0f), Vector2.one, 0f);
                    SetWipe(UIWipeDirection.LeftToRight, 0f, 0.82f);
                    _open.Color.Off();
                    _close.Color.Off();
                    break;
                case UIAnimationStyle.StarRailHologram:
                    Entrance(0.44f, new Vector2(0f, -28f), new Vector2(0.97f, 0.08f), 0f);
                    SplitScaleWindows(0.2f, 1f, 0f, 0.8f);
                    SetWipe(UIWipeDirection.BottomToTop, 0f, 0.64f);
                    SetPulseStay(4.8f, 0.95f);
                    break;
                case UIAnimationStyle.StarRailWindow:
                    Entrance(0.38f, new Vector2(0f, -24f), new Vector2(0.95f, 0.95f), 0f);
                    _open.Scale.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutSine, 0.08f, 1f));
                    break;
                case UIAnimationStyle.ZenlessTv:
                    Entrance(0.24f, Vector2.zero, new Vector2(0.02f, 0.015f), 0f);
                    SplitScaleWindows(0f, 0.36f, 0.32f, 0.92f);
                    _open.StepsPerSecond = _close.StepsPerSecond = 24;
                    _close.Scale.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseInCubic, 0.64f, 1f));
                    _close.Scale.SetYCurve(UIMotionCurves.Window(UIEase.EaseInCubic, 0f, 0.66f));
                    break;
                case UIAnimationStyle.ZenlessSnap:
                    Entrance(0.24f, new Vector2(0f, -56f), new Vector2(0.88f, 0.78f), -2f, 0.05f);
                    _open.StepsPerSecond = _close.StepsPerSecond = 24;
                    break;
                case UIAnimationStyle.ZenlessGlitch:
                    Entrance(0.26f, new Vector2(72f, 0f), new Vector2(0.92f, 0.88f), 3f, 0.04f);
                    _open.StepsPerSecond = _close.StepsPerSecond = 24;
                    _open.Color.Curve.SetCustom(UIMotionCurves.Steps((0f, 0f), (0.08f, 1f), (0.14f, 0.25f),
                        (0.21f, 1f), (0.27f, 0.6f), (0.34f, 1f), (1f, 1f)));
                    break;
                case UIAnimationStyle.ZenlessSticker:
                    Entrance(0.28f, new Vector2(24f, 64f), new Vector2(1.18f, 1.18f), -9f, -0.04f);
                    _open.StepsPerSecond = _close.StepsPerSecond = 24;
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseInCubic, 0f, 0.66f));
                    break;
                case UIAnimationStyle.JrpgCommand:
                    Entrance(0.24f, Vector2.zero, new Vector2(0.92f, 0.08f), 0f);
                    SplitScaleWindows(0f, 0.5f, 0.12f, 1f);
                    _open.StepsPerSecond = _close.StepsPerSecond = 16;
                    break;
                case UIAnimationStyle.JrpgScroll:
                    Entrance(0.56f, Vector2.zero, new Vector2(0.06f, 0.04f), 0f);
                    SplitScaleWindows(0f, 0.35f, 0.24f, 1f);
                    _close.Scale.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseInCubic, 0.65f, 1f));
                    _close.Scale.SetYCurve(UIMotionCurves.Window(UIEase.EaseInCubic, 0f, 0.72f));
                    break;
                case UIAnimationStyle.JrpgSummon:
                    Entrance(0.60f, new Vector2(-40f, -96f), new Vector2(0.86f, 0.90f), -10f, 0.03f);
                    _open.Offset.SetYCurve(UIMotionCurves.Window(UIEase.EaseOutCubic, 0f, 0.82f));
                    _open.Rotation.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseInOutSine, 0.16f, 1f));
                    SetFloatStay(5f, 3f);
                    break;
                case UIAnimationStyle.JrpgVictory:
                    Entrance(0.42f, new Vector2(-140f, 0f), new Vector2(1.14f, 0.86f), 4f, 0.05f);
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuint, 0.06f, 0.78f));
                    break;
                case UIAnimationStyle.JrpgDialogue:
                    Entrance(0.36f, new Vector2(0f, -28f), new Vector2(1f, 0.94f), 0f);
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutSine, 0f, 1f));
                    break;
                case UIAnimationStyle.JrpgItem:
                    Entrance(0.48f, new Vector2(0f, -64f), new Vector2(0.82f, 0.82f), 5f, 0.04f);
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Anticipate(0.06f, 0.1f, UIEase.EaseOutCubic));
                    SetPulseStay(4.2f, 0.94f);
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(style));
            }
            SetStatePreset(UIAnimationPresetCatalog.Name(style), UIAnimationPresetCatalog.Description(style));
        }

        private void Entrance(float time, Vector2 offset, Vector2 factors, float tilt, float settle = 0f)
        {
            Vector2 full = _baseScale;
            Color hidden = _baseColor;
            hidden.a = 0f;
            SetTimes(time, time * 0.65f);
            _open.Color.Set(hidden, _baseColor, UIEase.Linear);
            _open.Color.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuad, 0f, 0.30f));
            _close.Color.Set(_baseColor, hidden, UIEase.Linear);
            _close.Color.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseInCubic, 0.5f, 1f));
            if (offset != Vector2.zero)
            {
                _open.Offset.Set(offset, Vector2.zero, UIEase.Linear);
                _open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuart, 0f, 1f));
                _close.Offset.Set(Vector2.zero, offset.normalized * Mathf.Max(24f, offset.magnitude * 0.65f), UIEase.EaseInCubic);
            }
            if (factors != Vector2.one)
            {
                _open.Scale.Set(Vector2.Scale(full, factors), full, UIEase.Linear);
                _open.Scale.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, 0.04f, 1f));
                _close.Scale.Set(full, Vector2.Scale(full, Vector2.Lerp(Vector2.one, factors, 0.6f)), UIEase.EaseInCubic);
                if (settle != 0f) SetScaleSettle(factors, settle, 0.76f);
            }
            if (tilt != 0f)
            {
                _open.Rotation.Set(tilt, 0f, UIEase.Linear);
                _open.Rotation.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, 0.08f, 1f));
                _close.Rotation.Set(0f, tilt * 0.5f, UIEase.EaseInCubic);
            }
        }

        private void SetScaleSettle(Vector2 factors, float amount, float peakTime)
        {
            float Peak(float start) => Mathf.Approximately(start, 1f) ? 1f : (1f + amount - start) / (1f - start);
            _open.Scale.Curve.SetCustom(UIMotionCurves.Settle(Peak(factors.x), peakTime, UIEase.EaseOutCubic, UIEase.EaseInOutSine));
            _open.Scale.SetYCurve(UIMotionCurves.Settle(Peak(factors.y), peakTime, UIEase.EaseOutCubic, UIEase.EaseInOutSine));
        }

        private void SplitScaleWindows(float xStart, float xEnd, float yStart, float yEnd)
        {
            _open.Scale.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, xStart, xEnd));
            _open.Scale.SetYCurve(UIMotionCurves.Window(UIEase.EaseOutCubic, yStart, yEnd));
        }

        private void SetWipe(UIWipeDirection direction, float start, float end)
        {
            _wipeDirection = direction;
            _open.Wipe.Set(0f, 1f, UIEase.Linear);
            _open.Wipe.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, start, end));
            _close.Wipe.Set(1f, 0f, UIEase.EaseInCubic);
        }

        // 자동 Close를 켜면 Time이 유지 시간이므로 프리셋이 덮어쓰지 않는다.
        private void SetStayPeriod(float period)
        {
            if (!_stay.AutoClose)
            {
                _stay.Time = period;
            }
        }

        private void SetFloatStay(float period, float height)
        {
            SetStayPeriod(period);
            _stay.Offset.Set(Vector2.zero, new Vector2(0f, height), UIEase.EaseInOutSine);
        }

        private void SetPulseStay(float period, float alpha)
        {
            Color dim = _baseColor;
            dim.a *= alpha;
            SetStayPeriod(period);
            _stay.Color.Set(_baseColor, dim, UIEase.EaseInOutSine);
        }

        // Stay에서 계속 움직이는 대기 동작이다. 값이 있으면 Animator가 멈추지 않는다.
        public void ApplyStayStyle(UIStayStyle style)
        {
            _stay.ClearMotion();
            _stay.Speed = 1f;
            switch (style)
            {
                case UIStayStyle.Float:
                    SetStayPeriod(3.6f);
                    _stay.Offset.Set(Vector2.zero, new Vector2(0f, 6f), UIEase.EaseInOutSine);
                    break;
                case UIStayStyle.Pulse:
                    Color shown = GetSpan(UIAnimationState.Open).Color.To;
                    Color dim = shown;
                    dim.a *= 0.9f;
                    SetStayPeriod(3.6f);
                    _stay.Color.Set(shown, dim, UIEase.EaseInOutSine);
                    break;
                default:
                    SetStayPeriod(3.6f);
                    break;
            }
            _stayPresetName = UIAnimationPresetCatalog.Name(style);
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
            SetPointerPreset(UIAnimationPresetCatalog.Name(style));
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
                _close.Color.Curve.CopyFrom(_open.Color.Curve, true);
            }
            else
            {
                _close.Color.Off();
            }
        }

        // 저장한 프리셋을 불러온다.
        public void CopyMotionFrom(UIAnimationPreset source)
        {
            CopyValuesFrom(source);
            SetStatePreset(source.name);
            SetPointerPreset(source.name);
        }

        // 적용한 프리셋 표시까지 그대로 옮긴다. 크기는 이 Image의 기준 크기에 맞춰 환산한다.
        public void CopyValuesFrom(UIAnimationPreset source)
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

        private static void Mirror(UIVector2Track open, UIVector2Track close)
        {
            if (open.Use)
            {
                close.Set(open.To, open.From, UIEasing.Mirror(open.Curve.Ease));
                close.Curve.CopyFrom(open.Curve, true);
                if (open.SplitY)
                {
                    close.SetYCurve(AnimationCurve.Linear(0f, 0f, 1f, 1f));
                    close.CurveY.CopyFrom(open.CurveY, true);
                }
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
                close.Curve.CopyFrom(open.Curve, true);
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
