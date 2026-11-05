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
        [SerializeField] private RectTransform _highlight;
        [SerializeField] private CanvasGroup _highlightGroup;
        [SerializeField, Min(0f)] private float _highlightPadding = 10f;

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
            SetHighlightVisible(false);
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
            if (_battle != null)
            {
                _battle.FieldBattleEnded -= OnFieldBattleEnded;
            }
            if (_isOpen)
            {
                Close();
            }
            SetHighlightVisible(false);
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
                SetHighlightVisible(false);
                return;
            }
            if (inBattle && _detailPending && _opening == null && _battle.HasSelectedBattleTarget)
            {
                // 대상을 지정해 몬스터 정보 카드가 열린 순간 약점 연쇄 안내를 이어서 띄운다.
                _detailPending = false;
                Open(_detailPage, _current.Texts.Length, true);
                return;
            }
            UpdateHighlight(inBattle);
        }

        private void OnBattleStarted()
        {
            int index = _battleCount++;
            _current = _entries.FirstOrDefault(entry => entry.Condition == index && !CompletedTutorials.Contains(entry.Id));
            _detailPending = false;
            _guideWeakness = false;
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
            _guideWeakness = false;
            _current = null;
            SetHighlightVisible(false);
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
        }

        private string GetTitle(string id, int page)
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
            return best != null ? best.Title : "튜토리얼";
        }

        private void UpdateHighlight(bool inBattle)
        {
            if (!_guideWeakness || !inBattle || _highlight == null || !_battle.CanChooseAction
                || !_battle.HasSelectedBattleTarget)
            {
                SetHighlightVisible(false);
                return;
            }
            BattleSnapshot state = _battle.Snapshot;
            CombatantState target = state == null ? null
                : state.Combatants.FirstOrDefault(unit => unit.InstanceId == _battle.TargetId);
            if (target == null || target.IsDead || target.Data.WeaknessChain.Count == 0)
            {
                SetHighlightVisible(false);
                return;
            }
            int step = target.ChainStep < target.Data.WeaknessChain.Count ? target.ChainStep : 0;
            BattleElement weakness = target.Data.WeaknessChain[step];
            RectTransform button = null;
            for (int index = 0; index < _skillButtons.Length && index < _battle.Skills.Count; index++)
            {
                if (_skillButtons[index] != null && _skillButtons[index].gameObject.activeInHierarchy
                    && _battle.Skills[index].Element == weakness)
                {
                    button = _skillButtons[index];
                    break;
                }
            }
            if (button == null)
            {
                SetHighlightVisible(false);
                return;
            }
            // 두 캔버스 모두 Screen Space Overlay이므로 버튼의 화면 사각형을 그대로 옮긴다.
            var corners = new Vector3[4];
            button.GetWorldCorners(corners);
            RectTransform parent = (RectTransform)_highlight.parent;
            Vector2 min, max;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, corners[0], null, out min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, corners[2], null, out max);
            _highlight.anchorMin = _highlight.anchorMax = new Vector2(0.5f, 0.5f);
            _highlight.pivot = new Vector2(0.5f, 0.5f);
            _highlight.localPosition = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
            _highlight.sizeDelta = new Vector2(max.x - min.x, max.y - min.y) + Vector2.one * (_highlightPadding * 2f);
            SetHighlightVisible(true);
            if (_highlightGroup != null)
            {
                _highlightGroup.alpha = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 2.2f, 1f);
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
