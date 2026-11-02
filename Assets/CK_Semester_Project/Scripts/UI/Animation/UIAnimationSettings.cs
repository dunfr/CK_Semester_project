using System;
using UnityEngine;

namespace CK.SemesterProject.UIAnimation
{
    public enum UIAnimationState
    {
        Open,
        Stay,
        Close
    }

    public enum UIPointerEffect
    {
        Hover,
        Exit,
        Pressed
    }

    // 어떤 입력 반응을 만들지 고르는 값이다.
    [Flags]
    public enum UIPointerEffectMask
    {
        None = 0,
        Hover = 1,
        Exit = 2,
        Pressed = 4,
        All = Hover | Exit | Pressed
    }

    public enum UIWipeDirection
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    [Serializable]
    public sealed class UIEaseCurve
    {
        [SerializeField] private UIEase _ease = UIEase.EaseOutCubic;
        [SerializeField] private AnimationCurve _custom = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public UIEase Ease
        {
            get => _ease;
            set => _ease = value;
        }

        public AnimationCurve Custom => _custom;

        public void SetCustom(AnimationCurve curve)
        {
            if (curve == null) throw new ArgumentNullException(nameof(curve));
            _custom = new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
            _ease = UIEase.Custom;
        }

        public void CopyFrom(UIEaseCurve source, bool mirror = false)
        {
            if (source.Ease == UIEase.Custom)
                SetCustom(mirror ? UIMotionCurves.Reverse(source.Custom) : source.Custom);
            else
                _ease = mirror ? UIEasing.Mirror(source.Ease) : source.Ease;
        }

        public float Evaluate(float t)
        {
            return _ease == UIEase.Custom ? _custom.Evaluate(Mathf.Clamp01(t)) : UIEasing.Evaluate(_ease, t);
        }
    }

    // 위치와 크기처럼 X, Y를 함께 움직이는 값이다.
    [Serializable]
    public sealed class UIVector2Track
    {
        [SerializeField] private bool _use;
        [SerializeField] private Vector2 _from;
        [SerializeField] private Vector2 _to;
        [SerializeField] private UIEaseCurve _curve = new UIEaseCurve();
        [SerializeField] private bool _splitY;
        [SerializeField] private UIEaseCurve _curveY = new UIEaseCurve();

        public UIVector2Track()
        {
        }

        public UIVector2Track(Vector2 rest)
        {
            _from = rest;
            _to = rest;
        }

        public bool Use => _use;
        public Vector2 From => _from;
        public Vector2 To => _to;
        public UIEaseCurve Curve => _curve;
        public bool SplitY => _splitY;
        public UIEaseCurve CurveY => _splitY ? _curveY : _curve;

        public void SetYCurve(AnimationCurve curve)
        {
            _curveY.SetCustom(curve);
            _splitY = true;
        }

        public Vector2 Evaluate(Vector2 from, Vector2 to, float t)
        {
            return new Vector2(Mathf.LerpUnclamped(from.x, to.x, _curve.Evaluate(t)),
                Mathf.LerpUnclamped(from.y, to.y, CurveY.Evaluate(t)));
        }

        public void Set(Vector2 from, Vector2 to, UIEase ease)
        {
            _use = true;
            _from = from;
            _to = to;
            _curve.Ease = ease;
            _splitY = false;
        }

        public void Off()
        {
            _use = false;
            _splitY = false;
            _curveY = new UIEaseCurve();
        }

        public void Rescale(Vector2 ratio)
        {
            _from = Vector2.Scale(_from, ratio);
            _to = Vector2.Scale(_to, ratio);
        }
    }

    // 회전과 와이프처럼 값이 하나인 트랙이다.
    [Serializable]
    public sealed class UIFloatTrack
    {
        [SerializeField] private bool _use;
        [SerializeField] private float _from;
        [SerializeField] private float _to;
        [SerializeField] private UIEaseCurve _curve = new UIEaseCurve();

        public UIFloatTrack()
        {
        }

        public UIFloatTrack(float rest)
        {
            _from = rest;
            _to = rest;
        }

