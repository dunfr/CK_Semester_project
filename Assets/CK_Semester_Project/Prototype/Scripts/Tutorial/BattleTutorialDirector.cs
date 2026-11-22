using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    // 전투 튜토리얼(Tutorial_DT). 전투 진입 횟수(tutorial_condition)에 맞는 안내 팝업을 띄우고 기능을 해금한다.
    // TT00: 진입 시 텍스트 1~2 → 몬스터 상세보기(대상 지정) 시 텍스트 3~4 → 약점 속성 스킬 하이라이트
    // TT01: 두 번째 전투 진입 시 텍스트 1~3 / TT02: 세 번째 전투 진입 시 텍스트 1~5
    // 팝업이 열린 동안은 전투 시간을 멈추고 Space(또는 →, NEXT)로 다음 장, ←로 이전 장을 본다.
    public sealed class BattleTutorialDirector : MonoBehaviour
    {
        public const string MonsterInfo = "MI";
        public const string WeaknessChain = "WC";
        public const string MemoryLoss = "ML";
        public const string Overheat = "OH";
        public const string SpecialAttack = "SA";

        [Serializable]
        public sealed class PageTitle
        {
            public string TutorialId;
            [Min(0), Tooltip("이 제목을 쓰기 시작하는 페이지(0부터)")] public int FromPage;
            public string Title;

            public PageTitle(string id, int fromPage, string title)
            {
                TutorialId = id;
                FromPage = fromPage;
                Title = title;
            }
        }

        // 안내 한 장이 가리킬 HUD와 상자 옆 꼬리표 문구.
        [Serializable]
        public sealed class PageFocus
        {
            public string TutorialId;
            [Min(0), Tooltip("이 HUD를 가리킬 페이지(0부터)")] public int Page;
            [Tooltip("노란 상자로 감쌀 HUD. 비우거나 꺼져 있으면 상자 없이 안내 카드만 기본 위치에 띄운다.")]
            public RectTransform Target;
            [Tooltip("상자 위 노란 꼬리표 문구. 비우면 꼬리표를 숨긴다.")]
            public string Tag;
        }

        [Header("Data")]
        [SerializeField, Tooltip("Tutorial_DT.csv (기획 xlsx와 같은 열 순서)")]
        private TextAsset _table;
        [SerializeField] private TutorialBattleController _battle;
        [SerializeField, Tooltip("팝업이 열린 동안 끌 전투 키 입력(PrototypeHudPresenter 등)")]
        private Behaviour[] _blockedInput = Array.Empty<Behaviour>();
        [SerializeField, Tooltip("페이지별 제목 박스 문구")]
        private PageTitle[] _titles =
        {
            new PageTitle("TT00", 0, "전투 순서 가이드"),
            new PageTitle("TT00", 2, "약점 연쇄 가이드"),
            new PageTitle("TT01", 0, "메모리 가이드"),
            new PageTitle("TT02", 0, "폭주 게이지 가이드"),
            new PageTitle("TT02", 3, "특수 공격 가이드"),
        };
        [SerializeField, Min(0), Tooltip("MI(몬스터 상세보기)를 해금하는 튜토리얼에서 상세보기 진입 시 이어 보여 줄 첫 페이지")]
        private int _detailPage = 2;
        [SerializeField, Min(0f), Tooltip("전투 진입 후 카메라가 자리를 잡고 팝업을 띄우기까지의 시간(초)")]
        private float _openDelay = 0.9f;
        [SerializeField, Tooltip("팝업이 열린 동안 Time.timeScale = 0")]
        private bool _pauseBattle = true;
        [SerializeField, Tooltip("체크 시 Play마다 처음부터 다시 진행(테스트용). 끄면 한 세션에서 한 번만 나온다.")]
        private bool _resetOnPlay = true;

        [Header("View")]
        [SerializeField] private GameObject _popup;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private TMP_Text _pager;
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private Button _nextLabel;

        [Header("Weakness Highlight")]
        [SerializeField, Tooltip("HUD 스킬 버튼. 순서는 TutorialBattleController.Skills와 같다.")]
        private RectTransform[] _skillButtons = Array.Empty<RectTransform>();
        [SerializeField, Tooltip("기존 노란 테두리와 문구를 사용한다.")]
        private bool _useLegacyWeaknessHighlight = false;
        [SerializeField] private RectTransform _highlight;
        [SerializeField] private CanvasGroup _highlightGroup;
        [SerializeField, Tooltip("테두리 옆 \"약점 속성 공격!\" 꼬리표. 약점 스킬을 이미 고르고 있으면 숨긴다.")]
        private GameObject _highlightTag;
        [SerializeField, Tooltip("지금 고른 스킬을 알기 위한 HUD")]
        private PrototypeHudPresenter _hud;
        [SerializeField, Min(0f), Tooltip("네온 테두리를 버튼 바깥으로 넓히는 여백(px)")]
        private float _highlightPadding = 22f;
        [SerializeField, Min(0f), Tooltip("버튼 이미지 오른쪽의 투명한 부분만큼 테두리 오른쪽을 줄이는 양(px)")]
        private float _highlightRightTrim = 150f;

        [Header("Coach Mark")]
        [SerializeField, Tooltip("페이지별로 가리킬 HUD. 목록에 없는 페이지는 안내 카드만 띄운다.")]
        private PageFocus[] _focuses = Array.Empty<PageFocus>();
        [SerializeField, Tooltip("안내 대상을 감싸는 노란 상자")]
        private RectTransform _focusBox;
        [SerializeField, Tooltip("상자 위 노란 꼬리표(부모 오브젝트째 켜고 끈다)")]
        private TMP_Text _focusTag;
        [SerializeField, Tooltip("제목·본문·페이지 표시가 든 안내 카드")]
        private RectTransform _callout;
        [SerializeField, Tooltip("안내 전체 투명도(나타날 때 페이드)")]
        private CanvasGroup _coachGroup;
        [SerializeField, Min(0f), Tooltip("상자와 대상 사이 여백(px)")]
        private float _focusPadding = 10f;
        [SerializeField, Min(0f), Tooltip("상자와 안내 카드 사이 간격(px)")]
        private float _calloutGap = 28f;
        [SerializeField, Tooltip("가리킬 대상이 없을 때 안내 카드 위치(화면 중앙 기준 px)")]
        private Vector2 _calloutFallbackPosition = new Vector2(0f, 250f);
        [SerializeField, Min(0.01f), Tooltip("페이지가 바뀔 때 상자가 조여 들고 카드가 미끄러져 들어오는 시간(초)")]
        private float _revealSeconds = 0.25f;

        private const float FocusStartScale = 1.18f;
        private const float CalloutSlide = 36f;
        private readonly Vector3[] _corners = new Vector3[4];
        private float _pageShownAt;

        [Header("Weakness Spotlight")]
        [SerializeField] private PrototypeHudPresenter _spotlightHud;
        [SerializeField] private string _spotlightSkillId = "SK01";
        [SerializeField, Tooltip("비워 두면 안내 대상 스킬 버튼을 따라간다.")]
        private RectTransform _spotlightRegion;
        [SerializeField] private RectTransform _investmentSpotlightRegion;
        [SerializeField] private RectTransform _executeSpotlightRegion;
        [SerializeField, Tooltip("강조 영역의 가로·세로 여백(튜토리얼 캔버스 단위).")]
        private Vector2 _spotlightPadding = new Vector2(10f, 10f);
        [SerializeField, Tooltip("강조 영역의 회전 방향을 기준으로 한 가로·세로 위치 조정.")]
        private Vector2 _spotlightOffset;
        [SerializeField, Range(0f, 1f)] private float _weaknessDimAlpha = 0.72f;
        [SerializeField, Min(0.01f)] private float _spotlightDimInSeconds = 0.14f;
        [SerializeField, Min(0.01f)] private float _spotlightRingInSeconds = 0.32f;
        [SerializeField] private TutorialSpotlightGraphic.Style _skillSpotlightStyle = TutorialSpotlightGraphic.Style.Mint;
        [SerializeField] private TutorialSpotlightGraphic.Style _investmentSpotlightStyle = TutorialSpotlightGraphic.Style.Mint;
        [SerializeField] private TutorialSpotlightGraphic.Style _executeSpotlightStyle = TutorialSpotlightGraphic.Style.Mint;

        [Header("Enemy Target Reticle")]
        [SerializeField] private Color _enemyReticleColor = new Color(1f, 0.32f, 0.12f);
        [SerializeField, Range(0f, 1f)] private float _enemyReticleGlow = 0.85f;
        [SerializeField, Min(1f)] private float _enemyReticleSize = 104f;
        [SerializeField, Min(0f)] private float _enemyReticleHeight = 1.05f;

        private TutorialEnemyReticle _enemyReticle;
        private float _enemyReticleStartedAt;
        private const float LegacyHighlightPadding = 10f;
        private RectTransform _legacyHighlightParent;
        private readonly Vector3[] _skillCorners = new Vector3[4];
        private TutorialSpotlightGraphic _weaknessSpotlight;
        private float _spotlightStartedAt;

        private static readonly HashSet<string> UnlockedFeatures = new HashSet<string>();
        private static readonly HashSet<string> CompletedTutorials = new HashSet<string>();
        private static int _battleCount;

        public static event Action<string> FeatureUnlocked;

        private List<BattleTutorialEntry> _entries = new List<BattleTutorialEntry>();
        private BattleTutorialEntry _current;
        private int _pageStart;
        private int _pageEnd;
        private int _page;
        private bool _isOpen;
        private bool _openedDetail;
        private bool _detailPending;
        private enum SpotlightStep { None, Skill, Investment, Execute }

        private SpotlightStep _spotlightStep;
        private long? _spotlightTurnId;
        private bool _openSpotlightSkillMenu;
        private bool _firstBattle;
        private bool _guideWeakness;
        private bool _wasInBattle;
        private float _savedTimeScale = 1f;
        private bool[] _blockedStates = Array.Empty<bool>();
        private Coroutine _opening;

        public static bool IsFeatureUnlocked(string code)
        {
            return UnlockedFeatures.Contains(code);
        }

        public static int BattleCount => _battleCount;
        public bool IsOpen => _isOpen;
        /// <summary>약점 스킬 분홍 테두리가 떠 있는지. 떠 있는 동안 스킬 선택 테두리는 겹치지 않게 숨긴다.</summary>
        public bool IsWeaknessHintVisible => (_highlight != null && _highlight.gameObject.activeInHierarchy)
            || (_weaknessSpotlight != null && _weaknessSpotlight.gameObject.activeInHierarchy);

        /// <summary>새 게임을 시작할 때 튜토리얼 진행(해금·완료·전투 횟수)을 처음으로 되돌린다.</summary>
        public static void ResetForNewGame()
        {
            ResetSession();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            UnlockedFeatures.Clear();
            CompletedTutorials.Clear();
            _battleCount = 0;
            FeatureUnlocked = null;
        }

        private void Awake()
        {
            _legacyHighlightParent = _highlight != null ? _highlight.parent as RectTransform : null;
            if (_resetOnPlay)
            {
                UnlockedFeatures.Clear();
                CompletedTutorials.Clear();
                _battleCount = 0;
            }
            _entries = BattleTutorialTable.Parse(_table != null ? _table.text : null);
            if (_previous != null)
            {
                _previous.onClick.AddListener(Previous);
            }
            if (_next != null)
            {
                _next.onClick.AddListener(Next);
            }
            if (_nextLabel != null)
            {
                _nextLabel.onClick.AddListener(Next);
            }
            if (_popup != null)
            {
                _popup.SetActive(false);
            }
            HideWeaknessGuide();
        }

        private void OnDestroy()
        {
            if (_previous != null)
            {
                _previous.onClick.RemoveListener(Previous);
            }
            if (_next != null)
            {
                _next.onClick.RemoveListener(Next);
            }
            if (_nextLabel != null)
            {
                _nextLabel.onClick.RemoveListener(Next);
            }
        }

        private void OnEnable()
        {
            if (_spotlightHud != null)
            {
                _spotlightHud.SkillSelected += OnGuideSkillSelected;
                _spotlightHud.InvestmentCycled += OnGuideInvestmentCycled;
                _spotlightHud.SkillUseRequested += FinishSpotlight;
            }
            if (_battle != null)
            {
                _battle.FieldBattleEnded += OnFieldBattleEnded;
            }
        }

        // 결과 확인 직후 바로 다음 전투가 시작돼도 진입 횟수를 놓치지 않도록 종료 이벤트로도 정리한다.
        private void OnFieldBattleEnded(BattleOutcome outcome)
        {
            if (_wasInBattle)
            {
                OnBattleEnded();
                _wasInBattle = false;
            }
        }

        private void OnDisable()
        {
            if (_spotlightHud != null)
            {
                _spotlightHud.SkillSelected -= OnGuideSkillSelected;
                _spotlightHud.InvestmentCycled -= OnGuideInvestmentCycled;
                _spotlightHud.SkillUseRequested -= FinishSpotlight;
            }
            if (_battle != null)
            {
                _battle.FieldBattleEnded -= OnFieldBattleEnded;
            }
            if (_isOpen)
            {
                Close();
            }
            FinishSpotlight();
            HideEnemyReticle();
        }

        private void Update()
        {
            bool inBattle = _battle != null && _battle.IsInBattle;
            if (inBattle && !_wasInBattle)
            {
                OnBattleStarted();
            }
            else if (!inBattle && _wasInBattle)
            {
                OnBattleEnded();
            }
            _wasInBattle = inBattle;
            if (_isOpen)
            {
                ReadPopupInput();
                if (_isOpen)
                {
                    LayoutCoachMark();
                }
                return;
            }
            if (inBattle && _detailPending && _opening == null && _battle.HasSelectedBattleTarget)
            {
                // 대상을 지정해 몬스터 정보 카드가 열린 순간 약점 연쇄 안내를 이어서 띄운다.
                _detailPending = false;
                Open(_detailPage, _current.Texts.Length, true);
                return;
            }
        }

        private void LateUpdate()
        {
            bool inBattle = _battle != null && _battle.IsInBattle;
            UpdateEnemyReticle(inBattle);
            if (_spotlightStep != SpotlightStep.None)
            {
                UpdateSpotlight(inBattle);
            }
            else
            {
                UpdateWeaknessHighlight(inBattle);
            }
        }

        private void OnBattleStarted()
        {
            int index = _battleCount++;
            _firstBattle = index == 0;
            _guideWeakness = false;
            _current = _entries.FirstOrDefault(entry => entry.Condition == index && !CompletedTutorials.Contains(entry.Id));
            _detailPending = false;
            _spotlightStep = SpotlightStep.None;
            _spotlightTurnId = null;
            _openSpotlightSkillMenu = false;
            if (_current == null || _current.Texts.Length == 0)
            {
                _current = null;
                return;
            }
            CompletedTutorials.Add(_current.Id);
            foreach (string feature in _current.Features)
            {
                if (UnlockedFeatures.Add(feature) && FeatureUnlocked != null)
                {
                    FeatureUnlocked(feature);
                }
            }
            int end = _current.Texts.Length;
            if (_current.HasFeature(MonsterInfo) && _detailPage > 0 && _detailPage < end)
            {
                end = _detailPage;
                _detailPending = true;
            }
            _opening = StartCoroutine(OpenAfterDelay(end));
        }

        private IEnumerator OpenAfterDelay(int end)
        {
            float until = Time.time + _openDelay;
            while (Time.time < until)
            {
                if (_battle == null || !_battle.IsInBattle)
                {
                    _opening = null;
                    yield break;
                }
                yield return null;
            }
            _opening = null;
            Open(0, end, false);
        }

        private void OnBattleEnded()
        {
            if (_opening != null)
            {
                StopCoroutine(_opening);
                _opening = null;
            }
            if (_isOpen)
            {
                Close();
            }
            _detailPending = false;
            _firstBattle = false;
            _guideWeakness = false;
            _current = null;
            FinishSpotlight();
            HideEnemyReticle();
        }

        private void Open(int start, int end, bool isDetail)
        {
            if (_current == null || _popup == null || start >= end)
            {
                return;
            }
            _pageStart = start;
            _pageEnd = end;
            _page = start;
            _openedDetail = isDetail;
            _isOpen = true;
            _popup.SetActive(true);
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            _blockedStates = new bool[_blockedInput.Length];
            for (int index = 0; index < _blockedInput.Length; index++)
            {
                if (_blockedInput[index] != null)
                {
                    _blockedStates[index] = _blockedInput[index].enabled;
                    _blockedInput[index].enabled = false;
                }
            }
            if (_pauseBattle)
            {
                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            Render();
        }

        private void Close()
        {
            _isOpen = false;
            if (_popup != null)
            {
                _popup.SetActive(false);
            }
            for (int index = 0; index < _blockedInput.Length && index < _blockedStates.Length; index++)
            {
                if (_blockedInput[index] != null)
                {
                    _blockedInput[index].enabled = _blockedStates[index];
                }
            }
            if (_pauseBattle)
            {
                Time.timeScale = _savedTimeScale <= 0f ? 1f : _savedTimeScale;
            }
            // 약점 안내를 다 읽은 뒤부터 약점 속성 스킬을 하이라이트한다. 상세보기 단계가 없으면 첫 안내 뒤에 바로 시작한다.
            if (_current != null && _current.HasFeature(WeaknessChain) && (_openedDetail || !_detailPending))
            {
                _guideWeakness = true;
                if (_firstBattle)
                {
                    _spotlightStep = SpotlightStep.Skill;
                    _openSpotlightSkillMenu = true;
                }
            }
        }

        private void ReadPopupInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !Application.isFocused)
            {
                return;
            }
            // 조작 안내와 같게 ENTER(확인)로 넘긴다. 이전 페이지는 화면의 < 버튼을 쓴다.
            if (keyboard.enterKey.wasPressedThisFrame)
            {
                Next();
            }
        }

        public void Next()
        {
            if (!_isOpen)
            {
                return;
            }
            if (_page + 1 >= _pageEnd)
            {
                Close();
                return;
            }
            _page++;
            Render();
        }

        public void Previous()
        {
            if (!_isOpen || _page <= _pageStart)
            {
                return;
            }
            _page--;
            Render();
        }

        private void Render()
        {
            if (_title != null)
            {
                _title.text = GetTitle(_current.Id, _page);
            }
            if (_callout != null)
            {
                _callout.localRotation = Quaternion.identity;
            }
            if (_body != null)
            {
                _body.text = "•  " + _current.Texts[_page];
            }
            if (_pager != null)
            {
                var dots = new System.Text.StringBuilder();
                for (int index = _pageStart; index < _pageEnd; index++)
                {
                    dots.Append(index == _page ? "<color=#73EDFF>●</color>" : "<size=60%><color=#7D8699>●</color></size>");
                    if (index + 1 < _pageEnd)
                    {
                        dots.Append("  ");
                    }
                }
                _pager.text = dots.ToString();
            }
            if (_previous != null)
            {
                _previous.interactable = _page > _pageStart;
            }
            PageFocus focus = FindFocus(_current.Id, _page);
            if (_focusTag != null)
            {
                bool hasTag = focus != null && !string.IsNullOrEmpty(focus.Tag);
                _focusTag.transform.parent.gameObject.SetActive(hasTag);
                if (hasTag)
                {
                    _focusTag.text = focus.Tag;
                }
            }
            _pageShownAt = Time.unscaledTime;
            LayoutCoachMark();
        }

        private PageFocus FindFocus(string id, int page)
        {
            foreach (PageFocus focus in _focuses)
            {
                if (focus != null && focus.TutorialId == id && focus.Page == page)
                {
                    return focus;
                }
            }
            return null;
        }

        // 가리킬 HUD를 노란 상자로 감싸고 그 옆에 안내 카드를 붙인다. HUD가 움직여도 따라가도록 열려 있는 동안 매 프레임 부른다.
        private void LayoutCoachMark()
        {
            if (_callout == null || _current == null)
            {
                return;
            }
            float reveal = Mathf.Clamp01((Time.unscaledTime - _pageShownAt) / _revealSeconds);
            float eased = 1f - (1f - reveal) * (1f - reveal) * (1f - reveal);
            if (_coachGroup != null)
            {
                _coachGroup.alpha = eased;
            }
            PageFocus focus = FindFocus(_current.Id, _page);
            RectTransform area = (RectTransform)_callout.parent;
            Rect areaRect = area.rect;
            Vector2 calloutSize = _callout.rect.size;
            Rect target = default;
            bool hasTarget = focus != null && focus.Target != null && focus.Target.gameObject.activeInHierarchy
                && TryGetLocalRect(focus.Target, area, out target);
            if (_focusBox != null)
            {
                _focusBox.gameObject.SetActive(hasTarget);
            }
            _callout.anchorMin = _callout.anchorMax = new Vector2(0.5f, 0.5f);
            _callout.pivot = new Vector2(0.5f, 0.5f);
            Rect box = default;
            if (hasTarget)
            {
                box = new Rect(target.xMin - _focusPadding, target.yMin - _focusPadding,
                    target.width + _focusPadding * 2f, target.height + _focusPadding * 2f);
                PlaceFocusBox(box, eased);
            }
            if (!hasTarget)
            {
                _callout.anchoredPosition = _calloutFallbackPosition + Vector2.down * (CalloutSlide * (1f - eased));
                return;
            }
            // 카드는 상자 오른쪽에 두고, 화면 밖으로 나가면 왼쪽, 그래도 안 되면 아래(위)로 옮긴다.
            float halfWidth = calloutSize.x * 0.5f;
            float halfHeight = calloutSize.y * 0.5f;
            Vector2 position;
            float slideDirection;
            if (box.xMax + _calloutGap + calloutSize.x <= areaRect.xMax)
            {
                position = new Vector2(box.xMax + _calloutGap + halfWidth, box.center.y);
                slideDirection = 1f;
            }
            else if (box.xMin - _calloutGap - calloutSize.x >= areaRect.xMin)
            {
                position = new Vector2(box.xMin - _calloutGap - halfWidth, box.center.y);
                slideDirection = -1f;
            }
            else
            {
                bool below = box.yMin - _calloutGap - calloutSize.y >= areaRect.yMin;
                position = new Vector2(box.center.x,
                    below ? box.yMin - _calloutGap - halfHeight : box.yMax + _calloutGap + halfHeight);
                slideDirection = 0f;
            }
            position.x = Mathf.Clamp(position.x, areaRect.xMin + halfWidth, areaRect.xMax - halfWidth);
            position.y = Mathf.Clamp(position.y, areaRect.yMin + halfHeight, areaRect.yMax - halfHeight);
            _callout.anchoredPosition = position + Vector2.right * (slideDirection * CalloutSlide * (1f - eased));
        }

        private void PlaceFocusBox(Rect box, float eased)
        {
            if (_focusBox == null)
            {
                return;
            }
            _focusBox.anchorMin = _focusBox.anchorMax = new Vector2(0.5f, 0.5f);
            _focusBox.pivot = new Vector2(0.5f, 0.5f);
            _focusBox.anchoredPosition = box.center;
            _focusBox.sizeDelta = box.size;
            float pulse = 1f + 0.015f * Mathf.Sin(Time.unscaledTime * 6f);
            _focusBox.localScale = Vector3.one * (Mathf.Lerp(FocusStartScale, 1f, eased) * pulse);
        }

        private bool TryGetLocalRect(RectTransform target, RectTransform space, out Rect rect)
        {
            // 두 캔버스 모두 Screen Space Overlay이므로 화면 좌표를 그대로 옮긴다.
            target.GetWorldCorners(_corners);
            bool hasMin = RectTransformUtility.ScreenPointToLocalPointInRectangle(space, _corners[0], null, out Vector2 min);
            bool hasMax = RectTransformUtility.ScreenPointToLocalPointInRectangle(space, _corners[2], null, out Vector2 max);
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return hasMin && hasMax && rect.width > 1f && rect.height > 1f;
        }

        private string GetTitle(string id, int page)
        {
            PageTitle title = FindTitle(id, page);
            return title != null ? title.Title : "튜토리얼";
        }

        private PageTitle FindTitle(string id, int page)
        {
            PageTitle best = null;
            foreach (PageTitle title in _titles)
            {
                if (title != null && title.TutorialId == id && title.FromPage <= page
                    && (best == null || title.FromPage > best.FromPage))
                {
                    best = title;
                }
            }
            return best;
        }

        private void OnGuideSkillSelected(int index)
        {
            if (_spotlightStep == SpotlightStep.Skill && !_isOpen && _battle.Skills[index].Id == _spotlightSkillId)
            {
                HideWeaknessGuide();
                _spotlightStep = SpotlightStep.Investment;
            }
        }

        private void OnGuideInvestmentCycled()
        {
            if (_spotlightStep == SpotlightStep.Investment && !_isOpen)
            {
                HideWeaknessGuide();
                _spotlightStep = SpotlightStep.Execute;
            }
        }

        private void FinishSpotlight()
        {
            _spotlightStep = SpotlightStep.None;
            _spotlightTurnId = null;
            _openSpotlightSkillMenu = false;
            HideWeaknessGuide();
        }

        private RectTransform GetGuideSkillButton()
        {
            for (int index = 0; index < _skillButtons.Length && index < _battle.Skills.Count; index++)
            {
                if (_battle.Skills[index].Id == _spotlightSkillId)
                {
                    return _skillButtons[index];
                }
            }
            return null;
        }

        private void UpdateEnemyReticle(bool inBattle)
        {
            if (!inBattle || !_firstBattle || !_detailPending || _opening != null || _isOpen
                || _battle.HasSelectedBattleTarget || !_battle.CanChooseAction
                || !_battle.TryGetMonsterTransform(TutorialBattleController.FirstMonsterId, out Transform enemy))
            {
                HideEnemyReticle();
                return;
            }

            Camera camera = _battle.BattleCamera;
            RectTransform canvasRect = (RectTransform)transform;
            Vector3 point = camera.WorldToScreenPoint(enemy.position + Vector3.up * _enemyReticleHeight);
            if (point.z <= 0f || !camera.pixelRect.Contains(point)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, point, null, out Vector2 center))
            {
                HideEnemyReticle();
                return;
            }
            if (_enemyReticle == null)
            {
                var marker = new GameObject("Tutorial Enemy Reticle", typeof(TutorialEnemyReticle));
                marker.layer = gameObject.layer;
                marker.transform.SetParent(canvasRect, false);
                marker.transform.SetAsFirstSibling();
                _enemyReticle = marker.GetComponent<TutorialEnemyReticle>();
                _enemyReticle.Clicked += SelectReticleTarget;
                _enemyReticleStartedAt = Time.unscaledTime;
            }

            _enemyReticle.gameObject.SetActive(true);
            RenderEnemyReticle(_enemyReticle, center, Time.unscaledTime - _enemyReticleStartedAt);
        }

        private void SelectReticleTarget()
        {
            _battle.SelectTarget(0);
            if (_battle.HasSelectedBattleTarget)
            {
                HideEnemyReticle();
            }
        }

        private void HideEnemyReticle()
        {
            if (_enemyReticle != null && _enemyReticle.gameObject.activeSelf)
            {
                _enemyReticle.gameObject.SetActive(false);
            }
        }

        public void RenderEnemyReticle(TutorialEnemyReticle reticle, Vector2 center, float elapsed)
        {
            reticle.rectTransform.localPosition = center;
            reticle.SetFrame(_enemyReticleSize, _enemyReticleColor, _enemyReticleGlow, elapsed);
        }

        private void UpdateSpotlight(bool inBattle)
        {
            if (_spotlightTurnId.HasValue && inBattle && _battle.Snapshot?.TurnId != _spotlightTurnId)
            {
                FinishSpotlight();
            }
            if (_spotlightStep == SpotlightStep.None || !inBattle || _isOpen || !_battle.CanChooseAction
                || !_battle.HasSelectedBattleTarget)
            {
                HideWeaknessGuide();
                return;
            }

            if (_openSpotlightSkillMenu)
            {
                _openSpotlightSkillMenu = false;
                _spotlightHud?.OpenSkillMenuForTutorial();
            }
            if (_spotlightHud != null && _spotlightHud.IsCommandPhase)
            {
                HideWeaknessGuide();
                return;
            }

            RectTransform region = _spotlightStep switch
            {
                SpotlightStep.Skill => _spotlightRegion != null ? _spotlightRegion : GetGuideSkillButton(),
                SpotlightStep.Investment => _investmentSpotlightRegion,
                SpotlightStep.Execute => _executeSpotlightRegion,
                _ => null,
            };
            if (region == null || !region.gameObject.activeInHierarchy)
            {
                HideWeaknessGuide();
                return;
            }

            _spotlightTurnId = _battle.Snapshot.TurnId;
            if (!_useLegacyWeaknessHighlight)
            {
                SetHighlightVisible(false);
                UpdateWeaknessSpotlight(region);
                return;
            }

            if (_weaknessSpotlight != null)
            {
                _weaknessSpotlight.gameObject.SetActive(false);
            }
            if (_highlight != null)
            {
                UpdateLegacyHighlight(region);
            }
        }

        private void UpdateWeaknessHighlight(bool inBattle)
        {
            bool isMissingLegacyHighlight = _useLegacyWeaknessHighlight && _highlight == null;
            if (!_guideWeakness || _isOpen || !inBattle || isMissingLegacyHighlight || !_battle.CanChooseAction
                || !_battle.HasSelectedBattleTarget)
            {
                HideWeaknessGuide();
                return;
            }
            BattleSnapshot state = _battle.Snapshot;
            CombatantState target = state == null ? null
                : state.Combatants.FirstOrDefault(unit => unit.InstanceId == _battle.TargetId);
            if (target == null || target.IsDead || target.Data.WeaknessChain.Count == 0)
            {
                HideWeaknessGuide();
                return;
            }
            int step = target.ChainStep < target.Data.WeaknessChain.Count ? target.ChainStep : 0;
            BattleElement weakness = target.Data.WeaknessChain[step];
            RectTransform button = null;
            int buttonIndex = -1;
            for (int index = 0; index < _skillButtons.Length && index < _battle.Skills.Count; index++)
            {
                if (_skillButtons[index] != null && _skillButtons[index].gameObject.activeInHierarchy
                    && _battle.Skills[index].Element == weakness)
                {
                    button = _skillButtons[index];
                    buttonIndex = index;
                    break;
                }
            }
            if (button == null)
            {
                HideWeaknessGuide();
                return;
            }
            // 첫 전투 단계 안내가 끝난 뒤에도 약점 스킬은 분홍 테두리 대신 스포트라이트로 안내한다.
            if (!_useLegacyWeaknessHighlight)
            {
                SetHighlightVisible(false);
                UpdateWeaknessSpotlight(button);
                return;
            }
            // 기울어진 스킬 버튼을 그대로 감싸도록 버튼의 자식으로 붙인다(분홍 네온 테두리).
            if (_highlight.parent != button)
            {
                _highlight.SetParent(button, false);
                _highlight.anchorMin = Vector2.zero;
                _highlight.anchorMax = Vector2.one;
                _highlight.pivot = new Vector2(0.5f, 0.5f);
                _highlight.offsetMin = new Vector2(-_highlightPadding, -_highlightPadding);
                _highlight.offsetMax = new Vector2(_highlightPadding - _highlightRightTrim, _highlightPadding);
                _highlight.localRotation = Quaternion.identity;
                _highlight.localScale = Vector3.one;
            }
            _highlight.SetAsLastSibling();
            SetHighlightVisible(true);
            if (_highlightTag != null)
            {
                // 이미 약점 스킬에 커서가 있으면 꼬리표가 설명 패널을 가리지 않게 테두리만 남긴다.
                bool isOnWeakness = _hud != null && _hud.SelectedSkill == buttonIndex;
                _highlightTag.SetActive(!isOnWeakness);
            }
            if (_highlightGroup != null)
            {
                _highlightGroup.alpha = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 2.2f, 1f);
            }
        }

        private void UpdateWeaknessSpotlight(RectTransform region)
        {
            RectTransform canvasRect = (RectTransform)transform;
            if (_weaknessSpotlight == null)
            {
                var spotlight = new GameObject("Weakness Spotlight", typeof(TutorialSpotlightGraphic));
                spotlight.SetActive(false);
                spotlight.layer = gameObject.layer;
                _weaknessSpotlight = spotlight.GetComponent<TutorialSpotlightGraphic>();
                _weaknessSpotlight.raycastTarget = false;
                _weaknessSpotlight.rectTransform.SetParent(canvasRect, false);
                _weaknessSpotlight.rectTransform.SetAsFirstSibling();
            }
            if (!_weaknessSpotlight.gameObject.activeSelf)
            {
                _spotlightStartedAt = Time.unscaledTime;
                _weaknessSpotlight.gameObject.SetActive(true);
            }

            RenderSpotlight(_weaknessSpotlight, region, _spotlightStep, Time.unscaledTime - _spotlightStartedAt);
        }

        private void RenderSpotlight(TutorialSpotlightGraphic graphic, RectTransform region, SpotlightStep step, float elapsed)
        {
            RectTransform canvasRect = (RectTransform)transform;
            // Follow the current region, including the rotation of buttons on the skill arc.
            region.GetWorldCorners(_skillCorners);
            Vector2 bottomLeft = canvasRect.InverseTransformPoint(_skillCorners[0]);
            Vector2 topLeft = canvasRect.InverseTransformPoint(_skillCorners[1]);
            Vector2 bottomRight = canvasRect.InverseTransformPoint(_skillCorners[3]);
            Vector2 right = bottomRight - bottomLeft;
            Vector2 up = topLeft - bottomLeft;
            Vector2 center = bottomLeft + (right + up) * 0.5f
                + right.normalized * _spotlightOffset.x + up.normalized * _spotlightOffset.y;
            Vector2 halfSize = new Vector2(right.magnitude, up.magnitude) * 0.5f
                + Vector2.Max(Vector2.zero, _spotlightPadding);
            RectTransform spotlightRect = graphic.rectTransform;
            spotlightRect.localPosition = center;
            spotlightRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg);

            Vector2 offset = canvasRect.rect.center - center;
            Vector2 farthest = new Vector2(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) + canvasRect.rect.size * 0.5f;
            float dimProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _spotlightDimInSeconds));
            float ringProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _spotlightRingInSeconds));
            TutorialSpotlightGraphic.Style style = step switch
            {
                SpotlightStep.Investment => _investmentSpotlightStyle,
                SpotlightStep.Execute => _executeSpotlightStyle,
                _ => _skillSpotlightStyle,
            };
            graphic.SetFrame(halfSize, farthest.magnitude, style,
                _weaknessDimAlpha * Mathf.SmoothStep(0f, 1f, dimProgress), ringProgress, elapsed);
        }

