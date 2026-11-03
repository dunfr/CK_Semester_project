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
        public const string IsHiddenParameter = "IsHidden";
        public const string OffsetField = "_animatedOffset";
        public const string RotationField = "_animatedRotation";

        [SerializeField] private Vector3 _baseScale = Vector3.one;
        [SerializeField] private Color _baseColor = Color.white;
        [SerializeField] private bool _imageEnabled = true;
        [SerializeField] private Image.Type _imageType = Image.Type.Simple;
        [SerializeField] private bool _fadeChildren;
        [SerializeField] private UIWipeDirection _wipeDirection;
        [SerializeField] private float _wipeAngle;
        [SerializeField, Range(0f, 1f)] private float _wipeSoftness;
        [SerializeField, Min(1)] private int _glitchBands = 12;
        [SerializeField, Min(1f)] private float _glitchFps = 12f;
        [SerializeField, Min(1f)] private float _dissolveCellSize = 16f;
        [SerializeField] private float _sheenAngle = 25f;
        [SerializeField, Range(0.01f, 1f)] private float _sheenWidth = 0.2f;
        [SerializeField, Min(0f)] private float _sheenIntensity = 1f;
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
        [SerializeField] private bool _hideShowEnabled;
        [SerializeField] private UIAnimationStateSettings _hide = new UIAnimationStateSettings();
        [SerializeField] private UIAnimationStateSettings _show = new UIAnimationStateSettings();

        public bool HideShowEnabled => _hideShowEnabled;

        public void SetHideShowEnabled(bool enabled)
        {
            if (_hideShowEnabled == enabled) return;
            _hideShowEnabled = enabled;
            if (enabled) GenerateHideShow();
        }

        private void GenerateHideShow()
        {
            _hide = JsonUtility.FromJson<UIAnimationStateSettings>(JsonUtility.ToJson(_close));
            _show = JsonUtility.FromJson<UIAnimationStateSettings>(JsonUtility.ToJson(_open));
            _hide.KeepEnabled = _show.KeepEnabled = true;
            _hide.AutoClose = _show.AutoClose = false;
            _show.Time = _open.Time * 0.7f;
        }

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

        public bool AnimatesOffset => _open.Offset.Use || _stay.Offset.Use || _close.Offset.Use
            || _hideShowEnabled && (_hide.Offset.Use || _show.Offset.Use);
        public bool AnimatesScale => _open.Scale.Use || _stay.Scale.Use || _close.Scale.Use
            || _hideShowEnabled && (_hide.Scale.Use || _show.Scale.Use);
        public bool AnimatesRotation => _open.Rotation.Use || _stay.Rotation.Use || _close.Rotation.Use
            || _hideShowEnabled && (_hide.Rotation.Use || _show.Rotation.Use);
        public bool AnimatesColor => _open.Color.Use || _stay.Color.Use || _close.Color.Use
            || _hideShowEnabled && (_hide.Color.Use || _show.Color.Use);
        public bool AnimatesWipe => _open.Wipe.Use || _stay.Wipe.Use || _close.Wipe.Use
            || _hideShowEnabled && (_hide.Wipe.Use || _show.Wipe.Use);
        public bool AnimatesSkew => _open.Skew.Use || _stay.Skew.Use || _close.Skew.Use
            || _hideShowEnabled && (_hide.Skew.Use || _show.Skew.Use);
        public bool AnimatesSlice => _open.Slice.Use || _stay.Slice.Use || _close.Slice.Use
            || _hideShowEnabled && (_hide.Slice.Use || _show.Slice.Use);
        public bool AnimatesSplit => _open.Split.Use || _stay.Split.Use || _close.Split.Use
            || _hideShowEnabled && (_hide.Split.Use || _show.Split.Use);
        public bool AnimatesScan => _open.Scan.Use || _stay.Scan.Use || _close.Scan.Use
            || _hideShowEnabled && (_hide.Scan.Use || _show.Scan.Use);
        public bool AnimatesDissolve => _open.Dissolve.Use || _stay.Dissolve.Use || _close.Dissolve.Use
            || _hideShowEnabled && (_hide.Dissolve.Use || _show.Dissolve.Use);
        public bool AnimatesSheen => _open.Sheen.Use || _stay.Sheen.Use || _close.Sheen.Use
            || _hideShowEnabled && (_hide.Sheen.Use || _show.Sheen.Use);
        public bool AnimatesFlash => _open.Flash.Use || _stay.Flash.Use || _close.Flash.Use
            || _hideShowEnabled && (_hide.Flash.Use || _show.Flash.Use);
        public bool UsesFx => _open.UsesFx || _stay.UsesFx || _close.UsesFx || _hideShowEnabled && (_hide.UsesFx || _show.UsesFx);
        public float WipeAngle { get => _wipeAngle; set => _wipeAngle = value; }
        public float WipeSoftness { get => Mathf.Clamp01(_wipeSoftness); set => _wipeSoftness = value; }
        public int GlitchBands { get => Mathf.Clamp(_glitchBands, 1, 48); set => _glitchBands = value; }
        public float GlitchFps { get => Mathf.Clamp(_glitchFps, 1f, 120f); set => _glitchFps = value; }
        public float DissolveCellSize { get => Mathf.Max(1f, _dissolveCellSize); set => _dissolveCellSize = value; }
        public float SheenAngle { get => _sheenAngle; set => _sheenAngle = value; }
        public float SheenWidth { get => Mathf.Clamp(_sheenWidth, 0.01f, 1f); set => _sheenWidth = value; }
        public float SheenIntensity { get => Mathf.Max(0f, _sheenIntensity); set => _sheenIntensity = value; }
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
                    && Mathf.Approximately(span.Wipe.From, span.Wipe.To)
                    && Static(span.Skew) && Static(span.Slice) && Static(span.Split) && Static(span.Scan)
                    && Static(span.Dissolve) && Static(span.Sheen) && Static(span.Flash)
                    && !(_stay.Slice.Use && (span.Slice.From != 0f || span.Slice.To != 0f));
            }
        }

        public UIAnimationStateSettings Get(UIAnimationState state)
        {
            switch (state)
            {
                case UIAnimationState.Open: return _open;
                case UIAnimationState.Stay: return _stay;
                case UIAnimationState.Hide: return _hide;
                case UIAnimationState.Show: return _show;
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
            if (state == UIAnimationState.Show)
            {
                UIAnimationSpan shown = GetSpan(UIAnimationState.Open);
                return new UIAnimationSpan(First(_show.Offset, shown.Offset.To), First(_show.Scale, shown.Scale.To),
                    First(_show.Rotation, shown.Rotation.To), First(_show.Color, shown.Color.To), First(_show.Wipe, shown.Wipe.To),
                    First(_show.Skew, shown.Skew.To), First(_show.Slice, shown.Slice.To), First(_show.Split, shown.Split.To),
                    First(_show.Scan, shown.Scan.To), First(_show.Dissolve, shown.Dissolve.To),
                    First(_show.Sheen, shown.Sheen.To), First(_show.Flash, shown.Flash.To));
            }
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
            UIAnimationStateSettings current = Get(state);
            return new UIAnimationSpan(offset, scale, rotation, color, wipe,
                FxSpan(_open.Skew, current.Skew, state), FxSpan(_open.Slice, current.Slice, state),
                FxSpan(_open.Split, current.Split, state), FxSpan(_open.Scan, current.Scan, state),
                FxSpan(_open.Dissolve, current.Dissolve, state), FxSpan(_open.Sheen, current.Sheen, state),
                FxSpan(_open.Flash, current.Flash, state));
        }

        private static bool Static(UIRange<float> span) => Mathf.Approximately(span.From, span.To);

        private static UIRange<float> FxSpan(UIFloatTrack open, UIFloatTrack current, UIAnimationState state)
        {
            UIRange<float> span = First(open, 0f);
            return state == UIAnimationState.Open ? span : Next(current, span.To);
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
            _wipeAngle = 0f;
            _wipeSoftness = 0f;
            _glitchBands = 12;
            _glitchFps = 12f;
            _dissolveCellSize = 16f;
            _sheenAngle = 25f;
            _sheenWidth = 0.2f;
            _sheenIntensity = 1f;

            switch (style)
            {
                case UIAnimationStyle.Pop:
                    Entrance(0.24f, Vector2.zero, new Vector2(0.96f, 0.96f), 0f);
                    ScalePath(new Vector2(0.96f, 0.96f),
                        (0f, 0.96f), (0.08f, 0.954f), (0.58f, 1.018f), (0.96f, 1f), (1f, 1f));
                    break;
                case UIAnimationStyle.Fade:
                    Entrance(0.22f, Vector2.zero, new Vector2(0.975f, 0.975f), 0f);
                    _open.Color.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutSine, 0f, 0.92f));
                    _close.Color.Curve.Ease = UIEase.EaseInCubic;
                    break;
                case UIAnimationStyle.BouncePop:
                    Entrance(0.40f, new Vector2(0f, -10f), new Vector2(0.88f, 0.88f), 0f);
                    ScalePath(new Vector2(0.88f, 0.88f), (0f, 0.88f), (0.10f, 0.86f),
                        (0.48f, 1.055f), (0.70f, 0.992f), (0.84f, 1.008f), (1f, 1f));
                    break;
                case UIAnimationStyle.ExpandHorizontal:
                    Entrance(0.30f, Vector2.zero, new Vector2(0.04f, 0.94f), 0f);
                    _open.Scale.Curve.SetCustom(UIMotionCurves.Path((0f, 0f, UIEase.Linear),
                        (0.65f, 1.0125f, UIEase.EaseOutQuart), (0.86f, 1f, UIEase.EaseInOutSine), (1f, 1f, UIEase.Linear)));
                    _open.Scale.SetYCurve(UIMotionCurves.Path((0f, 0f, UIEase.Linear),
                        (0.22f, 0f, UIEase.Linear), (0.84f, 1.05f, UIEase.EaseOutCubic), (1f, 1f, UIEase.EaseInOutSine)));
                    break;
                case UIAnimationStyle.ExpandVertical:
                    Entrance(0.32f, Vector2.zero, new Vector2(0.94f, 0.04f), 0f);
                    _open.Scale.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, 0f, 0.48f));
                    _open.Scale.SetYCurve(UIMotionCurves.Path((0f, 0f, UIEase.Linear),
                        (0.10f, 0f, UIEase.Linear), (0.78f, 1.015625f, UIEase.EaseOutQuart), (1f, 1f, UIEase.EaseInOutSine)));
                    break;
                case UIAnimationStyle.PersonaDrift:
                    Entrance(0.44f, new Vector2(-44f, -10f), new Vector2(0.985f, 1.012f), 0f);
                    _open.Offset.SetYCurve(UIMotionCurves.Window(UIEase.EaseOutCubic, 0.10f, 0.80f));
                    SetWipe(UIWipeDirection.LeftToRight, -18f, 0.22f, 0f, 0.92f);
                    _open.Color.Off();
                    SetFloatStay(12f, 1.5f);
                    SetSheenStay(12f, 0.95f, 0.28f);
                    break;
                case UIAnimationStyle.PersonaSlash:
                    Entrance(0.30f, new Vector2(-42f, -8f), Vector2.one, 0f);
                    _open.Skew.Set(-14f, 0f, UIEase.Linear);
                    _open.Skew.Curve.SetCustom(UIMotionCurves.Path((0f, 0f, UIEase.Linear),
                        (0.10f, -2f / 14f, UIEase.EaseOutQuad), (0.72f, 1f + 1.8f / 14f, UIEase.EaseOutQuart),
                        (1f, 1f, UIEase.EaseInOutSine)));
                    _close.Skew.Set(0f, -8f, UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.PersonaCutIn:
                    Entrance(0.20f, new Vector2(-112f, -24f), Vector2.one, 0f);
                    SetWipe(UIWipeDirection.LeftToRight, -32f, 0.035f, 0f, 0.60f);
                    _open.Color.Off();
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuint, 0f, 0.66f));
                    _open.Skew.Set(-24f, 0f, UIEase.Linear);
                    _open.Skew.Curve.SetCustom(UIMotionCurves.Settle(1f + 2f / 24f, 0.76f, UIEase.EaseOutQuart, UIEase.EaseInOutSine));
                    Burst(_open.Flash, 0.85f, 0.02f, 0.10f, 0.42f);
                    Burst(_open.Split, 0.12f, 0.56f, 0.66f, 0.88f);
                    _close.Offset.Set(Vector2.zero, new Vector2(88f, 22f), UIEase.EaseInCubic);
                    _close.Skew.Set(0f, 22f, UIEase.EaseInCubic);
                    // 같은 사선 경계의 노출량을 줄이면 Open과 반대 방향으로 베어 나간다.
                    _close.Wipe.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseInCubic, 0.08f, 0.92f));
                    break;
                case UIAnimationStyle.PersonaList:
                    Entrance(0.20f, new Vector2(-18f, -5f), Vector2.one, -3f);
                    _close.Offset.Set(Vector2.zero, new Vector2(16f, 4f), UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.StarRailSlide:
                    Entrance(0.26f, new Vector2(36f, 0f), Vector2.one, 0f);
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, 0f, 0.78f));
                    SetSheen(0.18f, 0.94f, 0.045f, 0.45f);
                    break;
                case UIAnimationStyle.StarRailWipe:
                    Entrance(0.34f, Vector2.zero, Vector2.one, 0f);
                    SetWipe(UIWipeDirection.LeftToRight, 0f, 0.16f, 0.02f, 0.88f);
                    SetSheen(0.02f, 0.88f, 0.08f, 0.6f, UIEase.EaseOutCubic);
                    _sheenAngle = 0f;
                    _open.Color.Off();
                    _close.Color.Off();
                    break;
                case UIAnimationStyle.StarRailHologram:
                    Entrance(0.32f, Vector2.zero, new Vector2(1f, 0.96f), 0f);
                    _open.Color.Curve.SetCustom(UIMotionCurves.Path((0f, 0f, UIEase.Linear),
                        (0.06f, 1f, UIEase.EaseOutQuad), (0.12f, 0.65f, UIEase.EaseInOutSine),
                        (0.19f, 1f, UIEase.EaseOutQuad), (0.24f, 0.9f, UIEase.EaseInOutSine),
                        (0.34f, 1f, UIEase.EaseOutQuad), (1f, 1f, UIEase.Linear)));
                    Decay(_open.Scan, 0.5f, 0.06f, 0.74f);
                    Decay(_open.Split, 0.08f, 0f, 0.48f);
                    SetStayPeriod(5.8f);
                    _stay.Scan.Set(0f, 0.055f, UIEase.EaseInOutSine);
                    _close.Scale.Set((Vector2)_baseScale, Vector2.Scale(_baseScale, new Vector2(1f, 0.04f)), UIEase.EaseInCubic);
                    _close.Dissolve.Set(0f, 1f, UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.StarRailWindow:
                    Entrance(0.24f, Vector2.zero, new Vector2(0.985f, 0.985f), 0f);
                    _open.Color.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuad, 0f, 0.58f));
                    _open.Scale.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutSine, 0.04f, 1f));
                    break;
                case UIAnimationStyle.ZenlessTv:
                    Entrance(0.30f, Vector2.zero, new Vector2(0.015f, 0.012f), 0f);
                    _open.Color.Off();
                    _open.Scale.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuart, 0f, 0.28f));
                    _open.Scale.SetYCurve(UIMotionCurves.Path((0f, 0f, UIEase.Linear),
                        (0.25f, 0f, UIEase.Linear), (0.76f, 1.015f, UIEase.EaseOutQuart), (1f, 1f, UIEase.EaseInOutSine)));
                    Decay(_open.Flash, 0.9f, 0f, 0.28f);
                    Decay(_open.Scan, 0.7f, 0.08f, 0.85f);
                    _close.Scale.Set((Vector2)_baseScale, Vector2.Scale(_baseScale, new Vector2(0.015f, 0.012f)), UIEase.Linear);
                    _close.Scale.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseInCubic, 0.58f, 1f));
                    _close.Scale.SetYCurve(UIMotionCurves.Window(UIEase.EaseInCubic, 0f, 0.65f));
                    _close.Color.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseInCubic, 0.86f, 1f));
                    break;
                case UIAnimationStyle.ZenlessSnap:
                    Entrance(0.25f, new Vector2(0f, -14f), new Vector2(0.88f, 0.76f), 0f);
                    _open.StepsPerSecond = 12;
                    // 공통 페이드의 중간 키가 12fps 사이에 끼어들지 않게 한다.
                    _open.Color.Curve.SetCustom(UIMotionCurves.Steps((0f, 0f), (1f / 3f, 1f), (1f, 1f)));
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Steps((0f, 0f), (1f / 3f, 0.82f), (2f / 3f, 1f), (1f, 1f)));
                    _open.Scale.Curve.SetCustom(UIMotionCurves.Steps((0f, 0f), (1f / 3f, 0.85f), (2f / 3f, 1.18f), (1f, 1f)));
                    _open.Scale.SetYCurve(UIMotionCurves.Steps((0f, 0f), (1f / 3f, 0.72f), (2f / 3f, 1.18f), (1f, 1f)));
                    _open.Split.Set(0.16f, 0f, UIEase.Linear);
                    _open.Split.Curve.SetCustom(UIMotionCurves.Steps((0f, 1f), (2f / 3f, 0f), (1f, 1f)));
                    break;
                case UIAnimationStyle.ZenlessGlitch:
                    Entrance(0.25f, Vector2.zero, Vector2.one, 0f);
                    _open.StepsPerSecond = _close.StepsPerSecond = 12;
                    _open.Color.Off();
                    _open.Color.Curve.Ease = UIEase.Linear;
                    _close.Time = 2f / 12f;
                    _close.Color.Curve.SetCustom(UIMotionCurves.Steps((0f, 0f), (0.5f, 0.125f), (1f, 1f)));
                    _glitchBands = 9;
                    _open.Slice.Set(0.85f, 0f, UIEase.Linear);
                    _open.Slice.Curve.SetCustom(UIMotionCurves.Steps((0f, 0f), (1f / 3f, 0.42f), (2f / 3f, 0.9f), (1f, 1f)));
                    _open.Split.Set(0.4f, 0f, UIEase.Linear);
                    _open.Split.Curve.SetCustom(UIMotionCurves.Steps((0f, 0f), (1f / 3f, 0.6f), (2f / 3f, 1f), (1f, 1f)));
                    _close.Slice.Set(0f, 0.8f, UIEase.EaseInCubic);
                    _close.Dissolve.Set(0f, 1f, UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.ZenlessSticker:
                    Entrance(0.30f, new Vector2(0f, 22f), new Vector2(1.22f, 1.22f), -10f);
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseInCubic, 0f, 0.32f));
                    ScalePath(new Vector2(1.22f, 1.22f), (0f, 1.22f), (0.32f, 0.94f), (0.58f, 1.025f), (1f, 1f));
                    _open.Rotation.Curve.SetCustom(UIMotionCurves.Path((0f, 0f, UIEase.Linear),
                        (0.12f, -0.04f, UIEase.EaseOutQuad), (0.72f, 1.08f, UIEase.EaseOutQuart),
                        (1f, 1f, UIEase.EaseInOutSine)));
                    Burst(_open.Flash, 0.7f, 0.24f, 0.32f, 0.40f);
                    break;
                case UIAnimationStyle.JrpgCommand:
                    Entrance(0.30f, Vector2.zero, new Vector2(1f, 0.05f), 0f);
                    _open.StepsPerSecond = _close.StepsPerSecond = 10;
                    _open.Color.Off();
                    _open.Color.Curve.Ease = UIEase.Linear;
                    _open.Scale.Curve.Ease = UIEase.Linear;
                    _open.Scale.SetYCurve(UIMotionCurves.Steps((0f, 0f), (1f / 3f, 0.25f), (2f / 3f, 0.74f), (1f, 1f)));
                    _close.Time = 0.20f;
                    _close.Scale.Set((Vector2)_baseScale, Vector2.Scale(_baseScale, new Vector2(1f, 0.02f)), UIEase.EaseInCubic);
                    _close.Color.Curve.SetCustom(UIMotionCurves.Steps((0f, 0f), (0.5f, 0.125f), (1f, 1f)));
                    break;
                case UIAnimationStyle.JrpgScroll:
                    Entrance(0.42f, Vector2.zero, new Vector2(0.995f, 0.97f), 0f);
                    SetWipe(UIWipeDirection.BottomToTop, 0f, 0.18f, 0.04f, 0.82f);
                    _open.Color.Off();
                    _open.Scale.SetYCurve(UIMotionCurves.Path((0f, 0f, UIEase.Linear),
                        (0.68f, 1.2f, UIEase.EaseOutCubic), (1f, 1f, UIEase.EaseInOutSine)));
                    break;
                case UIAnimationStyle.JrpgSummon:
                    Entrance(0.46f, Vector2.zero, new Vector2(0.035f, 0.98f), 0f);
                    _open.Scale.Curve.SetCustom(UIMotionCurves.Path((0f, 0f, UIEase.Linear),
                        (0.08f, 0f, UIEase.Linear), (0.72f, 1.02f, UIEase.EaseOutQuart), (1f, 1f, UIEase.EaseInOutSine)));
                    _open.Scale.SetYCurve(UIMotionCurves.Window(UIEase.EaseOutCubic, 0.12f, 0.90f));
                    Burst(_open.Flash, 0.75f, 0.08f, 0.14f, 0.24f);
                    SetSheen(0.35f, 0.94f, 0.14f, 0.8f);
                    _close.Scale.Off();
                    _close.Dissolve.Set(0f, 1f, UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.JrpgVictory:
                    Entrance(0.34f, new Vector2(-124f, 0f), new Vector2(0.98f, 1.025f), 0f);
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Path((0f, 0f, UIEase.Linear),
                        (0.06f, -0.025f, UIEase.EaseOutQuad), (0.63f, 1.035f, UIEase.EaseOutQuint),
                        (0.78f, 1f, UIEase.EaseInOutSine), (1f, 1f, UIEase.Linear)));
                    SetSheen(0.55f, 0.98f, 0.10f, 0.9f);
                    SetSheenStay(9f, 0.93f, 0.9f);
                    _close.Offset.Set(Vector2.zero, new Vector2(100f, 0f), UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.JrpgDialogue:
                    Entrance(0.18f, new Vector2(0f, -10f), Vector2.one, 0f);
                    _open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, 0f, 0.80f));
                    _close.Offset.Set(Vector2.zero, new Vector2(0f, -8f), UIEase.EaseInCubic);
                    break;
                case UIAnimationStyle.JrpgItem:
                    Entrance(0.36f, new Vector2(0f, -18f), new Vector2(0.86f, 0.86f), 0f);
                    ScalePath(new Vector2(0.86f, 0.86f), (0f, 0.86f), (0.08f, 0.85f), (0.70f, 1.04f), (1f, 1f));
                    Burst(_open.Flash, 0.55f, 0.06f, 0.14f, 0.28f);
                    SetSheen(0.40f, 0.95f, 0.08f, 0.75f);
                    SetSheenStay(10f, 0.94f, 0.75f);
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(style));
            }
            SetStatePreset(UIAnimationPresetCatalog.Name(style), UIAnimationPresetCatalog.Description(style));
            if (_hideShowEnabled) GenerateHideShow();
        }

        private void Entrance(float time, Vector2 offset, Vector2 factors, float tilt)
        {
            Vector2 full = _baseScale;
            Color hidden = _baseColor;
            hidden.a = 0f;
            SetTimes(time, time * 0.60f);
            _open.Color.Set(hidden, _baseColor, UIEase.Linear);
            _open.Color.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuad, 0f, 0.30f));
            _close.Color.Set(_baseColor, hidden, UIEase.Linear);
            _close.Color.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseInCubic, 0.25f, 1f));
            if (offset != Vector2.zero)
            {
                _open.Offset.Set(offset, Vector2.zero, UIEase.Linear);
                _open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuart, 0f, 0.70f));
                _close.Offset.Set(Vector2.zero, offset * 0.65f, UIEase.EaseInCubic);
            }
            if (factors != Vector2.one)
            {
                _open.Scale.Set(Vector2.Scale(full, factors), full, UIEase.Linear);
                _open.Scale.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, 0.04f, 1f));
                _close.Scale.Set(full, full * 0.985f, UIEase.EaseInCubic);
            }
            if (tilt != 0f)
            {
                _open.Rotation.Set(tilt, 0f, UIEase.Linear);
                _open.Rotation.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, 0.08f, 1f));
                _close.Rotation.Set(0f, tilt * 0.5f, UIEase.EaseInCubic);
            }
        }

        private void ScalePath(Vector2 factors, params (float t, float factor)[] points)
        {
            AnimationCurve Axis(float start)
            {
                var path = new (float t, float v, UIEase ease)[points.Length];
                for (int i = 0; i < points.Length; i++)
                    path[i] = (points[i].t, (points[i].factor - start) / (1f - start),
                        i == 1 ? (start > 1f ? UIEase.EaseInCubic : UIEase.EaseOutQuad)
                            : points[i].factor == 1f ? UIEase.EaseInOutSine : UIEase.EaseOutCubic);
                return UIMotionCurves.Path(path);
            }
            _open.Scale.Curve.SetCustom(Axis(factors.x));
            _open.Scale.SetYCurve(Axis(factors.y));
        }

        private static void Decay(UIFloatTrack track, float strength, float start, float end)
        {
            track.Set(strength, 0f, UIEase.Linear);
            track.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, start, end));
        }

        private static void Burst(UIFloatTrack track, float strength, float start, float peak, float end)
        {
            track.Set(strength, 0f, UIEase.Linear);
            track.Curve.SetCustom(UIMotionCurves.Path((0f, 1f, UIEase.Linear), (start, 1f, UIEase.Linear),
                (peak, 0f, UIEase.EaseOutQuad), (end, 1f, UIEase.EaseOutCubic), (1f, 1f, UIEase.Linear)));
        }

        private void SetSheen(float start, float end, float width, float intensity, UIEase ease = UIEase.Linear)
        {
            _sheenWidth = width;
            _sheenIntensity = intensity;
            _open.Sheen.Set(1f, 0f, UIEase.Linear);
            _open.Sheen.Curve.SetCustom(UIMotionCurves.Sweep(ease, start, end));
        }

        private void SetSheenStay(float period, float start, float intensity)
        {
            SetStayPeriod(period);
            _sheenIntensity = intensity;
            _stay.Sheen.Set(0f, 1f, UIEase.Linear);
            // 왕복의 전환점 근처에서만 화면 안을 지나고 나머지 시간은 쉰다.
            _stay.Sheen.Curve.SetCustom(UIMotionCurves.Window(UIEase.Linear, start, 1f));
        }

        private void SetWipe(UIWipeDirection direction, float angle, float softness, float start, float end)
        {
            _wipeDirection = direction;
            _wipeAngle = angle;
            _wipeSoftness = softness;
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
            Mirror(_open.Skew, _close.Skew);
            Mirror(_open.Slice, _close.Slice);
            Mirror(_open.Split, _close.Split);
            Mirror(_open.Scan, _close.Scan);
            Mirror(_open.Dissolve, _close.Dissolve);
            Mirror(_open.Sheen, _close.Sheen);
            Mirror(_open.Flash, _close.Flash);
            if (_open.Color.Use)
            {
                _close.Color.Set(_open.Color.To, _open.Color.From, UIEasing.Mirror(_open.Color.Curve.Ease));
                _close.Color.Curve.CopyFrom(_open.Color.Curve, true);
            }
            else
            {
                _close.Color.Off();
            }
            if (_hideShowEnabled) GenerateHideShow();
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
            _hide.Scale.Rescale(ratio);
            _show.Scale.Rescale(ratio);
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