        public bool Use => _use;
        public float From => _from;
        public float To => _to;
        public UIEaseCurve Curve => _curve;

        public void Set(float from, float to, UIEase ease)
        {
            _use = true;
            _from = from;
            _to = to;
            _curve.Ease = ease;
        }

        public void Off()
        {
            _use = false;
        }
    }

    [Serializable]
    public sealed class UIColorTrack
    {
        [SerializeField] private bool _use;
        [SerializeField] private Color _from = Color.white;
        [SerializeField] private Color _to = Color.white;
        [SerializeField] private UIEaseCurve _curve = new UIEaseCurve();

        public bool Use => _use;
        public Color From => _from;
        public Color To => _to;
        public UIEaseCurve Curve => _curve;

        public void Set(Color from, Color to, UIEase ease)
        {
            _use = true;
            _from = from;
            _to = to;
            _curve.Ease = ease;
        }

        public void Off()
        {
            _use = false;
        }
    }

    [Serializable]
    public sealed class UIAnimationStateSettings
    {
        private const float MinimumValue = 0.01f;

        [SerializeField, Min(MinimumValue)] private float _time = 0.25f;
        [SerializeField, Min(MinimumValue)] private float _speed = 1f;
        [SerializeField, Min(0)] private int _stepsPerSecond;
        [SerializeField] private UIVector2Track _offset = new UIVector2Track(Vector2.zero);
        [SerializeField] private UIVector2Track _scale = new UIVector2Track(Vector2.one);
        [SerializeField] private UIFloatTrack _rotation = new UIFloatTrack(0f);
        [SerializeField] private UIColorTrack _color = new UIColorTrack();
        [SerializeField] private UIFloatTrack _wipe = new UIFloatTrack(1f);
        [SerializeField] private UIFloatTrack _skew = new UIFloatTrack(0f);
        [SerializeField] private UIFloatTrack _slice = new UIFloatTrack(0f);
        [SerializeField] private UIFloatTrack _split = new UIFloatTrack(0f);
        [SerializeField] private UIFloatTrack _scan = new UIFloatTrack(0f);
        [SerializeField] private UIFloatTrack _dissolve = new UIFloatTrack(0f);
        [SerializeField] private UIFloatTrack _sheen = new UIFloatTrack(0f);
        [SerializeField] private UIFloatTrack _flash = new UIFloatTrack(0f);
        [SerializeField] private bool _keepEnabled = true;
        [SerializeField] private bool _autoClose;
        [SerializeField] private AudioClip _sound;

        public float Time
        {
            get => Mathf.Max(MinimumValue, _time);
            set => _time = value;
        }

        public float Speed
        {
            get => Mathf.Max(MinimumValue, _speed);
            set => _speed = value;
        }

        public float Duration => Time / Speed;

        // 0이면 부드럽게, 값이 있으면 초당 그 횟수로 끊어 재생한다. 애니메이션 작화 같은 손맛을 낸다.
        public int StepsPerSecond
        {
            get => Mathf.Max(0, _stepsPerSecond);
            set => _stepsPerSecond = value;
        }

        public UIVector2Track Offset => _offset;
        public UIVector2Track Scale => _scale;
        public UIFloatTrack Rotation => _rotation;
        public UIColorTrack Color => _color;
        public UIFloatTrack Wipe => _wipe;
        public UIFloatTrack Skew => _skew;
        public UIFloatTrack Slice => _slice;
        public UIFloatTrack Split => _split;
        public UIFloatTrack Scan => _scan;
        public UIFloatTrack Dissolve => _dissolve;
        public UIFloatTrack Sheen => _sheen;
        public UIFloatTrack Flash => _flash;
        public bool UsesFx => _wipe.Use || UsesExtraFx;
        public bool UsesExtraFx => _skew.Use || _slice.Use || _split.Use || _scan.Use || _dissolve.Use || _sheen.Use || _flash.Use;
        public AudioClip Sound => _sound;

        // Enable: 끄면 이 상태가 끝날 때 오브젝트가 꺼진다.
        public bool KeepEnabled
        {
            get => _keepEnabled;
            set => _keepEnabled = value;
        }

