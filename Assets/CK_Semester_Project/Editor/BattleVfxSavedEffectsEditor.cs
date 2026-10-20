using System;
using System.Linq;
using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEngine.UIElements;

namespace CK.SemesterProject.EditorTools
{
    [CustomEditor(typeof(BattleVfxSavedEffects))]
    public sealed class BattleVfxSavedEffectsEditor : UnityEditor.Editor
    {
        private const string StylePath = "Assets/CK_Semester_Project/Editor/BattleVfxSavedEffectsEditor.uss";
        private const int PageSize = 8;
        [UnityEngine.SerializeField] private string _searchText = string.Empty;
        [UnityEngine.SerializeField] private int _pageIndex;
        private BattleVfxSavedEffectPreview _preview;
        private BattleVfxSavedEffectLibrary.Entry[] _entries = Array.Empty<BattleVfxSavedEffectLibrary.Entry>();
        private BattleVfxSavedEffectLibrary.Entry _selected;
        private VisualElement _root;
        private VisualElement _list;
        private VisualElement _selectionControls;
        private Label _count;
        private TextField _search;
        private Label _pageStatus;
        private Button _previousPage;
        private Button _nextPage;
        private Label _selection;
        private Label _status;
        private Slider _timeline;

        private void OnEnable()
        {
            _preview = new BattleVfxSavedEffectPreview();
            EditorApplication.projectChanged += Refresh;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Undo.undoRedoPerformed += Refresh;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= Refresh;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            Undo.undoRedoPerformed -= Refresh;
            _preview?.Dispose();
        }

        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement { name = "savedEffectsInspector", focusable = true };
            _root.AddToClassList("saved-effects");
            _root.RegisterCallback<KeyDownEvent>(ListKeyDown);
            StyleSheet stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (stylesheet != null)
            {
                _root.styleSheets.Add(stylesheet);
            }
            _root.Add(new HelpBox("목록에서 저장본을 선택해 재생하거나 삭제하세요. 재생은 Play 없이 기본 더미를 기준으로 미리 봅니다.", HelpBoxMessageType.Info));
            _count = new Label();
            _root.Add(_count);
            var searchRow = new VisualElement();
            searchRow.AddToClassList("saved-effect-search");
            _search = new TextField("이름 검색") { name = "savedEffectSearch", value = _searchText };
            _search.RegisterValueChangedCallback(change =>
            {
                _searchText = change.newValue;
                _pageIndex = 0;
                RenderList();
            });
            searchRow.Add(_search);
            searchRow.Add(new Button(() => _search.value = string.Empty) { text = "초기화", name = "clearSavedEffectSearch" });
            _root.Add(searchRow);
            _root.Add(new Button(Refresh) { text = "목록 새로고침", name = "refreshSavedEffects" });
            _list = new VisualElement { name = "savedEffectsList" };
            _root.Add(_list);
            var pages = new VisualElement { name = "savedEffectPages", tooltip = "목록에 포커스가 있을 때 PageUp / PageDown으로도 이동할 수 있습니다." };
            pages.AddToClassList("saved-effect-pages");
            _previousPage = new Button(() => ChangePage(-1)) { text = "이전", name = "previousSavedEffectPage" };
            pages.Add(_previousPage);
            _pageStatus = new Label { name = "savedEffectPageStatus" };
            pages.Add(_pageStatus);
            _nextPage = new Button(() => ChangePage(1)) { text = "다음", name = "nextSavedEffectPage" };
            pages.Add(_nextPage);
            _root.Add(pages);
            _selectionControls = new VisualElement { name = "savedEffectControls" };
            _selectionControls.AddToClassList("saved-effect-controls");
            _selection = new Label("저장본을 선택하세요.");
            _selection.AddToClassList("saved-effect-selection");
            _selectionControls.Add(_selection);
            _selectionControls.Add(new Button(Play) { text = "재생 / 처음부터", name = "playSavedEffect" });
            _selectionControls.Add(new Button(() => _preview.SetPlaying(!_preview.IsPlaying)) { text = "일시정지 / 계속", name = "pauseSavedEffect" });
            _selectionControls.Add(new Button(() => _preview.Stop()) { text = "정지", name = "stopSavedEffect" });
            var loop = new Toggle("반복 재생") { value = _preview.Loop };
            loop.RegisterValueChangedCallback(change => _preview.Loop = change.newValue);
            _selectionControls.Add(loop);
            var speed = new Slider("미리보기 속도", .1f, 4f) { value = _preview.Speed, showInputField = true };
            speed.RegisterValueChangedCallback(change => _preview.Speed = change.newValue);
            _selectionControls.Add(speed);
            _timeline = new Slider("재생 위치 (초)", 0f, 1f) { showInputField = true, name = "savedEffectTimeline" };
            _timeline.RegisterValueChangedCallback(change =>
            {
                _preview.SetPlaying(false);
                _preview.Sample(change.newValue);
            });
            _selectionControls.Add(_timeline);
            _status = new Label();
            _status.AddToClassList("saved-effect-status");
            _selectionControls.Add(_status);
            _selectionControls.Add(new Button(() => EditorGUIUtility.PingObject(_selected?.Prefab)) { text = "Project에서 저장본 찾기" });
            _selectionControls.Add(new Button(DeleteSelected) { text = "선택한 저장본 삭제", name = "deleteSavedEffect" });
            _root.Add(_selectionControls);
            _root.schedule.Execute(UpdatePlaybackStatus).Every(100);
            Refresh();
            return _root;
        }

