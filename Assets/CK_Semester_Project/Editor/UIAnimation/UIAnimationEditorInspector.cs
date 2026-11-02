using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation.EditorTools
{
    // 디자이너가 쓰는 화면이다. 생성 전에는 생성 화면을, 생성 뒤에는 상태 애니메이션과 마우스 반응 탭을 보여 준다.
    // 값을 바꾸면 그 자리에서 .anim에 구워지고, 잠시 뒤 저장된다.
    [CustomEditor(typeof(UIAnimationEditor))]
    public sealed class UIAnimationEditorInspector : UnityEditor.Editor
    {
        private enum Page
        {
            States,
            Input,
            Directing
        }

        private const double SaveDelaySeconds = 0.6;
        private const float SaveButtonHeight = 28f;
        private const float CurveRowHeight = 42f;
        private const float EventToggleWidth = 76f;
        private const float TimelineRowHeight = 14f;
        private const float TimelineLabelWidth = 120f;
        private const float MaxBrightness = 3f;
        private const string InputGuide =
            "버튼처럼 조작하는 Image에만 켜세요. 패널처럼 보여 주기만 하는 Image는 꺼 두면 됩니다.";
        private const string TriggerGuide =
            "+를 눌러 오브젝트를 끌어다 놓고 실행할 함수를 고릅니다. 이 Image에만 적용되고 플레이 중에 실행됩니다.";

        private static readonly string[] PageNames = { "상태 애니메이션", "입력 반응", "연출·연결" };
        private static readonly string[] RateNames = { "1배", "0.5배", "0.25배" };
        private static readonly float[] Rates = { 1f, 0.5f, 0.25f };
        private static readonly string[] StepNames = { "부드럽게", "24fps", "12fps", "8fps" };
        private static readonly int[] StepValues = { 0, 24, 12, 8 };
        private static readonly string[] WipeNames = { "왼쪽 → 오른쪽", "오른쪽 → 왼쪽", "아래 → 위", "위 → 아래" };
        private static readonly string[] PivotNames = { "↖", "↑", "↗", "←", "●", "→", "↙", "↓", "↘" };
        private static readonly string[] StateNames = { "Open (등장)", "Stay (유지)", "Close (퇴장)" };
        private static readonly string[] StateGuides =
        {
            "오브젝트가 켜지면 한 번 재생되고 Stay로 넘어갑니다.",
            "Open이 끝난 모습으로 머뭅니다. 값을 넣으면 왕복 반복합니다.",
            "Close()를 부르면 재생됩니다."
        };

        private static readonly (UIPointerEffect Effect, string Name, string Guide)[] MouseEvents =
        {
            (UIPointerEffect.Hover, "Hover", "마우스를 올리거나 키보드·패드로 골랐을 때"),
            (UIPointerEffect.Exit, "Exit", "벗어났을 때 (끄면 바로 원래대로)"),
            (UIPointerEffect.Pressed, "Pressed", "눌렀을 때")
        };

        private static readonly Color TimelineOpen = new Color(0.3f, 0.62f, 0.95f);
        private static readonly Color TimelineStay = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color TimelineClose = new Color(0.92f, 0.55f, 0.3f);

        private static readonly (UIAnimationStyle Style, string Label)[] Styles =
            Enum.GetValues(typeof(UIAnimationStyle)).Cast<UIAnimationStyle>()
                .Select(style => (style, UIAnimationPresetCatalog.Name(style))).ToArray();

        private static readonly (UIStayStyle Style, string Label)[] StayStyles =
        {
            (UIStayStyle.Still, "가만히 있기"),
            (UIStayStyle.Float, "둥실 떠 있기 (페르소나풍)"),
            (UIStayStyle.Pulse, "은은하게 깜빡이기")
        };

        private static readonly (UIPointerStyle Style, string Label)[] PointerStyles =
        {
            (UIPointerStyle.Glow, "밝아지기 (하이라이트)"),
            (UIPointerStyle.Grow, "살짝 커지기"),
            (UIPointerStyle.Nudge, "옆으로 밀리기 (목록 선택)"),
            (UIPointerStyle.Punch, "기울며 튀어나오기")
        };

        private SerializedProperty _image;
        private SerializedProperty _assetName;
        private SerializedProperty _folder;
        private SerializedProperty _mouseEvents;
        private SerializedProperty _startStyle;
        private SerializedProperty _startPreset;
        private SerializedObject _presetObject;
        private SerializedObject _playerObject;
        private SerializedObject _pointerObject;
        private UIAnimationPreset _preset;
        private UIAnimationPlayer _player;
        private UIAnimationPreviewer _previewer;
        private UIImageBrightness _effectBrightness;
        private Func<float, float> _effectBrightnessCurve;
        private Page _page;
        private UIAnimationState _state;
        private UIPointerEffect _effect;
        private int _previewRate;
        private UIAnimationState[] _previewStates;
        private bool _showSceneList;
        private bool _showTriggers;
        private float _staggerStep = 0.04f;
        private bool _staggerReverse;
        private double _saveAt = double.MaxValue;

        private void OnEnable()
        {
            _image = serializedObject.FindProperty("_image");
            _assetName = serializedObject.FindProperty("_assetName");
            _folder = serializedObject.FindProperty("_folder");
            _mouseEvents = serializedObject.FindProperty("_mouseEvents");
            _startStyle = serializedObject.FindProperty("_startStyle");
            _startPreset = serializedObject.FindProperty("_startPreset");
            _previewer = new UIAnimationPreviewer();
            _previewer.Stopped += ClearEffectBrightness;
            _previewer.Stopped += Repaint;
            _previewer.Ticked += UpdateEffectBrightness;
            _previewer.Ticked += Repaint;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += SaveWhenDue;
        }

        private void OnDisable()
        {
            _previewer.Stop();
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= SaveWhenDue;
            SavePending();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_image, new GUIContent("대상 Image", "애니메이션을 넣을 UI Image"));
            if (EditorGUI.EndChangeCheck())
            {
                OnImageChanged(_image.objectReferenceValue as Image);
            }

            var image = _image.objectReferenceValue as Image;
            Bind(image);
            if (image == null)
            {
                EditorGUILayout.HelpBox("Hierarchy의 Image를 위 칸에 끌어다 놓으세요.", MessageType.Info);
            }
            else if (_preset == null)
            {
                DrawCreatePage(image);
            }
            serializedObject.ApplyModifiedProperties();

            if (image != null && _preset != null)
            {
                DrawEditPages(image);
            }
            DrawSceneList(image);
        }

        private void OnImageChanged(Image image)
        {
            _previewer.Stop();
            GUI.FocusControl(null);
            _assetName.stringValue = image != null ? UIAnimationGenerator.SuggestName(image) : string.Empty;
            _mouseEvents.intValue = image != null ? (int)UIAnimationGenerator.SuggestEffects(image) : 0;
            _page = Page.States;
            _state = UIAnimationState.Open;
            _effect = UIPointerEffect.Hover;
        }

        private void Bind(Image image)
        {
            UIAnimationPlayer player = image != null ? image.GetComponent<UIAnimationPlayer>() : null;
            UIAnimationPreset preset = player != null ? player.Preset : null;
            // 스크립트를 다시 컴파일하면 _preset만 남고 _presetObject는 비어 있다.
            if (preset == _preset && (preset == null || (player == _player && _presetObject != null)))
            {
                return;
            }
            _previewer.Stop();
            SavePending();
            _preset = preset;
            _player = preset != null ? player : null;
            _presetObject = preset != null ? new SerializedObject(preset) : null;
            _playerObject = preset != null ? new SerializedObject(player) : null;
        }

        private static EditorGUILayout.VerticalScope Group(string title)
        {
            var group = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            return group;
        }

        private static bool DrawEventToggle(string name, string guide, bool value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.ToggleLeft(name, value, GUILayout.Width(EventToggleWidth));
                EditorGUILayout.LabelField(guide, EditorStyles.miniLabel);
            }
            return value;
        }

        // 입력 칸에 포커스가 남아 있으면 화면이 바뀌어도 이전 글자가 그대로 보인다.
        private static void Select<T>(ref T current, T picked)
        {
            if (!EqualityComparer<T>.Default.Equals(current, picked))
            {
                current = picked;
                GUI.FocusControl(null);
            }
        }

        // 바뀌지 않은 값까지 쓰면 매 프레임 수정으로 잡혀 클립을 계속 다시 굽게 된다.
        private static void SetIfChanged(SerializedProperty flag, bool value)
        {
            if (flag.boolValue != value)
            {
                flag.boolValue = value;
            }
        }

        private void DrawCreatePage(Image image)
        {
            string folder;
            string name;
            bool validFolder;
            using (Group("파일"))
            {
                EditorGUILayout.PropertyField(_assetName, new GUIContent("이름", "기본값은 Image 오브젝트 이름입니다."));
                DrawFolderField();
                folder = _folder.stringValue.TrimEnd('/');
                name = UIAnimationGenerator.CleanName(_assetName.stringValue);
                validFolder = UIAnimationGenerator.IsProjectFolder(folder);
                if (!validFolder)
                {
                    EditorGUILayout.HelpBox("폴더는 Assets 아래여야 합니다.", MessageType.Error);
                }
                else if (name.Length > 0)
                {
                    folder = UIAnimationGenerator.FolderFor(image, folder);
                    foreach (string path in UIAnimationGenerator.AssetPaths(folder, name))
                    {
                        EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                    }
                }
            }

            using (Group("시작 프리셋"))
            {
                EditorGUILayout.LabelField("생성할 때 넣을 움직임입니다. 만든 뒤에도 바꿀 수 있습니다.", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(StartPresetLabel() + " ▾"))
                {
                    ShowStartPresetMenu();
                }
            }

            using (Group("입력 반응 (선택)"))
            {
                EditorGUILayout.LabelField(InputGuide, EditorStyles.wordWrappedMiniLabel);
                foreach ((UIPointerEffect effect, string label, string guide) in MouseEvents)
                {
                    int bit = 1 << (int)effect;
                    bool on = (_mouseEvents.intValue & bit) != 0;
                    if (DrawEventToggle(label, guide, on) != on)
                    {
                        _mouseEvents.intValue ^= bit;
                    }
                }
            }

            bool inScene = !EditorUtility.IsPersistent(image);
            if (!inScene)
            {
                EditorGUILayout.HelpBox("프리팹 파일이 아니라 씬에 있는 Image를 넣어 주세요.", MessageType.Warning);
            }
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이를 멈춘 뒤 생성하세요.", MessageType.Info);
            }
            using (new EditorGUI.DisabledScope(Application.isPlaying || !inScene || !validFolder || name.Length == 0))
            {
                if (GUILayout.Button("생성", GUILayout.Height(32f)))
                {
                    GUI.FocusControl(null);
                    serializedObject.ApplyModifiedProperties();
                    var pointerEffects = (UIPointerEffectMask)_mouseEvents.intValue;
                    Create(image, folder, name, pointerEffects, StartSetup(pointerEffects));
                    GUIUtility.ExitGUI();
                }
            }
            DrawConnectExisting(image);
        }

        private string StartPresetLabel()
        {
            if (_startPreset.objectReferenceValue != null)
            {
                return "저장한 프리셋/" + _startPreset.objectReferenceValue.name;
            }
            var style = (UIAnimationStyle)_startStyle.enumValueIndex;
            return Styles.First(entry => entry.Style == style).Label;
        }

        private void ShowStartPresetMenu()
        {
            var picked = _startPreset.objectReferenceValue as UIAnimationPreset;
            var menu = new GenericMenu();
            foreach ((UIAnimationStyle style, string label) in Styles)
            {
                bool on = picked == null && style == (UIAnimationStyle)_startStyle.enumValueIndex;
                menu.AddItem(new GUIContent(label), on, () => PickStartPreset(style, null));
            }
            foreach (UIAnimationPreset saved in UIAnimationGenerator.SavedPresets())
            {
                menu.AddItem(new GUIContent("저장한 프리셋/" + saved.name), saved == picked, () => PickStartPreset(default, saved));
            }
            menu.ShowAsContext();
        }

        private void PickStartPreset(UIAnimationStyle style, UIAnimationPreset saved)
        {
            serializedObject.Update();
            _startStyle.enumValueIndex = (int)style;
            _startPreset.objectReferenceValue = saved;
            serializedObject.ApplyModifiedProperties();
        }

        private Action<UIAnimationPreset> StartSetup(UIPointerEffectMask pointerEffects)
        {
            var style = (UIAnimationStyle)_startStyle.enumValueIndex;
            var saved = _startPreset.objectReferenceValue as UIAnimationPreset;
            if (saved == null)
            {
                return preset => preset.ApplyStyle(style);
            }
            return preset =>
            {
                preset.CopyMotionFrom(saved);
                preset.SetPointerEffects(pointerEffects);
            };
        }

        private void DrawFolderField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_folder, new GUIContent("저장 폴더"));
                if (GUILayout.Button("찾기", GUILayout.Width(44f)))
                {
                    string picked = EditorUtility.OpenFolderPanel("저장 폴더", _folder.stringValue, string.Empty);
                    if (picked.StartsWith(Application.dataPath, StringComparison.Ordinal))
                    {
                        _folder.stringValue = "Assets" + picked.Substring(Application.dataPath.Length);
                    }
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private static void Create(
            Image image, string folder, string name, UIPointerEffectMask pointerEffects, Action<UIAnimationPreset> setup)
        {
            Animator animator = image.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null
                && !EditorUtility.DisplayDialog(
                    "기존 Animator",
                    $"{image.name}에는 이미 '{animator.runtimeAnimatorController.name}' Animator가 있습니다. 새 애니메이션으로 교체할까요?",
                    "교체", "취소"))
            {
                return;
            }

            if (!UIAnimationGenerator.IsNameFree(folder, name))
            {
                UIAnimationPreset existing = UIAnimationGenerator.FindPreset(folder, name);
                string unique = UIAnimationGenerator.UniqueName(folder, name);
                if (existing != null)
                {
                    int choice = EditorUtility.DisplayDialogComplex(
                        "이미 있는 이름",
                        $"'{name}' 애니메이션이 이미 있습니다. 함께 쓰면 값을 고칠 때 같이 바뀝니다.",
                        "함께 쓰기", "취소", $"새로 만들기 ({unique})");
                    if (choice == 0)
                    {
                        UIAnimationGenerator.Connect(image, existing);
                    }
                    if (choice != 2)
                    {
                        return;
                    }
                }
                else if (!EditorUtility.DisplayDialog(
                    "이미 있는 파일",
                    $"같은 이름의 파일이 있어 덮어쓰지 않습니다. '{unique}' 이름으로 새로 만들까요?",
                    "새로 만들기", "취소"))
                {
                    return;
                }
                name = unique;
            }
            UIAnimationGenerator.Create(image, folder, name, pointerEffects, setup);
        }

        private static void DrawConnectExisting(Image image)
        {
            EditorGUILayout.Space(4f);
            if (!GUILayout.Button("기존 애니메이션 함께 쓰기 ▾"))
            {
                return;
            }

            var menu = new GenericMenu();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController"))
            {
                UIAnimationPreset preset = UIAnimationGenerator.PresetAt(AssetDatabase.GUIDToAssetPath(guid));
                if (preset != null)
                {
                    menu.AddItem(new GUIContent(preset.name), false, () => UIAnimationGenerator.Connect(image, preset));
                }
            }
            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("아직 만든 애니메이션이 없습니다"));
            }
            menu.ShowAsContext();
        }

        private void DrawEditPages(Image image)
        {
            _presetObject.Update();
            _playerObject.Update();

            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(_preset), EditorStyles.miniLabel);
            DrawConnectionWarning(image);
            EditorGUILayout.Space(4f);
            DrawSaveBar();
            EditorGUILayout.Space(4f);
            Select(ref _page, (Page)GUILayout.Toolbar((int)_page, PageNames, GUILayout.Height(24f)));
            EditorGUILayout.Space(4f);
            switch (_page)
            {
                case Page.States:
                    DrawStatesPage();
                    break;
                case Page.Input:
                    DrawInputPage(image);
                    break;
                default:
                    DrawDirectingPage(image);
                    break;
            }

            bool playerChanged = _playerObject.ApplyModifiedProperties();
            bool presetChanged = _presetObject.hasModifiedProperties;
            if (presetChanged || playerChanged)
            {
                MarkManualChange();
                _presetObject.ApplyModifiedProperties();
                ApplyChanges();
            }
        }

        private void DrawStatesPage()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Select(ref _state, (UIAnimationState)EditorGUILayout.Popup("상태 선택", (int)_state, StateNames));
                EditorGUILayout.LabelField(StateGuides[(int)_state], EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("프리셋 넣기 ▾"))
                    {
                        ShowStyleMenu();
                    }
                    if (_state != UIAnimationState.Stay && GUILayout.Button("Close를 Open 반대로"))
                    {
                        ChangePreset("Close 자동 생성", preset => { preset.MirrorOpenToClose(); preset.MarkStatePresetEdited(); });
                    }
                }
                bool stay = _state == UIAnimationState.Stay;
                EditorGUILayout.LabelField(stay ? _preset.StayPresetLabel : _preset.StatePresetLabel, EditorStyles.wordWrappedMiniLabel);
                if (!stay && !string.IsNullOrEmpty(_preset.StatePresetDescription))
                    EditorGUILayout.LabelField(_preset.StatePresetDescription, EditorStyles.wordWrappedMiniLabel);
            }
            DrawState(_state);
            DrawPreviewControls();
        }

        private void ShowStyleMenu()
        {
            var menu = new GenericMenu();
            if (_state == UIAnimationState.Stay)
            {
                foreach ((UIStayStyle style, string label) in StayStyles)
                {
                    menu.AddItem(new GUIContent(label), _preset.StayPresetName == UIAnimationPresetCatalog.Name(style),
                        () => ChangePreset("Stay 프리셋", preset => preset.ApplyStayStyle(style)));
                }
            }
            else
            {
                foreach ((UIAnimationStyle style, string label) in Styles)
                {
                    menu.AddItem(new GUIContent(label), _preset.StatePresetName == label,
                        () => ChangePreset("UI 애니메이션 프리셋", preset => preset.ApplyStyle(style)));
                }
            }
            menu.ShowAsContext();
        }

        private void ChangePreset(string undoName, Action<UIAnimationPreset> change)
        {
            Undo.RecordObject(_preset, undoName);
            change(_preset);
            EditorUtility.SetDirty(_preset);
            _presetObject.Update();
            ApplyChanges();
        }

        private void DrawState(UIAnimationState state)
        {
            SerializedProperty settings = _presetObject.FindProperty(StateField(state));
            UIAnimationStateSettings values = _preset.Get(state);
            UIAnimationSpan span = _preset.GetSpan(state);
            bool isOpen = state == UIAnimationState.Open;
            bool isStay = state == UIAnimationState.Stay;

            using (Group("재생"))
            {
                if (!isStay)
                {
                    EditorGUILayout.PropertyField(
                        _playerObject.FindProperty(isOpen ? "_openDelay" : "_closeDelay"),
                        new GUIContent("Delay (시작 지연, 초)", "이 Image만 늦게 시작합니다. 같은 애니메이션을 함께 써도 Image마다 따로 정합니다."));
                }
                DrawTiming(settings, DescribeLength(state, values));
                DrawSteps(settings.FindPropertyRelative("_stepsPerSecond"));
                if (isStay)
                {
                    EditorGUILayout.PropertyField(
                        settings.FindPropertyRelative("_autoClose"),
                        new GUIContent("Time 뒤 자동 Close", "끄면 Close()를 부를 때까지 Stay에 머뭅니다."));
                }
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("_sound"), new GUIContent("Sound (효과음)"));
            }

            string toLabel = isStay ? "왕복 목표" : "끝";
            DrawVectorTrack(settings.FindPropertyRelative("_offset"), values.Offset.Curve, "위치  Position (px, 원래 자리 = 0)", toLabel, isOpen, span.Offset.From);
            DrawVectorTrack(settings.FindPropertyRelative("_scale"), values.Scale.Curve, "크기  Scale", toLabel, isOpen, span.Scale.From);
            DrawFloatTrack(settings.FindPropertyRelative("_rotation"), values.Rotation.Curve, "회전  Rotation (도)", toLabel, isOpen, span.Rotation.From);
            DrawColorTrack(settings.FindPropertyRelative("_color"), values.Color.Curve, toLabel, isOpen, span.Color.From);
            DrawWipeTrack(settings.FindPropertyRelative("_wipe"), values.Wipe.Curve, toLabel, isOpen, span.Wipe.From);

            if (!isStay)
            {
                using (Group("끝난 뒤"))
                {
                    SerializedProperty keepEnabled = settings.FindPropertyRelative("_keepEnabled");
                    EditorGUILayout.PropertyField(keepEnabled, new GUIContent("Enable (켜 둠)"));
                    string guide = keepEnabled.boolValue ? "끝나도 오브젝트가 켜져 있습니다." : "끝나면 오브젝트가 꺼집니다.";
                    EditorGUILayout.LabelField(guide, EditorStyles.miniLabel);
                }
                DrawTriggers(_playerObject,
                    (isOpen ? "_openStarted" : "_closeStarted", "시작할 때"),
                    (isOpen ? "_openFinished" : "_closeFinished", "끝났을 때"));
            }
        }

        // 연결이 없을 때는 화면이 길어지지 않게 접어 둔다.
        private void DrawTriggers(SerializedObject target, params (string Field, string Label)[] events)
        {
            int connected = events.Sum(entry => target.FindProperty(entry.Field).FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string title = connected > 0 ? $"트리거 ({connected}개 연결됨)" : "트리거 (선택)";
                _showTriggers = EditorGUILayout.Foldout(_showTriggers, title, true);
                if (!_showTriggers)
                {
                    return;
                }
                EditorGUILayout.LabelField(TriggerGuide, EditorStyles.wordWrappedMiniLabel);
                foreach ((string field, string label) in events)
                {
                    EditorGUILayout.PropertyField(target.FindProperty(field), new GUIContent(label));
                }
            }
        }

        private string DescribeLength(UIAnimationState state, UIAnimationStateSettings values)
        {
            string seconds = $"{values.Time / values.Speed:0.##}초";
            if (state != UIAnimationState.Stay)
            {
                return seconds;
            }
            if (values.AutoClose)
            {
                return $"{seconds} 뒤 Close";
            }
            return _preset.IsStayStatic ? "Close를 부를 때까지 유지" : $"왕복 1회 {seconds}";
        }

        private void DrawInputPage(Image image)
        {
            var available = new List<UIPointerEffect>();
            using (Group("만들 이벤트"))
            {
                EditorGUILayout.LabelField(InputGuide, EditorStyles.wordWrappedMiniLabel);
                foreach ((UIPointerEffect effect, string label, string guide) in MouseEvents)
                {
                    SerializedProperty enabled = _presetObject.FindProperty(EffectField(effect)).FindPropertyRelative("_enabled");
                    SetIfChanged(enabled, DrawEventToggle(label, guide, enabled.boolValue));
                    if (enabled.boolValue)
                    {
                        available.Add(effect);
                    }
                }
            }
            DrawPointerPresetButton();
            if (available.Count == 0)
            {
                return;
            }

            DrawPointerWarnings(image);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(
                    _presetObject.FindProperty("_reactToSelection"),
                    new GUIContent("키보드·패드에도 반응", "방향키로 고르면 Hover, Enter로 누르면 Pressed가 재생됩니다."));
                string[] names = available.Select(effect => MouseEvents[(int)effect].Name).ToArray();
                int selected = EditorGUILayout.Popup("이벤트 선택", Mathf.Max(0, available.IndexOf(_effect)), names);
                Select(ref _effect, available[selected]);
                EditorGUILayout.LabelField(MouseEvents[(int)_effect].Guide, EditorStyles.wordWrappedMiniLabel);
            }
            DrawEffect(image, _effect);
            DrawEffectButtons(image);
            DrawEffectTrigger(image);
        }

        private void ShowPointerStyleMenu()
        {
            var menu = new GenericMenu();
            foreach ((UIPointerStyle style, string label) in PointerStyles)
            {
                menu.AddItem(new GUIContent(label), _preset.PointerPresetName == UIAnimationPresetCatalog.Name(style),
                    () => ChangePreset("입력 반응 프리셋", preset => preset.ApplyPointerStyle(style)));
            }
            menu.ShowAsContext();
        }

        private void DrawPointerPresetButton()
        {
            if (GUILayout.Button("반응 프리셋 넣기 ▾")) ShowPointerStyleMenu();
            EditorGUILayout.LabelField(_preset.PointerPresetLabel, EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawEffect(Image image, UIPointerEffect effect)
        {
            SerializedProperty settings = _presetObject.FindProperty(EffectField(effect));
            UIPointerEffectSettings values = _preset.Get(effect);

            using (Group("재생"))
            {
                DrawTiming(settings, $"{values.Duration:0.##}초");
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("_sound"), new GUIContent("Sound (효과음)"));
            }

            EditorGUILayout.LabelField("애니메이션", EditorStyles.boldLabel);
            DrawVectorTrack(settings.FindPropertyRelative("_offset"), values.Offset.Curve, "위치  Position (px)", "이동량", false, null);
            DrawVectorTrack(settings.FindPropertyRelative("_scale"), values.Scale.Curve, "크기  Scale", "배율 (1 = 원래 크기)", false, null);
            DrawFloatTrack(settings.FindPropertyRelative("_rotation"), values.Rotation.Curve, "회전  Rotation (도)", "회전량", false, null);

            EditorGUILayout.LabelField("하이라이트", EditorStyles.boldLabel);
            if (effect == UIPointerEffect.Exit)
            {
                EditorGUILayout.LabelField("Exit에서는 하이라이트가 꺼지고 원래 이미지와 색으로 돌아갑니다.", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            DrawHighlightImage(settings, image, effect);
            DrawHighlightColor(settings, values.Color.Curve);
        }

        private void DrawHighlightImage(SerializedProperty settings, Image image, UIPointerEffect effect)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("하이라이트 이미지  Sprite", EditorStyles.boldLabel);
                SerializedProperty sprite = settings.FindPropertyRelative("_sprite");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(sprite, new GUIContent("바꿀 이미지"));
                    if (GUILayout.Button("자동 찾기", GUILayout.Width(64f)))
                    {
                        FindHighlightSprite(sprite, image);
                    }
                }
                string guide = effect == UIPointerEffect.Pressed
                    ? "비워 두면 Hover 이미지를 유지합니다."
                    : "비워 두면 이미지를 바꾸지 않습니다. Exit에서 원래 이미지로 돌아갑니다.";
                EditorGUILayout.LabelField(guide, EditorStyles.miniLabel);
            }
        }

        private static void FindHighlightSprite(SerializedProperty sprite, Image image)
        {
            Sprite found = UIAnimationGenerator.SuggestHighlight(image);
            if (found != null)
            {
                sprite.objectReferenceValue = found;
                return;
            }
            EditorUtility.DisplayDialog(
                "하이라이트 이미지",
                "찾지 못했습니다. Button의 Sprite State와, 같은 폴더에서 Default를 Selected·Highlight·Hover로 바꾼 이름을 찾습니다.",
                "확인");
        }

        private void DrawHighlightColor(SerializedProperty settings, UIEaseCurve curve)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty track = settings.FindPropertyRelative("_color");
                SerializedProperty use = track.FindPropertyRelative("_use");
                SetIfChanged(use, EditorGUILayout.ToggleLeft("하이라이트 색  Highlight Color", use.boolValue, EditorStyles.boldLabel));
                if (!use.boolValue)
                {
                    return;
                }
                EditorGUILayout.LabelField("Image 색에 곱합니다. 흰색은 원래 색이고, 밝기를 1보다 올리면 밝아집니다.", EditorStyles.wordWrappedMiniLabel);
                DrawColor(track.FindPropertyRelative("_to"), "색");
                EditorGUILayout.Slider(settings.FindPropertyRelative("_brightness"), 0f, MaxBrightness, new GUIContent("밝기", "1 = 원래 밝기, 1.3 = 30% 밝게"));
                DrawCurve(track.FindPropertyRelative("_curve"), curve);
            }
        }

        private void DrawEffectTrigger(Image image)
        {
            UIAnimationPointerEffects effects = image.GetComponent<UIAnimationPointerEffects>();
            if (effects == null)
            {
                return;
            }
            if (_pointerObject == null || _pointerObject.targetObject != effects)
            {
                _pointerObject = new SerializedObject(effects);
            }
            _pointerObject.Update();
            DrawTriggers(_pointerObject, (TriggerField(_effect), $"{MouseEvents[(int)_effect].Name} 때 실행"));
            if (_pointerObject.ApplyModifiedProperties()) MarkManualChange();
        }

        private void DrawDirectingPage(Image image)
        {
            DrawStaggerGroup();
            DrawButtonGroup();
            DrawPivotGroup(image.rectTransform);
            DrawPresetGroup();
        }

        private void DrawStaggerGroup()
        {
            using (Group("순차 등장  Stagger"))
            {
                EditorGUILayout.LabelField(
                    "자식 Image가 차례로 나오고 반대 순서로 들어갑니다. 메뉴나 스킬 목록처럼 여러 개가 줄지어 나올 때 씁니다.",
                    EditorStyles.wordWrappedMiniLabel);

                if (GUILayout.Button("자식 Image에 같은 애니메이션 넣기 ▾"))
                {
                    ShowShareMenu();
                }
                List<UIAnimationPlayer> children = UIAnimationGenerator.ChildPlayers(_player);
                if (children.Count == 0)
                {
                    EditorGUILayout.LabelField("애니메이션이 있는 자식 Image가 없습니다.", EditorStyles.miniLabel);
                    return;
                }

                _staggerStep = Mathf.Max(0f, EditorGUILayout.FloatField("간격 (초)", _staggerStep));
                _staggerReverse = EditorGUILayout.Toggle("아래부터 나오기", _staggerReverse);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"자식 {children.Count}개에 적용"))
                    {
                        UIAnimationGenerator.ApplyStagger(_player, _staggerStep, _staggerReverse);
                        _playerObject.Update();
                    }
                    using (new EditorGUI.DisabledScope(_player.Followers.Count == 0))
                    {
                        if (GUILayout.Button("해제"))
                        {
                            UIAnimationGenerator.ClearStagger(_player);
                            _playerObject.Update();
                        }
                    }
                }
                foreach (UIAnimationPlayer child in children)
                {
                    EditorGUILayout.LabelField(child.name, $"{child.OpenDelay:0.##}초 뒤 등장", EditorStyles.miniLabel);
                }
            }
        }

        private void ShowShareMenu()
        {
            var menu = new GenericMenu();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController"))
            {
                UIAnimationPreset preset = UIAnimationGenerator.PresetAt(AssetDatabase.GUIDToAssetPath(guid));
                if (preset != null)
                {
                    menu.AddItem(new GUIContent(preset.name), false, () => UIAnimationGenerator.ShareWithChildren(_player, preset));
                }
            }
            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("아직 만든 애니메이션이 없습니다"));
            }
            menu.ShowAsContext();
        }

        private void DrawButtonGroup()
        {
            using (Group("버튼 연결"))
            {
                EditorGUILayout.LabelField("Button을 끌어다 놓으면 누를 때 이 Image가 열리거나 닫힙니다.", EditorStyles.wordWrappedMiniLabel);
                DrawButtonSlot("여는 버튼", nameof(UIAnimationPlayer.Open));
                DrawButtonSlot("닫는 버튼", nameof(UIAnimationPlayer.Close));
            }
        }

        private void DrawButtonSlot(string label, string method)
        {
            foreach (Button button in UIAnimationGenerator.ButtonsCalling(_player, method))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField(label, button, typeof(Button), true);
                    }
                    if (GUILayout.Button("빼기", GUILayout.Width(44f)))
                    {
                        UIAnimationGenerator.DisconnectButton(button, _player, method);
                        GUIUtility.ExitGUI();
                    }
                }
            }
            var added = EditorGUILayout.ObjectField(label + " 추가", null, typeof(Button), true) as Button;
            if (added != null)
            {
                UIAnimationGenerator.ConnectButton(added, _player, method);
            }
        }

        private static void DrawPivotGroup(RectTransform rect)
        {
            using (Group("기준점  Pivot"))
            {
                EditorGUILayout.LabelField("크기와 회전이 시작되는 지점입니다. 화면의 자리는 그대로 유지됩니다.", EditorStyles.wordWrappedMiniLabel);
                int current = Array.FindIndex(PivotValues(), value => value == rect.pivot);
                int picked = GUILayout.SelectionGrid(current, PivotNames, 3, GUILayout.Width(114f), GUILayout.Height(66f));
                if (picked != current)
                {
                    UIAnimationGenerator.SetPivot(rect, PivotValues()[picked]);
                }
            }
        }

        private static Vector2[] PivotValues()
        {
            return new[]
            {
                new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f)
            };
        }

        private void DrawPresetGroup()
        {
            using (Group("프리셋 저장·불러오기"))
            {
                EditorGUILayout.LabelField("마음에 드는 설정을 저장해 두고 다른 Image에 불러옵니다.", EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("지금 설정 저장"))
                    {
                        SavePreset();
                        GUIUtility.ExitGUI();
                    }
                    if (GUILayout.Button("불러오기 ▾"))
                    {
                        ShowLoadMenu();
                    }
                }
            }
        }

        private void SavePreset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "프리셋 저장", _preset.name, "asset", "프리셋 이름을 정하세요.", UIAnimationGenerator.PresetFolder);
            if (!string.IsNullOrEmpty(path))
            {
                UIAnimationGenerator.SaveAsPreset(_preset, path);
            }
        }

        private void ShowLoadMenu()
        {
            var menu = new GenericMenu();
            foreach (UIAnimationPreset saved in UIAnimationGenerator.SavedPresets())
            {
                menu.AddItem(new GUIContent(saved.name), _preset.StatePresetName == saved.name,
                    () => ChangePreset("프리셋 불러오기", preset => preset.CopyMotionFrom(saved)));
            }
            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("저장한 프리셋이 없습니다"));
            }
            menu.ShowAsContext();
        }

        private static void DrawTiming(SerializedProperty settings, string totalSeconds)
        {
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("_time"), new GUIContent("Time (길이, 초)", "애니메이션 길이"));
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("_speed"), new GUIContent("Speed (배속)", "2로 두면 두 배 빠르게 재생합니다."));
            EditorGUILayout.LabelField("실제 재생 시간", totalSeconds);
        }

        private static void DrawSteps(SerializedProperty steps)
        {
            int picked = EditorGUILayout.IntPopup(
                new GUIContent("움직임", "fps를 고르면 끊어 재생해 애니메이션 작화 같은 느낌을 냅니다."),
                steps.intValue, StepNames.Select(name => new GUIContent(name)).ToArray(), StepValues);
            if (picked != steps.intValue)
            {
                steps.intValue = picked;
            }
        }

        private void DrawVectorTrack(SerializedProperty track, UIEaseCurve curve, string title, string toLabel, bool editableFrom, Vector2? shownFrom)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty use = track.FindPropertyRelative("_use");
                SetIfChanged(use, EditorGUILayout.ToggleLeft(title, use.boolValue, EditorStyles.boldLabel));
                if (!use.boolValue)
                {
                    return;
                }

                if (editableFrom)
                {
                    EditorGUILayout.PropertyField(track.FindPropertyRelative("_from"), new GUIContent("시작"));
                }
                else if (shownFrom.HasValue)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.Vector2Field(new GUIContent("시작 (자동)", "이전 상태가 끝난 값에서 이어집니다."), shownFrom.Value);
                    }
                }
                EditorGUILayout.PropertyField(track.FindPropertyRelative("_to"), new GUIContent(toLabel));
                SerializedProperty split = track.FindPropertyRelative("_splitY");
                // 곡선을 나눈 프리셋에서만 보인다. 끄면 X 곡선 하나로 합쳐진다.
                if (split.boolValue)
                {
                    SetIfChanged(split, EditorGUILayout.ToggleLeft("X / Y 곡선 분리", true));
                }
                DrawCurve(track.FindPropertyRelative("_curve"), curve, split.boolValue ? "Curve X" : "Curve");
                if (split.boolValue)
                    DrawCurve(track.FindPropertyRelative("_curveY"), null, "Curve Y");
            }
        }

        private void DrawFloatTrack(SerializedProperty track, UIEaseCurve curve, string title, string toLabel, bool editableFrom, float? shownFrom)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty use = track.FindPropertyRelative("_use");
                SetIfChanged(use, EditorGUILayout.ToggleLeft(title, use.boolValue, EditorStyles.boldLabel));
                if (!use.boolValue)
                {
                    return;
                }
                DrawFloatRange(track, curve, toLabel, editableFrom, shownFrom);
            }
        }

        private void DrawWipeTrack(SerializedProperty track, UIEaseCurve curve, string toLabel, bool editableFrom, float? shownFrom)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty use = track.FindPropertyRelative("_use");
                SetIfChanged(use, EditorGUILayout.ToggleLeft("와이프  Wipe (0 = 안 보임, 1 = 전부)", use.boolValue, EditorStyles.boldLabel));
                if (!use.boolValue)
                {
                    return;
                }
                SerializedProperty direction = _presetObject.FindProperty("_wipeDirection");
                int picked = EditorGUILayout.Popup("방향", direction.enumValueIndex, WipeNames);
                if (picked != direction.enumValueIndex)
                {
                    direction.enumValueIndex = picked;
                }
                DrawFloatRange(track, curve, toLabel, editableFrom, shownFrom);
            }
        }

        private void DrawFloatRange(SerializedProperty track, UIEaseCurve curve, string toLabel, bool editableFrom, float? shownFrom)
        {
            if (editableFrom)
            {
                EditorGUILayout.PropertyField(track.FindPropertyRelative("_from"), new GUIContent("시작"));
            }
            else if (shownFrom.HasValue)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.FloatField(new GUIContent("시작 (자동)", "이전 상태가 끝난 값에서 이어집니다."), shownFrom.Value);
                }
            }
            EditorGUILayout.PropertyField(track.FindPropertyRelative("_to"), new GUIContent(toLabel));
            DrawCurve(track.FindPropertyRelative("_curve"), curve);
        }

        private void DrawColorTrack(SerializedProperty track, UIEaseCurve curve, string toLabel, bool editableFrom, Color? shownFrom)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty use = track.FindPropertyRelative("_use");
                SetIfChanged(use, EditorGUILayout.ToggleLeft("색·투명도  Color(Alpha)", use.boolValue, EditorStyles.boldLabel));
                if (!use.boolValue)
                {
                    return;
                }

                if (editableFrom)
                {
                    DrawColor(track.FindPropertyRelative("_from"), "시작");
                }
                else if (shownFrom.HasValue)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ColorField(new GUIContent("시작 (자동)", "이전 상태가 끝난 값에서 이어집니다."), shownFrom.Value);
                        EditorGUILayout.Slider("Alpha", shownFrom.Value.a, 0f, 1f);
                    }
                }
                DrawColor(track.FindPropertyRelative("_to"), toLabel);
                DrawCurve(track.FindPropertyRelative("_curve"), curve);
                EditorGUILayout.PropertyField(
                    _presetObject.FindProperty("_fadeChildren"),
                    new GUIContent("자식까지 같이 페이드", "켜면 알파를 CanvasGroup에 넣어 자식 Image와 글자도 함께 사라집니다."));
            }
        }

        // 색 칸의 작은 막대로는 투명도가 잘 안 보여 숫자로도 보여 준다.
        private static void DrawColor(SerializedProperty color, string label)
        {
            EditorGUILayout.PropertyField(color, new GUIContent(label));
            Color value = color.colorValue;
            EditorGUI.BeginChangeCheck();
            value.a = EditorGUILayout.Slider("Alpha", value.a, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                color.colorValue = value;
            }
        }

        private void DrawCurve(SerializedProperty curveProperty, UIEaseCurve curve, string title = "Curve")
        {
            SerializedProperty ease = curveProperty.FindPropertyRelative("_ease");
            var value = (UIEase)ease.enumValueIndex;
            Rect row = EditorGUILayout.GetControlRect(true, CurveRowHeight);
            Rect field = EditorGUI.PrefixLabel(row, new GUIContent(title));
            if (GUI.Button(field, GUIContent.none))
            {
                string path = curveProperty.propertyPath;
                PopupWindow.Show(field, new UIEasePicker(value, picked => SetEase(path, picked)));
            }
            if (Event.current.type == EventType.Repaint)
            {
                var graph = new Rect(field.x + 6f, field.y + 6f, 48f, field.height - 12f);
                AnimationCurve custom = curveProperty.FindPropertyRelative("_custom").animationCurveValue;
                UIEaseGraph.Draw(graph, t => value == UIEase.Custom ? custom.Evaluate(t) : UIEasing.Evaluate(value, t));
                var label = new Rect(graph.xMax + 8f, field.y, field.width - graph.width - 16f, field.height);
                GUI.Label(label, UIEaseGraph.Label(value));
            }
            if (value == UIEase.Custom)
            {
                EditorGUILayout.PropertyField(curveProperty.FindPropertyRelative("_custom"), new GUIContent("직접 그리기"));
            }
        }

        private void SetEase(string curvePath, UIEase ease)
        {
            if (_presetObject == null)
            {
                return;
            }
            _presetObject.Update();
            _presetObject.FindProperty(curvePath).FindPropertyRelative("_ease").enumValueIndex = (int)ease;
            MarkManualChange();
            _presetObject.ApplyModifiedProperties();
            ApplyChanges();
            Repaint();
        }

        private void MarkManualChange()
        {
            if (_page == Page.States)
                _presetObject.FindProperty("_statePresetEdited").stringValue = "세부 조정함";
            else if (_page == Page.Input)
                _presetObject.FindProperty("_pointerPresetEdited").stringValue = "세부 조정함";
        }

        private void DrawPreviewControls()
        {
            using (Group("미리보기"))
            {
                if (Application.isPlaying)
                {
                    DrawPlayModeButtons();
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"▶ {_state}"))
                    {
                        Preview(_state);
                    }
                    if (GUILayout.Button("▶ 전체 흐름"))
                    {
                        Preview(UIAnimationState.Open, UIAnimationState.Stay, UIAnimationState.Close);
                    }
                    using (new EditorGUI.DisabledScope(!_previewer.IsPlaying))
                    {
                        if (GUILayout.Button("■ 멈춤"))
                        {
                            _previewer.Stop();
                        }
                    }
                }
                DrawPreviewOptions();
                DrawTimeline();
            }
        }

        // 부모와 자식의 지연, Open, Stay, Close 길이를 막대로 보여 준다.
        private void DrawTimeline()
        {
            List<UIAnimationPlayer> players = TimelinePlayers();
            double closeAt = FlowCloseStart(players);
            double total = players.Max(player => closeAt + player.CloseDelay + player.Preset.Get(UIAnimationState.Close).Duration);
            Rect area = default;
            foreach (UIAnimationPlayer player in players)
            {
                Rect row = GUILayoutUtility.GetRect(0f, TimelineRowHeight, GUILayout.ExpandWidth(true));
                area = area == default ? row : Rect.MinMaxRect(row.x, area.y, row.xMax, row.yMax);
                if (Event.current.type == EventType.Repaint)
                {
                    DrawTimelineRow(row, player, closeAt, total);
                }
            }

            bool flowing = _previewer.IsPlaying && _previewStates != null && _previewStates.Length > 1;
            if (flowing && Event.current.type == EventType.Repaint)
            {
                float x = TimelineX(area, _previewer.Position, total);
                EditorGUI.DrawRect(new Rect(x, area.y, 1f, area.height), Color.white);
            }
        }

        private static void DrawTimelineRow(Rect row, UIAnimationPlayer player, double closeAt, double total)
        {
            UIAnimationPreset preset = player.Preset;
            GUI.Label(new Rect(row.x, row.y, TimelineLabelWidth, row.height), player.name, EditorStyles.miniLabel);
            double openEnd = OpenEnd(player);
            double closeStart = closeAt + player.CloseDelay;
            DrawTimelineBar(row, player.OpenDelay, openEnd, total, TimelineOpen);
            DrawTimelineBar(row, openEnd, closeStart, total, TimelineStay);
            DrawTimelineBar(row, closeStart, closeStart + preset.Get(UIAnimationState.Close).Duration, total, TimelineClose);
        }

        private static void DrawTimelineBar(Rect row, double start, double end, double total, Color color)
        {
            float left = TimelineX(row, start, total);
            float right = TimelineX(row, end, total);
            EditorGUI.DrawRect(new Rect(left, row.y + 3f, Mathf.Max(1f, right - left), row.height - 6f), color);
        }

        private static float TimelineX(Rect area, double time, double total)
        {
            float left = area.x + TimelineLabelWidth;
            return left + (area.xMax - left) * Mathf.Clamp01((float)(time / total));
        }

        private List<UIAnimationPlayer> TimelinePlayers()
        {
            var players = new List<UIAnimationPlayer> { _player };
            players.AddRange(_player.Followers.Where(follower => follower != null && follower.Preset != null));
            return players;
        }

        private static double FlowCloseStart(List<UIAnimationPlayer> players)
        {
            return players.Max(player => OpenEnd(player) + player.Preset.Get(UIAnimationState.Stay).Duration);
        }

        private void DrawPreviewOptions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _previewRate = EditorGUILayout.Popup(_previewRate, RateNames, GUILayout.Width(70f));
                _previewer.Rate = Rates[_previewRate];
                _previewer.Loop = EditorGUILayout.ToggleLeft("반복", _previewer.Loop, GUILayout.Width(60f));
            }
            if (!_previewer.IsPlaying)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            float position = EditorGUILayout.Slider("위치 (초)", (float)_previewer.Position, 0f, (float)_previewer.Duration);
            if (EditorGUI.EndChangeCheck())
            {
                _previewer.Seek(position);
            }
            if (_previewer.Paused && GUILayout.Button("이어서 재생"))
            {
                _previewer.Resume();
            }
        }

        private void DrawPlayModeButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open 실행"))
                {
                    _player.Open();
                }
                if (GUILayout.Button("Close 실행"))
                {
                    _player.Close();
                }
            }
        }

        private void DrawEffectButtons(Image image)
        {
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 중에는 게임 화면에서 마우스를 직접 올려 보세요.", MessageType.None);
            }
            else if (GUILayout.Button($"▶ {MouseEvents[(int)_effect].Name} 미리보기"))
            {
                PreviewEffect(image, _effect);
            }
        }

        // 자식 순차 등장까지 한 타임라인에 올려 게임과 같은 순서로 보여 준다.
        private void Preview(params UIAnimationState[] states)
        {
            _previewer.Stop();
            _previewStates = states;
            List<UIAnimationPlayer> players = TimelinePlayers();
            bool flows = states.Length > 1;
            double closeAt = flows ? FlowCloseStart(players) : 0.0;
            var entries = new List<UIAnimationPreviewer.Entry>();
            var clips = new List<AnimationClip>();
            foreach (UIAnimationPlayer player in players)
            {
                if (states.Contains(UIAnimationState.Open))
                {
                    AddEntry(entries, clips, player, UIAnimationState.Open, player.OpenDelay);
                }
                if (states.Contains(UIAnimationState.Stay))
                {
                    AddEntry(entries, clips, player, UIAnimationState.Stay, flows ? OpenEnd(player) : 0.0);
                }
                if (states.Contains(UIAnimationState.Close))
                {
                    AddEntry(entries, clips, player, UIAnimationState.Close, closeAt + player.CloseDelay);
                }
            }
            _previewer.Play(entries, clips);
        }

        private static double OpenEnd(UIAnimationPlayer player)
        {
            return player.OpenDelay + player.Preset.Get(UIAnimationState.Open).Duration;
        }

        private static void AddEntry(
            List<UIAnimationPreviewer.Entry> entries, List<AnimationClip> clips, UIAnimationPlayer player, UIAnimationState state, double start)
        {
            UIAnimationPreset preset = player.Preset;
            var clip = new AnimationClip { hideFlags = HideFlags.HideAndDontSave };
            UIAnimationClipBaker.BakePreview(preset, state, (RectTransform)player.transform, clip);
            clips.Add(clip);
            entries.Add(new UIAnimationPreviewer.Entry(player.gameObject, clip, start, preset.Get(state).Speed));
        }

        private void PreviewEffect(Image image, UIPointerEffect effect)
        {
            // 앞 미리보기 값이 남은 채로 구우면 배율이 겹친다.
            _previewer.Stop();
            _previewStates = null;
            var clip = new AnimationClip { hideFlags = HideFlags.HideAndDontSave };
            UIAnimationClipBaker.BakeEffectPreview(_preset, effect, image, clip);
            var entry = new UIAnimationPreviewer.Entry(image.gameObject, clip, 0.0, _preset.Get(effect).Speed);
            _effectBrightnessCurve = UIAnimationClipBaker.EffectBrightness(_preset, effect);
            _effectBrightness = _effectBrightnessCurve != null ? new UIImageBrightness(image) : null;
            _previewer.Play(new[] { entry }, new[] { clip });
        }

        private void UpdateEffectBrightness()
        {
            if (_effectBrightness != null)
            {
                _effectBrightness.Set(_effectBrightnessCurve(Mathf.Clamp01((float)(_previewer.Position / _previewer.Duration))));
            }
        }

        private void ClearEffectBrightness()
        {
            _effectBrightness?.Release();
            _effectBrightness = null;
        }

        private void DrawConnectionWarning(Image image)
        {
            Animator animator = image.GetComponent<Animator>();
            UIAnimationPointerEffects effects = image.GetComponent<UIAnimationPointerEffects>();
            bool connected = animator != null
                && animator.runtimeAnimatorController == UIAnimationGenerator.ControllerOf(_preset)
                && (effects != null ? _preset.HasPointerEffects || effects.HasTriggers : !_preset.HasPointerEffects);
            if (!connected)
            {
                Warn("이 Image의 컴포넌트가 설정과 다릅니다.", "다시 연결", () => UIAnimationGenerator.Connect(image, _preset));
            }
        }

        private void DrawPointerWarnings(Image image)
        {
            if (!image.raycastTarget)
            {
                Warn("Raycast Target이 꺼져 있어 마우스 이벤트가 오지 않습니다.", "켜기", () =>
                {
                    Undo.RecordObject(image, "Raycast Target 켜기");
                    image.raycastTarget = true;
                });
            }
            Canvas canvas = image.canvas;
            if (canvas != null && canvas.rootCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                Warn("Canvas에 Graphic Raycaster가 없어 마우스 이벤트가 오지 않습니다.", "추가",
                    () => Undo.AddComponent<GraphicRaycaster>(canvas.rootCanvas.gameObject));
            }
            if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                Warn("씬에 EventSystem이 없습니다. GameObject > UI (Canvas) > Event System으로 추가하세요.", null, null);
            }
            Selectable selectable = image.GetComponent<Selectable>();
            if (selectable == null || selectable.targetGraphic != image)
            {
                return;
            }
            bool colorOverlaps = selectable.transition == Selectable.Transition.ColorTint;
            bool spriteOverlaps = selectable.transition == Selectable.Transition.SpriteSwap;
            if (colorOverlaps || spriteOverlaps)
            {
                string target = colorOverlaps ? "Color Tint가 같은 Image 색을" : "Sprite Swap이 같은 Image 그림을";
                Warn($"Button의 {target} 바꿔 입력 반응과 겹칩니다.", "Transition 끄기", () =>
                {
                    Undo.RecordObject(selectable, "Transition 끄기");
                    selectable.transition = Selectable.Transition.None;
                });
            }
        }

        private static void Warn(string message, string button, Action fix)
        {
            EditorGUILayout.HelpBox(message, MessageType.Warning);
            if (button != null && GUILayout.Button(button))
            {
                fix();
            }
        }

        private void DrawSceneList(Image current)
        {
            UIAnimationPlayer[] players = FindObjectsByType<UIAnimationPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (players.Length == 0)
            {
                return;
            }

            EditorGUILayout.Space(6f);
            _showSceneList = EditorGUILayout.Foldout(_showSceneList, $"다른 Image 편집 (이 씬의 {players.Length}개)", true);
            if (!_showSceneList)
            {
                return;
            }

            foreach (UIAnimationPlayer player in players)
            {
                Image image = player.GetComponent<Image>();
                using (new EditorGUI.DisabledScope(image == null || image == current))
                {
                    string controller = player.Preset != null ? player.Preset.name : "연결 안 됨";
                    string mouse = player.GetComponent<UIAnimationPointerEffects>() != null ? "  ·  입력 반응" : string.Empty;
                    if (GUILayout.Button($"{player.name}  ·  {controller}{mouse}", EditorStyles.miniButton))
                    {
                        _image.objectReferenceValue = image;
                        OnImageChanged(image);
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }
        }

        // 값은 자동으로 파일에 저장되지만 버튼 연결 같은 씬 값은 씬을 저장해야 남는다. 이 버튼 하나로 둘 다 저장한다.
        private void DrawSaveBar()
        {
            Scene scene = _player.gameObject.scene;
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("저장", GUILayout.Height(SaveButtonHeight)))
                {
                    GUI.FocusControl(null);
                    _previewer.Stop();
                    _saveAt = double.MaxValue;
                    UIAnimationGenerator.SaveAll(_preset, scene);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.LabelField(SaveStatus(scene), EditorStyles.miniLabel);
        }

        private string SaveStatus(Scene scene)
        {
            if (Application.isPlaying)
            {
                return "플레이를 멈춘 뒤 저장하세요.";
            }
            return _saveAt != double.MaxValue || scene.isDirty ? "저장하지 않은 변경이 있습니다." : "저장됨";
        }

        private void ApplyChanges()
        {
            UIAnimationGenerator.RecordUndo(_preset);
            UIAnimationGenerator.Rebuild(_preset);
            UIAnimationGenerator.SyncComponents(_preset);
            _saveAt = EditorApplication.timeSinceStartup + SaveDelaySeconds;
            if (_previewer.IsPlaying && _previewStates != null)
            {
                RestartPreview();
            }
        }

        // 미리보기는 시작할 때 구운 임시 클립을 쓰므로, 값이 바뀌면 같은 자리에서 다시 구워 바로 보여 준다.
        private void RestartPreview()
        {
            double position = _previewer.Position;
            bool paused = _previewer.Paused;
            Preview(_previewStates);
            _previewer.Seek(position);
            if (!paused)
            {
                _previewer.Resume();
            }
        }

        // 클립도 같이 Undo되므로 여기서 다시 구우면 Redo 기록이 지워진다.
        private void OnUndoRedo()
        {
            if (_preset == null)
            {
                return;
            }
            _presetObject.Update();
            _saveAt = EditorApplication.timeSinceStartup + SaveDelaySeconds;
            Repaint();
        }

        // 저장하면 AnimationMode가 끝나므로 미리보기가 도는 동안에는 미룬다.
        private void SaveWhenDue()
        {
            if (EditorApplication.timeSinceStartup >= _saveAt && !_previewer.IsPlaying)
            {
                SavePending();
                Repaint();
            }
        }

        private void SavePending()
        {
            if (_saveAt != double.MaxValue && _preset != null)
            {
                UIAnimationGenerator.Save(_preset);
            }
            _saveAt = double.MaxValue;
        }

        private static string StateField(UIAnimationState state)
        {
            switch (state)
            {
                case UIAnimationState.Open: return "_open";
                case UIAnimationState.Stay: return "_stay";
                default: return "_close";
            }
        }

        private static string EffectField(UIPointerEffect effect)
        {
            switch (effect)
            {
                case UIPointerEffect.Hover: return "_hover";
                case UIPointerEffect.Exit: return "_exit";
                default: return "_pressed";
            }
        }

        private static string TriggerField(UIPointerEffect effect)
        {
            switch (effect)
            {
                case UIPointerEffect.Hover: return "_onHover";
                case UIPointerEffect.Exit: return "_onExit";
                default: return "_onPressed";
            }
        }

        [MenuItem("GameObject/UI (Canvas)/UI 애니메이션 에디터", false, 2200)]
        private static void CreateEditorObject(MenuCommand command)
        {
            var editor = new GameObject("UI Animation Editor", typeof(UIAnimationEditor)) { tag = "EditorOnly" };
            GameObjectUtility.SetParentAndAlign(editor, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(editor, "UI 애니메이션 에디터");
            Selection.activeGameObject = editor;
        }
    }
}