        // Stay 전용. Time이 지나면 스스로 Close로 넘어간다.
        public bool AutoClose
        {
            get => _autoClose;
            set => _autoClose = value;
        }

        public void ClearMotion()
        {
            _stepsPerSecond = 0;
            _offset.Off();
            _scale.Off();
            _rotation.Off();
            _color.Off();
            _wipe.Off();
            _skew.Off();
            _slice.Off();
            _split.Off();
            _scan.Off();
            _dissolve.Off();
            _sheen.Off();
            _flash.Off();
        }
    }

    [Serializable]
    public sealed class UIPointerEffectSettings
    {
        private const float MinimumValue = 0.01f;

        [SerializeField] private bool _enabled = true;
        [SerializeField, Min(MinimumValue)] private float _time = 0.12f;
        [SerializeField, Min(MinimumValue)] private float _speed = 1f;
        [SerializeField] private UIVector2Track _offset = new UIVector2Track(Vector2.zero);
        [SerializeField] private UIVector2Track _scale = new UIVector2Track(Vector2.one);
        [SerializeField] private UIFloatTrack _rotation = new UIFloatTrack(0f);
        [SerializeField] private UIColorTrack _color = new UIColorTrack();
        [SerializeField, Min(0f)] private float _brightness = 1f;
        [SerializeField] private Sprite _sprite;
        [SerializeField] private AudioClip _sound;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public float Time
        {
            get => Mathf.Max(MinimumValue, _time);
            set => _time = value;
        }

        public float Speed
        {
            get => Mathf.Max(MinimumValue, _speed);
            set => _speed = value;
        }

        public float Duration => Time / Speed;
        public UIVector2Track Offset => _offset;
        public UIVector2Track Scale => _scale;
        public UIFloatTrack Rotation => _rotation;
        public UIColorTrack Color => _color;
        public AudioClip Sound => _sound;

        // 하이라이트 색과 함께 곱하는 밝기다. 1보다 크면 원래보다 밝아진다.
        public float Brightness
        {
            get => Mathf.Max(0f, _brightness);
            set => _brightness = value;
        }

        // 이 이벤트에서 보여 줄 하이라이트 이미지다. 움직임이 아니라서 ClearMotion으로 지우지 않는다.
        public Sprite Sprite
        {
            get => _sprite;
            set => _sprite = value;
        }

        public void ClearMotion()
        {
            _offset.Off();
            _scale.Off();
            _rotation.Off();
            _color.Off();
            _brightness = 1f;
        }
    }

    public readonly struct UIRange<T>
    {
        public UIRange(T from, T to)
        {
            From = from;
            To = to;
        }

        public T From { get; }
        public T To { get; }
    }

    // 한 상태가 시작하고 끝나는 실제 값이다.
    public readonly struct UIAnimationSpan
    {
        public UIAnimationSpan(UIRange<Vector2> offset, UIRange<Vector2> scale, UIRange<float> rotation, UIRange<Color> color, UIRange<float> wipe,
            UIRange<float> skew = default, UIRange<float> slice = default, UIRange<float> split = default,
            UIRange<float> scan = default, UIRange<float> dissolve = default, UIRange<float> sheen = default, UIRange<float> flash = default)
        {
            Offset = offset;
            Scale = scale;
            Rotation = rotation;
            Color = color;
            Wipe = wipe;
            Skew = skew;
            Slice = slice;
            Split = split;
            Scan = scan;
            Dissolve = dissolve;
            Sheen = sheen;
            Flash = flash;
        }

        public UIRange<Vector2> Offset { get; }
        public UIRange<Vector2> Scale { get; }
        public UIRange<float> Rotation { get; }
        public UIRange<Color> Color { get; }
        public UIRange<float> Wipe { get; }
        public UIRange<float> Skew { get; }
        public UIRange<float> Slice { get; }
        public UIRange<float> Split { get; }
        public UIRange<float> Scan { get; }
        public UIRange<float> Dissolve { get; }
        public UIRange<float> Sheen { get; }
        public UIRange<float> Flash { get; }
    }
}