#if UNITY_EDITOR
        public void RenderSpotlightPreview(TutorialSpotlightGraphic graphic, RectTransform region, int stage, float elapsed)
        {
            RenderSpotlight(graphic, region, (SpotlightStep)(stage + 1), elapsed);
        }
#endif

        private void UpdateLegacyHighlight(RectTransform button)
        {
            // 두 캔버스 모두 Screen Space Overlay이므로 버튼의 화면 사각형을 그대로 옮긴다.
            if (_legacyHighlightParent == null)
            {
                return;
            }
            if (_highlight.parent != _legacyHighlightParent)
            {
                _highlight.SetParent(_legacyHighlightParent, false);
            }
            button.GetWorldCorners(_skillCorners);
            RectTransform parent = _legacyHighlightParent;
            Vector2 min, max;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _skillCorners[0], null, out min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _skillCorners[2], null, out max);
            _highlight.anchorMin = _highlight.anchorMax = new Vector2(0.5f, 0.5f);
            _highlight.pivot = new Vector2(0.5f, 0.5f);
            _highlight.localPosition = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
            _highlight.sizeDelta = new Vector2(max.x - min.x, max.y - min.y) + Vector2.one * (LegacyHighlightPadding * 2f);
            SetHighlightVisible(true);
            if (_highlightTag != null)
            {
                _highlightTag.SetActive(false);
            }
            if (_highlightGroup != null)
            {
                _highlightGroup.alpha = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 2.2f, 1f);
            }
        }

        private void HideWeaknessGuide()
        {
            SetHighlightVisible(false);
            if (_weaknessSpotlight != null)
            {
                _weaknessSpotlight.gameObject.SetActive(false);
            }
        }

        private void SetHighlightVisible(bool visible)
        {
            if (_highlight != null && _highlight.gameObject.activeSelf != visible)
            {
                _highlight.gameObject.SetActive(visible);
            }
        }
    }
}