        private void Refresh()
        {
            if (_list == null)
            {
                return;
            }
            string selectedPath = _selected?.AssetPath;
            _entries = BattleVfxSavedEffectLibrary.GetEntries();
            _selected = _entries.FirstOrDefault(entry => entry.AssetPath == selectedPath);
            if (_selected == null)
            {
                _preview.Stop();
            }
            RenderList();
        }

        private void RenderList()
        {
            string query = (_searchText ?? string.Empty).Trim();
            BattleVfxSavedEffectLibrary.Entry[] matches = string.IsNullOrEmpty(query) ? _entries
                : _entries.Where(entry => entry.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            int pageCount = Math.Max(1, (matches.Length + PageSize - 1) / PageSize);
            _pageIndex = Math.Max(0, Math.Min(_pageIndex, pageCount - 1));
            BattleVfxSavedEffectLibrary.Entry[] visible = matches.Skip(_pageIndex * PageSize).Take(PageSize).ToArray();
            _list.Clear();
            _count.text = string.IsNullOrEmpty(query) ? "저장된 이펙트 " + _entries.Length + "개"
                : "검색 결과 " + matches.Length + "개 / 전체 " + _entries.Length + "개";
            foreach (BattleVfxSavedEffectLibrary.Entry entry in visible)
            {
                var button = new Button(() => Select(entry))
                {
                    text = entry.DisplayName,
                    tooltip = entry.AssetPath
                };
                button.AddToClassList("saved-effect-item");
                if (_selected == entry)
                {
                    button.AddToClassList("saved-effect-item-selected");
                }
                _list.Add(button);
            }
            if (matches.Length == 0)
            {
                _list.Add(new HelpBox(string.IsNullOrEmpty(query)
                    ? "아직 저장된 이펙트가 없습니다. 씬의 이펙트에서 ‘이펙트 배치 저장’ 컴포넌트의 ‘다른 이름으로 저장’을 누르세요."
                    : "이름이 일치하는 저장본이 없습니다. 검색어를 바꾸거나 초기화하세요.", HelpBoxMessageType.Info));
            }
            _pageStatus.text = (_pageIndex + 1) + " / " + pageCount + " 페이지";
            _previousPage.SetEnabled(_pageIndex > 0);
            _nextPage.SetEnabled(_pageIndex < pageCount - 1);
            _selection.text = _selected != null ? "선택한 저장본: " + _selected.DisplayName
                + (visible.Any(entry => entry.AssetPath == _selected.AssetPath) ? string.Empty : "\n현재 목록 밖에 있는 선택 항목입니다.")
                : "저장본을 선택하세요.";
            _selectionControls.SetEnabled(_selected != null && !EditorApplication.isPlayingOrWillChangePlaymode);
            UpdatePlaybackStatus();
        }

        private void Select(BattleVfxSavedEffectLibrary.Entry entry)
        {
            _preview.Stop();
            _selected = entry;
            RenderList();
        }

        private void ChangePage(int direction)
        {
            _pageIndex += direction;
            RenderList();
        }

        private void ListKeyDown(KeyDownEvent key)
        {
            if (key.keyCode != UnityEngine.KeyCode.PageUp && key.keyCode != UnityEngine.KeyCode.PageDown)
            {
                return;
            }
            VisualElement focused = _root.panel?.focusController.focusedElement as VisualElement
                ?? key.target as VisualElement;
            while (focused != null && focused != _root)
            {
                if (focused is TextField || focused is FloatField || focused is IntegerField)
                {
                    return;
                }
                focused = focused.parent;
            }
            ChangePage(key.keyCode == UnityEngine.KeyCode.PageDown ? 1 : -1);
            key.StopPropagation();
        }

        private void Play()
        {
            try
            {
                _preview.Play(_selected, ((BattleVfxSavedEffects)target).gameObject.scene);
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    sceneView.Frame(new UnityEngine.Bounds(_preview.Instance.transform.position, UnityEngine.Vector3.one * 5f), false);
                }
                UpdatePlaybackStatus();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                EditorUtility.DisplayDialog("저장된 이펙트 재생", exception.Message, "확인");
            }
        }

        private void DeleteSelected()
        {
            if (_selected == null || !EditorUtility.DisplayDialog("저장된 이펙트 삭제",
                _selected.DisplayName + "\n\n선택한 이펙트 저장본을 휴지통으로 옮깁니다.", "삭제", "취소"))
            {
                return;
            }
            try
            {
                _preview.Stop();
                BattleVfxSavedEffectLibrary.Delete(_selected.Prefab);
                _selected = null;
                Refresh();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                EditorUtility.DisplayDialog("저장된 이펙트 삭제", exception.Message, "확인");
            }
        }

        private void UpdatePlaybackStatus()
        {
            if (_status == null || _preview == null)
            {
                return;
            }
            bool hasPreview = _preview.Instance != null;
            _timeline.SetEnabled(hasPreview);
            _timeline.highValue = _preview.Duration;
            _timeline.SetValueWithoutNotify(_preview.Time);
            _status.text = hasPreview ? (_preview.IsPlaying ? "재생 중" : _preview.Time >= _preview.Duration ? "재생 완료" : "일시정지") + " · "
                + _preview.Time.ToString("0.00") + " / " + _preview.Duration.ToString("0.00")
                + "초 · 파티클 " + _preview.ParticleCount + "개" : "재생할 저장본을 선택하고 재생 버튼을 누르세요.";
        }

        private void PlayModeChanged(PlayModeStateChange state)
        {
            Refresh();
        }
    }
}
