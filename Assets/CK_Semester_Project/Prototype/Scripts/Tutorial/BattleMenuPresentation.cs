using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>
    /// 전투 행동 메뉴 연출. 스킬 목록을 열면 화면을 어둡게 깔고 스킬 버튼을 위에서부터 차례로 펼치며,
    /// 고른 스킬에는 네온 테두리를 씌워 튀어나오게 한다. 버튼 위치는 건드리지 않아 메모리 투자 패널 정렬이 유지된다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class BattleMenuPresentation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PrototypeHudPresenter _hud;
        [SerializeField, Tooltip("스킬 버튼 묶음(SkillPanel). 열릴 때 통째로 미끄러져 들어온다.")]
        private RectTransform _skillPanel;
        [SerializeField, Tooltip("HUD의 스킬 버튼과 같은 순서. 비우면 스킬 패널 아래 SkillButton_으로 시작하는 오브젝트를 순서대로 쓴다.")]
        private RectTransform[] _skillButtons = new RectTransform[0];
        [SerializeField, Tooltip("공격·방어 등 첫 커맨드 묶음(CommandSelect). 비워도 된다.")]
        private RectTransform _commandPanel;
        [SerializeField, Tooltip("오른쪽 스킬 설명 패널. 비워도 된다.")]
        private RectTransform _detailsPanel;
        [SerializeField, Tooltip("스킬 목록이 열린 동안 화면을 덮는 어두운 막(전투 UI 맨 뒤)")]
        private Image _dim;
        [SerializeField, Tooltip("고른 스킬 버튼에 씌우는 네온 테두리")]
        private RectTransform _selectionGlow;
        [SerializeField, Tooltip("약점 스킬 안내. 분홍 테두리가 떠 있는 동안 선택 테두리를 숨겨 테두리가 두 개 보이지 않게 한다. 비워도 된다.")]
        private BattleTutorialDirector _tutorial;

        [Header("Dim")]
        [SerializeField, Range(0f, 1f), Tooltip("스킬 목록이 열린 동안 어두운 막의 불투명도")]
        private float _dimAlpha = 0.62f;
        [SerializeField, Min(0.01f), Tooltip("어두운 막이 깔리고 걷히는 시간(초)")]
        private float _dimSeconds = 0.18f;

        [Header("Open")]
        [SerializeField, Min(0.01f), Tooltip("메뉴가 펼쳐지는 시간(초)")]
        private float _openSeconds = 0.24f;
        [SerializeField, Min(0f), Tooltip("스킬 버튼마다 늦게 나타나는 간격(초)")]
        private float _staggerSeconds = 0.045f;
        [SerializeField, Tooltip("메뉴가 들어오기 시작하는 위치(px, 최종 위치 기준)")]
        private Vector2 _openOffset = new Vector2(-90f, 0f);
        [SerializeField, Range(0.5f, 1f), Tooltip("메뉴가 들어오기 시작할 때의 크기 배율")]
        private float _openStartScale = 0.9f;
        [SerializeField, Tooltip("설명 패널이 들어오기 시작하는 위치(px, 최종 위치 기준)")]
        private Vector2 _detailsOffset = new Vector2(80f, 0f);

        [Header("Selection")]
        [SerializeField, Min(0f), Tooltip("네온 테두리를 버튼 바깥으로 넓히는 여백(px)")]
        private float _glowPadding = 22f;
        [SerializeField, Min(0f), Tooltip("버튼 이미지 오른쪽의 투명한 부분만큼 테두리 오른쪽을 줄이는 양(px)")]
        private float _glowRightTrim = 150f;
        [SerializeField, Tooltip("네온 테두리 색")]
        private Color _glowColor = new Color(1f, 0.3f, 0.68f, 1f);
        [SerializeField, Min(0.01f), Tooltip("스킬을 바꿨을 때 테두리가 튀어나왔다 돌아오는 시간(초)")]
        private float _selectPopSeconds = 0.2f;
        [SerializeField, Min(0f), Tooltip("튀어나올 때 커지는 정도(1 = 두 배)")]
        private float _selectPopScale = 0.14f;

        private const float GlowPulseSpeed = 5.5f;
        private const float UnselectedGlowAlpha = 0.55f;
        private const string SkillButtonPrefix = "SkillButton_";

        private CanvasGroup[] _buttonGroups = new CanvasGroup[0];
        private CanvasGroup _skillGroup;
        private CanvasGroup _commandGroup;
        private CanvasGroup _detailsGroup;
        private Vector2 _skillBasePosition;
        private Vector2 _commandBasePosition;
        private Vector2 _detailsBasePosition;
        private Image _glowImage;
        private bool _wasSkillOpen;
        private bool _wasCommandOpen;
        private bool _wasDetailsOpen;
        private float _skillOpenedAt;
        private float _commandOpenedAt;
        private float _detailsOpenedAt;
        private float _selectedAt;
        private int _shownSelection = -1;
        private float _dimCurrent;

        private void Awake()
        {
            _skillGroup = GetOrAddGroup(_skillPanel);
            _commandGroup = GetOrAddGroup(_commandPanel);
            _detailsGroup = GetOrAddGroup(_detailsPanel);
            _skillBasePosition = _skillPanel != null ? _skillPanel.anchoredPosition : Vector2.zero;
            _commandBasePosition = _commandPanel != null ? _commandPanel.anchoredPosition : Vector2.zero;
            _detailsBasePosition = _detailsPanel != null ? _detailsPanel.anchoredPosition : Vector2.zero;
            if ((_skillButtons.Length == 0 || System.Array.TrueForAll(_skillButtons, button => button == null)) && _skillPanel != null)
            {
                _skillButtons = _skillPanel.Cast<Transform>()
                    .Where(child => child.name.StartsWith(SkillButtonPrefix, System.StringComparison.Ordinal))
                    .Select(child => (RectTransform)child)
                    .ToArray();
            }
            _buttonGroups = new CanvasGroup[_skillButtons.Length];
            for (int index = 0; index < _skillButtons.Length; index++)
            {
                _buttonGroups[index] = GetOrAddGroup(_skillButtons[index]);
            }
            if (_selectionGlow != null)
            {
                _glowImage = _selectionGlow.GetComponent<Image>();
                _glowImage.raycastTarget = false;
                _selectionGlow.gameObject.SetActive(false);
            }
            if (_dim != null)
            {
                _dim.raycastTarget = false;
                // 메뉴 뒤, 나머지 전투 화면 위에 깔리도록 맨 앞 형제로 둔다.
                _dim.transform.SetAsFirstSibling();
                SetDim(0f);
            }
        }

        private void OnDisable()
        {
            _wasSkillOpen = false;
            _wasCommandOpen = false;
            _wasDetailsOpen = false;
            _shownSelection = -1;
            _dimCurrent = 0f;
            SetDim(0f);
            if (_selectionGlow != null)
            {
                _selectionGlow.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            bool isSkillOpen = IsShown(_skillPanel);
            bool isCommandOpen = IsShown(_commandPanel);
            bool isDetailsOpen = IsShown(_detailsPanel);
            if (isSkillOpen && !_wasSkillOpen)
            {
                _skillOpenedAt = now;
                _shownSelection = -1;
            }
            if (isCommandOpen && !_wasCommandOpen)
            {
                _commandOpenedAt = now;
            }
            if (isDetailsOpen && !_wasDetailsOpen)
            {
                _detailsOpenedAt = now;
            }
            _wasSkillOpen = isSkillOpen;
            _wasCommandOpen = isCommandOpen;
            _wasDetailsOpen = isDetailsOpen;

            UpdateDim(isSkillOpen);
            if (isSkillOpen)
            {
                AnimateSkillPanel(now);
                UpdateSelectionGlow(now);
            }
            else if (_selectionGlow != null && _selectionGlow.gameObject.activeSelf)
            {
                _selectionGlow.gameObject.SetActive(false);
            }
            if (isCommandOpen)
            {
                AnimatePanel(_commandPanel, _commandGroup, _commandBasePosition, _openOffset, now - _commandOpenedAt);
            }
            if (isDetailsOpen)
            {
                AnimatePanel(_detailsPanel, _detailsGroup, _detailsBasePosition, _detailsOffset, now - _detailsOpenedAt);
            }
        }

        private void UpdateDim(bool isSkillOpen)
        {
            if (_dim == null)
            {
                return;
            }
            float target = isSkillOpen ? _dimAlpha : 0f;
            _dimCurrent = Mathf.MoveTowards(_dimCurrent, target, Time.unscaledDeltaTime * _dimAlpha / _dimSeconds);
            SetDim(_dimCurrent);
        }

        private void AnimateSkillPanel(float now)
        {
            float elapsed = now - _skillOpenedAt;
            float eased = EaseOutCubic(elapsed / _openSeconds);
            _skillPanel.anchoredPosition = _skillBasePosition + _openOffset * (1f - eased);
            _skillPanel.localScale = Vector3.one * Mathf.Lerp(_openStartScale, 1f, eased);
            if (_skillGroup != null)
            {
                _skillGroup.alpha = 1f;
            }
            // 위 버튼부터 차례로 켜져 부채꼴이 펼쳐지는 느낌을 준다.
            for (int index = 0; index < _buttonGroups.Length; index++)
            {
                if (_buttonGroups[index] != null)
                {
                    _buttonGroups[index].alpha = EaseOutCubic((elapsed - index * _staggerSeconds) / _openSeconds);
                }
            }
        }

        private void UpdateSelectionGlow(float now)
        {
            if (_selectionGlow == null || _hud == null)
            {
                return;
            }
            int selected = _hud.SelectedSkill;
            bool hasButton = selected >= 0 && selected < _skillButtons.Length && _skillButtons[selected] != null
                && _skillButtons[selected].gameObject.activeInHierarchy;
            bool isWeaknessHintShown = _tutorial != null && _tutorial.IsWeaknessHintVisible;
            if (!hasButton || isWeaknessHintShown)
            {
                _selectionGlow.gameObject.SetActive(false);
                return;
            }
            if (selected != _shownSelection)
            {
                _shownSelection = selected;
                _selectedAt = now;
                _selectionGlow.SetParent(_skillButtons[selected], false);
                _selectionGlow.anchorMin = Vector2.zero;
                _selectionGlow.anchorMax = Vector2.one;
                _selectionGlow.pivot = new Vector2(0.5f, 0.5f);
                _selectionGlow.offsetMin = new Vector2(-_glowPadding, -_glowPadding);
                _selectionGlow.offsetMax = new Vector2(_glowPadding - _glowRightTrim, _glowPadding);
                _selectionGlow.localRotation = Quaternion.identity;
            }
            _selectionGlow.SetAsLastSibling();
            _selectionGlow.gameObject.SetActive(true);
            float pop = 1f - EaseOutCubic((now - _selectedAt) / _selectPopSeconds);
            _selectionGlow.localScale = Vector3.one * (1f + _selectPopScale * pop);
            float pulse = 0.82f + 0.18f * Mathf.Sin(now * GlowPulseSpeed);
            float strength = _hud.HasSkillSelection ? 1f : UnselectedGlowAlpha;
            Color color = Color.Lerp(_glowColor, Color.white, pop * 0.6f);
            color.a = Mathf.Clamp01(strength * pulse + pop * 0.4f);
            _glowImage.color = color;
        }

        private void AnimatePanel(RectTransform panel, CanvasGroup group, Vector2 basePosition, Vector2 offset, float elapsed)
        {
            float eased = EaseOutCubic(elapsed / _openSeconds);
            panel.anchoredPosition = basePosition + offset * (1f - eased);
            if (group != null)
            {
                group.alpha = eased;
            }
        }

        private void SetDim(float alpha)
        {
            if (_dim == null)
            {
                return;
            }
            Color color = _dim.color;
            color.a = alpha;
            _dim.color = color;
            _dim.enabled = alpha > 0.001f;
        }

        private static bool IsShown(RectTransform panel)
        {
            return panel != null && panel.gameObject.activeInHierarchy;
        }

        private static CanvasGroup GetOrAddGroup(RectTransform target)
        {
            if (target == null)
            {
                return null;
            }
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
        }

        private static float EaseOutCubic(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            float inverse = 1f - clamped;
            return 1f - inverse * inverse * inverse;
        }
    }
}
