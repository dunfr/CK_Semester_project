using System.Collections.Generic;
using CK.SemesterProject.SkillSequence;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.SkillSequence.EditorTools
{
    /// <summary>
    /// 스킬 연출 스튜디오(런타임 전투 연출 전용). 애니메이션·이동·타격·카메라 흔들림·이펙트를 한 타임라인에서 편집한다.
    /// 별도 무대를 만들지 않고, 지금 열린 씬의 플레이어·몬스터(SkillStudioStageAnchor에 지정)와 메인 카메라를 그대로 쓴다.
    /// 그래서 씬 뷰·게임 뷰에 보이는 모습이 곧 미리보기다.
    /// - 아군 스킬: 플레이어가 시전, 선택한 슬롯의 몬스터가 대상
    /// - 적 스킬: 선택한 슬롯의 몬스터가 시전, 플레이어가 대상
    /// 미리보기 중 바뀐 플레이어·몬스터 포즈는 창을 닫거나 씬을 저장할 때 원래대로 되돌린다.
    /// 이펙트 미리보기 오브젝트만 저장되지 않는 임시 오브젝트(HideFlags.DontSave)로 만든다.
    /// 카메라 무빙이 필요한 컷신은 여기서 만들지 않고 Unity Timeline(SkillCutsceneAsset)으로 연결만 한다.
    /// </summary>
    public sealed class SkillSequenceStudioWindow : EditorWindow
    {
        private const string StageName = "__SkillSequencePreview";
        private const string LegacyStageName = "__SkillSequenceStudio";
        private const string DefaultSequenceFolder = "Assets/CK_Semester_Project/Data/SkillSequences";
        private const float LabelWidth = 120f;
        private const float RulerHeight = 20f;
        private const float RowHeight = 22f;
        private const float MarkerWidth = 9f;
        private const float EdgeGrabWidth = 6f;
        private const int FixedRowCount = 4;
        private const int ShakeRow = 3;

        private enum DragTarget
        {
            None,
            Playhead,
            AnimationMove,
            VfxEventMove,
            VfxEventStart,
            VfxEventEnd,
            Hit,
            ApproachStart,
            ApproachEnd,
            HoldMove,
            ShakeMove,
            ShakeStart,
            ShakeEnd,
            ReturnStart,
            ReturnEnd
        }

        private struct TransformState
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
        }

        private sealed class PreviewInstance
        {
            public int EventIndex;
            public int TargetIndex;
            public GameObject Prefab;
            public GameObject Instance;
            public ParticleSystem[] Particles;
            public UnityEngine.Playables.PlayableDirector[] Directors;
            public Animator[] Animators;
            public bool IsActive;
            public float LocalTime;
            public Vector3 Position;
            public Quaternion Rotation;
            public Transform Parent;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 AppliedPosition;
            public Quaternion AppliedRotation;
            public Vector3 AppliedScale;
        }

        [SerializeField] private SkillSequenceAsset _sequence;
        [SerializeField] private int _targetSlot = 1;
        [SerializeField] private bool _snapToFrame = true;
        [SerializeField] private bool _loop = true;
        [SerializeField] private float _playSpeed = 1f;
        [SerializeField] private bool _showStageSettings = true;
        [SerializeField] private bool _showSequenceSettings = true;

        private readonly List<Transform> _contextTargets = new List<Transform>();
        private readonly List<PreviewInstance> _previews = new List<PreviewInstance>();
        private readonly List<TransformState> _savedPoses = new List<TransformState>();
        private float _time;
        private bool _isPlaying;
        private double _lastUpdateTime;
        private int _selectedEvent = -1;
        private int _selectedHit = -1;
        private int _selectedShake = -1;
        private DragTarget _drag;
        private int _dragIndex;
        private float _dragGrabOffset;
        private float _dragEventEnd;
        private float _dragViewDuration;
        private bool _needsRefresh;
        private Vector2 _scroll;
        private Rect _trackRect;
        private GameObject _stageRoot;
        private int _contextSlot = -1;
        private bool _contextForEnemySkill;
        private string _contextSignature;
        private SkillSequenceContext _context;
        private SkillSequenceSampler _sampler;
        private AnimationClip _samplerClip;
        private SerializedObject _serialized;
        private SkillStudioStageAnchor _anchor;

        private int SlotCount => GetSlots().Count;
        /// <summary>미리보기 슬롯. 아군 스킬이면 맞는 몬스터, 적 스킬이면 시전하는 몬스터.</summary>
        private int ActiveSlot => Mathf.Clamp(_targetSlot, 0, Mathf.Max(0, SlotCount - 1));
        private bool IsEnemySkill => _sequence != null && _sequence.IsEnemySkill;

        [MenuItem("CK/스킬 연출 스튜디오")]
        private static void Open()
        {
            GetWindow<SkillSequenceStudioWindow>("스킬 연출 스튜디오").Show();
        }

        #region Unity 이벤트

        private void OnEnable()
        {
            titleContent = new GUIContent("스킬 연출 스튜디오");
            SceneView.duringSceneGui += OnSceneGUI;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnSceneSaved;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            Selection.selectionChanged += OnSelectionChanged;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    DestroyStaleStages();
                    RefreshPreview();
                }
            };
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnSceneSaving;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= OnSceneSaved;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Selection.selectionChanged -= OnSelectionChanged;
            DestroyStage();
        }

        private void OnFocus()
        {
            _needsRefresh = true;
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("플레이 모드에서는 미리보기를 사용할 수 없습니다.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawToolbar();
            DrawStageSettings();
            if (_sequence == null)
            {
                EditorGUILayout.HelpBox("시퀀스 에셋을 선택하거나 '새 시퀀스'로 만드세요.\n(Project 창 우클릭 > Create > CK > Skill Sequence 로도 만들 수 있습니다.)", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            SerializedObject serialized = GetSerialized();
            HandleShortcuts(serialized);
            DrawSequenceSettings(serialized);
            DrawWarnings();
            DrawTransport(serialized);
            DrawSlotBar();
            DrawTimeline(serialized);
            DrawSelection(serialized);
            if (serialized.ApplyModifiedProperties())
            {
                _needsRefresh = true;
            }
            EditorGUILayout.EndScrollView();

            if (_needsRefresh)
            {
                RefreshPreview();
                SceneView.RepaintAll();
            }
        }

        #endregion

        #region 상단 UI

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _sequence = (SkillSequenceAsset)EditorGUILayout.ObjectField("시퀀스", _sequence, typeof(SkillSequenceAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                OnSequenceChanged();
            }
            if (GUILayout.Button("새 시퀀스", GUILayout.Width(80f)))
            {
                CreateSequence();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStageSettings()
        {
            _showStageSettings = EditorGUILayout.Foldout(_showStageSettings, "씬 무대", true);
            if (!_showStageSettings)
            {
                return;
            }
            EditorGUI.indentLevel++;
            if (_anchor == null)
            {
                _anchor = FindStageAnchor();
            }
            if (_anchor == null)
            {
                EditorGUILayout.HelpBox("지금 씬에 무대 설정(SkillStudioStageAnchor)이 없습니다.\n씬에 있는 플레이어·몬스터를 그대로 쓰므로, 무대 설정을 만들고 플레이어와 몬스터 슬롯을 지정하세요.", MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                if (GUILayout.Button("무대 설정 만들기"))
                {
                    CreateStageAnchor();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("플레이어", _anchor.Player != null ? _anchor.Player.name : "(비어 있음 — 무대 설정에서 지정)");
                EditorGUILayout.LabelField("몬스터 슬롯", SlotCount + "개");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                if (GUILayout.Button("무대 설정 선택"))
                {
                    Selection.activeObject = _anchor.gameObject;
                }
                if (GUILayout.Button("씬 뷰로 보기"))
                {
                    FrameStage();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(" ", "게임 뷰 = 씬 메인 카메라 그대로(카메라 흔들림 포함). 플레이어·몬스터를 선택하면 원래 자리로 돌아가 옮길 수 있습니다.", EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUI.indentLevel--;
        }

        private void CreateStageAnchor()
        {
            GameObject anchorObject = new GameObject("SkillStudioStage");
            Undo.RegisterCreatedObjectUndo(anchorObject, "무대 설정 만들기");
            _anchor = anchorObject.AddComponent<SkillStudioStageAnchor>();
            Selection.activeObject = anchorObject;
            _needsRefresh = true;
        }

        private void DrawSequenceSettings(SerializedObject serialized)
        {
            _showSequenceSettings = EditorGUILayout.Foldout(_showSequenceSettings, "시퀀스 설정", true);
            if (!_showSequenceSettings)
            {
                return;
            }
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serialized.FindProperty("_casterClip"), new GUIContent("애니메이션"));
            EditorGUILayout.PropertyField(serialized.FindProperty("_clipStart"), new GUIContent("애니메이션 시작(초)"));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serialized.FindProperty("_duration"), new GUIContent("전체 길이(초)"));
            using (new EditorGUI.DisabledScope(_sequence.CasterClip == null))
            {
                if (GUILayout.Button("클립 길이에 맞추기", GUILayout.Width(120f)))
                {
                    serialized.FindProperty("_duration").floatValue = Mathf.Max(0.1f, _sequence.ClipStart + _sequence.CasterClip.length);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(serialized.FindProperty("_frameRate"), new GUIContent("프레임(fps)"));
            EditorGUILayout.PropertyField(serialized.FindProperty("_isEnemySkill"), new GUIContent("적 스킬", "켜면 선택한 슬롯의 몬스터가 시전하고 플레이어가 대상이 됩니다. 슬롯을 바꿔 가며 모든 위치에서 어색하지 않은지 확인하세요."));
            SerializedProperty cutscene = serialized.FindProperty("_cutscene");
            EditorGUILayout.PropertyField(cutscene, new GUIContent("컷신 Timeline(선택)", "카메라 무빙이 필요한 연출. 스튜디오에서는 미리보지 않고 게임에서 그대로 재생합니다. Create > CK > Skill Cutscene"));
            if (cutscene.objectReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serialized.FindProperty("_cutsceneStart"), new GUIContent("컷신 시작(초)"));
                var cutsceneAsset = (SkillCutsceneAsset)cutscene.objectReferenceValue;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                using (new EditorGUI.DisabledScope(cutsceneAsset.Prefab == null))
                {
                    if (GUILayout.Button("컷신 프리팹 열기 (Timeline 편집)"))
                    {
                        AssetDatabase.OpenAsset(cutsceneAsset.Prefab);
                        EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(" ", cutsceneAsset.Prefab == null ? "컷신 프리팹이 비어 있습니다." : $"컷신 길이 {cutsceneAsset.Duration:0.00}s (스튜디오 미리보기 없음)", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            SerializedProperty useMovement = serialized.FindProperty("_useMovement");
            EditorGUILayout.PropertyField(useMovement, new GUIContent("대상에게 다가가기", "켜면 접근→공격→복귀 이동을 합니다(기본 공격용). 특수 스킬처럼 제자리에서 쓰면 끄세요."));
            if (useMovement.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serialized.FindProperty("_attackDistance"), new GUIContent("공격 거리(m)"));
                DrawRange(serialized, "접근(초)", "_approachStart", "_approachEnd");
                DrawRange(serialized, "복귀(초)", "_returnStart", "_returnEnd");
                EditorGUILayout.PropertyField(serialized.FindProperty("_moveCurve"), new GUIContent("이동 곡선"));
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawRange(SerializedObject serialized, string label, string startName, string endName)
        {
            SerializedProperty start = serialized.FindProperty(startName);
            SerializedProperty end = serialized.FindProperty(endName);
            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(label));
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float half = (rect.width - 4f) * 0.5f;
            start.floatValue = Mathf.Max(0f, EditorGUI.FloatField(new Rect(rect.x, rect.y, half, rect.height), start.floatValue));
            end.floatValue = Mathf.Max(start.floatValue, EditorGUI.FloatField(new Rect(rect.x + half + 4f, rect.y, half, rect.height), end.floatValue));
            EditorGUI.indentLevel = indent;
        }

        private void DrawTransport(SerializedObject serialized)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(_isPlaying ? "❚❚ 정지" : "▶ 재생", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                _isPlaying = !_isPlaying;
                if (_isPlaying && _time >= _sequence.Duration)
                {
                    _time = 0f;
                }
                _lastUpdateTime = EditorApplication.timeSinceStartup;
            }
            if (GUILayout.Button("■ 처음", EditorStyles.toolbarButton, GUILayout.Width(55f)))
            {
                _isPlaying = false;
                SetTime(0f);
            }
            _loop = GUILayout.Toggle(_loop, "반복", EditorStyles.toolbarButton, GUILayout.Width(40f));
            _snapToFrame = GUILayout.Toggle(_snapToFrame, "프레임 스냅", EditorStyles.toolbarButton, GUILayout.Width(75f));
            GUILayout.Label("속도", GUILayout.Width(28f));
            _playSpeed = EditorGUILayout.Slider(_playSpeed, 0.1f, 2f, GUILayout.Width(140f));
            GUILayout.FlexibleSpace();
            int frame = Mathf.RoundToInt(_time * _sequence.FrameRate);
            int lastFrame = Mathf.RoundToInt(_sequence.Duration * _sequence.FrameRate);
            GUILayout.Label($"{_time:0.000}s  |  {frame} / {lastFrame}f", GUILayout.Width(150f));
            if (GUILayout.Button("+ 이펙트", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                AddVfxEvent(serialized);
            }
            if (GUILayout.Button("+ 타격", EditorStyles.toolbarButton, GUILayout.Width(50f)))
            {
                AddHit(serialized);
            }
            if (GUILayout.Button("+ 흔들림", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                AddShake(serialized);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>미리보기 슬롯 선택. 같은 스킬이 어느 슬롯에서든 어색하지 않은지 바꿔 가며 확인한다.</summary>
        private void DrawSlotBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(IsEnemySkill ? "시전 몬스터" : "대상 몬스터", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
            int slotCount = SlotCount;
            if (slotCount == 0)
            {
                GUILayout.Label("몬스터 슬롯이 없습니다(씬 무대 설정에서 지정).", EditorStyles.miniLabel);
            }
            else
            {
                string[] labels = new string[slotCount];
                for (int i = 0; i < slotCount; i++)
                {
                    labels[i] = "슬롯 " + (i + 1);
                }
                EditorGUI.BeginChangeCheck();
                int slot = GUILayout.Toolbar(ActiveSlot, labels, EditorStyles.toolbarButton, GUILayout.Width(Mathf.Min(70f * slotCount, 350f)));
                if (EditorGUI.EndChangeCheck())
                {
                    _targetSlot = slot;
                    _needsRefresh = true;
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(IsEnemySkill ? "적 스킬: 몬스터 → 플레이어" : "아군 스킬: 플레이어 → 몬스터", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>실제 전투에서 문제가 될 만한 설정을 알려 준다.</summary>
        private void DrawWarnings()
        {
            List<string> warnings = new List<string>();
            float duration = _sequence.Duration;
            if (_sequence.HitTimes.Count == 0)
            {
                warnings.Add("타격 시점이 없습니다. 실제 전투에서 피해·피격 반응이 들어가지 않습니다.");
            }
            int missingPrefab = 0;
            int lateEvents = 0;
            foreach (SkillVfxEvent vfxEvent in _sequence.VfxEvents)
            {
                if (vfxEvent.Prefab == null)
                {
                    missingPrefab++;
                }
                if (vfxEvent.Time > duration)
                {
                    lateEvents++;
                }
            }
            foreach (float hit in _sequence.HitTimes)
            {
                if (hit > duration)
                {
                    lateEvents++;
                }
            }
            foreach (SkillCameraShakeEvent shake in _sequence.ShakeEvents)
            {
                if (shake.Time > duration)
                {
                    lateEvents++;
                }
            }
            if (missingPrefab > 0)
            {
                warnings.Add($"프리팹이 비어 있는 이펙트 {missingPrefab}개");
            }
            if (lateEvents > 0)
            {
                warnings.Add($"전체 길이({duration:0.00}s)보다 뒤에 있는 항목 {lateEvents}개 — 게임에서는 실행되지 않습니다.");
            }
            if (_sequence.Cutscene != null && _sequence.CutsceneStart > duration)
            {
                warnings.Add("컷신 시작이 전체 길이보다 뒤에 있어 재생되지 않습니다.");
            }
            if (_sequence.UseMovement && _sequence.ReturnEnd > duration)
            {
                warnings.Add("복귀가 끝나기 전에 연출이 끝납니다. 전체 길이를 늘리거나 복귀를 당기세요.");
            }
            if (warnings.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", warnings), MessageType.Warning);
            }
        }

        /// <summary>Space 재생/정지, ←/→ 한 프레임, Delete 선택 항목 삭제. 입력칸에 글자를 쓰는 중에는 무시한다.</summary>
        private void HandleShortcuts(SerializedObject serialized)
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || EditorGUIUtility.editingTextField || GUIUtility.keyboardControl != 0)
            {
                return;
            }
            float frame = 1f / Mathf.Max(1f, _sequence.FrameRate);
            switch (current.keyCode)
            {
                case KeyCode.Space:
                    _isPlaying = !_isPlaying;
                    if (_isPlaying && _time >= _sequence.Duration)
                    {
                        _time = 0f;
                    }
                    _lastUpdateTime = EditorApplication.timeSinceStartup;
                    break;
                case KeyCode.LeftArrow:
                    _isPlaying = false;
                    SetTime(SkillSequenceUtility.SnapToFrame(_time - frame, _sequence.FrameRate));
                    break;
                case KeyCode.RightArrow:
                    _isPlaying = false;
                    SetTime(SkillSequenceUtility.SnapToFrame(_time + frame, _sequence.FrameRate));
                    break;
                case KeyCode.Delete:
                    if (!DeleteSelection(serialized))
                    {
                        return;
                    }
                    break;
                default:
                    return;
            }
            current.Use();
            Repaint();
        }

        private bool DeleteSelection(SerializedObject serialized)
        {
            SerializedProperty events = serialized.FindProperty("_vfxEvents");
            SerializedProperty hits = serialized.FindProperty("_hitTimes");
            SerializedProperty shakes = serialized.FindProperty("_shakeEvents");
            if (_selectedEvent >= 0 && _selectedEvent < events.arraySize)
            {
                DeleteVfxEvent(serialized, _selectedEvent);
                return true;
            }
            if (_selectedShake >= 0 && _selectedShake < shakes.arraySize)
            {
                shakes.DeleteArrayElementAtIndex(_selectedShake);
                _selectedShake = -1;
                return true;
            }
            if (_selectedHit >= 0 && _selectedHit < hits.arraySize)
            {
                hits.DeleteArrayElementAtIndex(_selectedHit);
                _selectedHit = -1;
                return true;
            }
            return false;
        }

        #endregion

        #region 타임라인

        private void DrawTimeline(SerializedObject serialized)
        {
            SerializedProperty events = serialized.FindProperty("_vfxEvents");
            SerializedProperty hits = serialized.FindProperty("_hitTimes");
            int rowCount = FixedRowCount + events.arraySize;
            Rect area = GUILayoutUtility.GetRect(10f, RulerHeight + rowCount * RowHeight + 2f, GUILayout.ExpandWidth(true));
            _trackRect = new Rect(area.x + LabelWidth, area.y, Mathf.Max(10f, area.width - LabelWidth - 10f), area.height);
            // 드래그 중에는 눈금 비율을 고정해 블럭 길이가 바뀌어도 화면이 흔들리지 않게 한다.
            float viewDuration = _drag != DragTarget.None && _dragViewDuration > 0f ? _dragViewDuration : GetViewDuration();

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(area, new Color(0.16f, 0.16f, 0.16f));
                DrawRuler(viewDuration);
                for (int row = 0; row < rowCount; row++)
                {
                    Rect rowRect = GetRowRect(area, row);
                    EditorGUI.DrawRect(rowRect, row % 2 == 0 ? new Color(0.21f, 0.21f, 0.21f) : new Color(0.19f, 0.19f, 0.19f));
                }
                DrawAnimationRow(area, viewDuration);
                DrawMovementRow(area, viewDuration);
                DrawHitRow(area, hits, viewDuration);
                DrawShakeRow(area, serialized.FindProperty("_shakeEvents"), viewDuration);
                for (int i = 0; i < events.arraySize; i++)
                {
                    DrawEventRow(area, events.GetArrayElementAtIndex(i), i, viewDuration);
                }
                float endX = TimeToX(_sequence.Duration, viewDuration);
                EditorGUI.DrawRect(new Rect(endX, area.y, 1f, area.height), new Color(1f, 0.4f, 0.3f, 0.8f));
                if (_sequence.Cutscene != null)
                {
                    // 컷신은 미리보지 않고 시작 시점만 표시한다.
                    float cutsceneX = TimeToX(_sequence.CutsceneStart, viewDuration);
                    EditorGUI.DrawRect(new Rect(cutsceneX - 1f, area.y, 2f, area.height), new Color(0.65f, 0.45f, 1f));
                    GUI.Label(new Rect(cutsceneX + 3f, area.y + 2f, 60f, RulerHeight), "컷신", EditorStyles.miniBoldLabel);
                }
                float playheadX = TimeToX(_time, viewDuration);
                EditorGUI.DrawRect(new Rect(playheadX - 1f, area.y, 2f, area.height), new Color(1f, 0.85f, 0.2f));
            }
            DrawRowLabels(area, events);
            for (int i = 0; i < events.arraySize; i++)
            {
                AddBlockCursors(GetEventBlockRect(area, i, events.GetArrayElementAtIndex(i).FindPropertyRelative("_time").floatValue, viewDuration));
            }
            SerializedProperty shakeList = serialized.FindProperty("_shakeEvents");
            for (int i = 0; i < shakeList.arraySize; i++)
            {
                AddBlockCursors(GetShakeBlockRect(GetTrackRow(area, ShakeRow), shakeList.GetArrayElementAtIndex(i), viewDuration));
            }
            if (_sequence.CasterClip != null)
            {
                EditorGUIUtility.AddCursorRect(GetAnimationRect(area, viewDuration), MouseCursor.Pan);
            }
            if (_sequence.UseMovement)
            {
                Rect moveRow = GetTrackRow(area, 1);
                Rect block = GetSpanRect(moveRow, _sequence.ApproachStart, _sequence.ReturnEnd, viewDuration);
                AddBlockCursors(block);
                foreach (float divider in new[] { _sequence.ApproachEnd, _sequence.ReturnStart })
                {
                    float x = TimeToX(divider, viewDuration);
                    EditorGUIUtility.AddCursorRect(new Rect(x - 3f, block.y, 6f, block.height), MouseCursor.ResizeHorizontal);
                }
            }
            HandleTimelineInput(area, serialized, events, hits, viewDuration);
        }

        private static void AddBlockCursors(Rect block)
        {
            EditorGUIUtility.AddCursorRect(new Rect(block.x - 2f, block.y, EdgeGrabWidth, block.height), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(block.xMax - EdgeGrabWidth + 2f, block.y, EdgeGrabWidth, block.height), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(block.x + EdgeGrabWidth, block.y, Mathf.Max(0f, block.width - EdgeGrabWidth * 2f), block.height), MouseCursor.Pan);
        }

        private void DrawRuler(float viewDuration)
        {
            Rect ruler = new Rect(_trackRect.x, _trackRect.y, _trackRect.width, RulerHeight);
            EditorGUI.DrawRect(ruler, new Color(0.25f, 0.25f, 0.25f));
            float[] steps = { 0.01f, 0.02f, 0.05f, 0.1f, 0.2f, 0.25f, 0.5f, 1f, 2f, 5f };
            float step = steps[steps.Length - 1];
            foreach (float candidate in steps)
            {
                if (candidate / viewDuration * _trackRect.width >= 55f)
                {
                    step = candidate;
                    break;
                }
            }
            GUIStyle style = EditorStyles.miniLabel;
            for (float t = 0f; t <= viewDuration + 0.0001f; t += step)
            {
                float x = TimeToX(t, viewDuration);
                EditorGUI.DrawRect(new Rect(x, ruler.y + RulerHeight - 7f, 1f, 7f), Color.gray);
                int frame = Mathf.RoundToInt(t * _sequence.FrameRate);
                GUI.Label(new Rect(x + 2f, ruler.y, 70f, RulerHeight), $"{t:0.##}s ({frame})", style);
            }
        }

        private void DrawRowLabels(Rect area, SerializedProperty events)
        {
            GUI.Label(new Rect(area.x + 4f, area.y + 2f, LabelWidth - 6f, RulerHeight), "시간(초 / 프레임)", EditorStyles.miniLabel);
            GUI.Label(Inset(GetRowRect(area, 0)), "애니메이션", EditorStyles.miniBoldLabel);
            GUI.Label(Inset(GetRowRect(area, 1)), _sequence.UseMovement ? "이동" : "이동(사용 안 함)", EditorStyles.miniBoldLabel);
            GUI.Label(Inset(GetRowRect(area, 2)), "타격", EditorStyles.miniBoldLabel);
            GUI.Label(Inset(GetRowRect(area, ShakeRow)), "카메라 흔들림", EditorStyles.miniBoldLabel);
            for (int i = 0; i < events.arraySize; i++)
            {
                Rect labelRect = Inset(GetRowRect(area, FixedRowCount + i));
                string name = events.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue;
                GUIStyle style = i == _selectedEvent ? EditorStyles.boldLabel : EditorStyles.label;
                if (GUI.Button(labelRect, new GUIContent("● " + name), style))
                {
                    SelectEvent(i);
                }
            }
        }

        private void DrawAnimationRow(Rect area, float viewDuration)
        {
            Rect row = GetTrackRow(area, 0);
            AnimationClip clip = _sequence.CasterClip;
            if (clip == null)
            {
                GUI.Label(row, "  (애니메이션 없음 — 클립 없이도 이펙트 위치 작업은 가능합니다)", EditorStyles.miniLabel);
                return;
            }
            Rect bar = GetAnimationRect(area, viewDuration);
            if (_drag == DragTarget.AnimationMove)
            {
                EditorGUI.DrawRect(new Rect(bar.x - 1f, bar.y - 1f, bar.width + 2f, bar.height + 2f), Color.white);
            }
            EditorGUI.DrawRect(bar, new Color(0.25f, 0.45f, 0.75f));
            GUI.Label(bar, "  " + clip.name + $"  ({_sequence.ClipStart:0.00}s ~ {_sequence.ClipStart + clip.length:0.00}s)", EditorStyles.whiteMiniLabel);
        }

        private Rect GetAnimationRect(Rect area, float viewDuration)
        {
            Rect row = GetTrackRow(area, 0);
            float length = _sequence.CasterClip != null ? _sequence.CasterClip.length : 0f;
            return GetSpanRect(row, _sequence.ClipStart, _sequence.ClipStart + length, viewDuration);
        }

        private void DrawMovementRow(Rect area, float viewDuration)
        {
            if (!_sequence.UseMovement)
            {
                return;
            }
            Rect row = GetTrackRow(area, 1);
            Rect block = GetSpanRect(row, _sequence.ApproachStart, _sequence.ReturnEnd, viewDuration);
            if (_drag == DragTarget.HoldMove)
            {
                EditorGUI.DrawRect(new Rect(block.x - 1f, block.y - 1f, block.width + 2f, block.height + 2f), Color.white);
            }
            // 하나의 이동 블럭 안에 접근(초록)·공격 위치(갈색)·복귀(파랑)를 나눠 그린다.
            float approachEndX = TimeToX(_sequence.ApproachEnd, viewDuration);
            float returnStartX = TimeToX(_sequence.ReturnStart, viewDuration);
            EditorGUI.DrawRect(new Rect(block.x, block.y, Mathf.Max(0f, approachEndX - block.x), block.height), new Color(0.3f, 0.65f, 0.4f));
            EditorGUI.DrawRect(new Rect(approachEndX, block.y, Mathf.Max(0f, returnStartX - approachEndX), block.height), new Color(0.55f, 0.42f, 0.25f));
            EditorGUI.DrawRect(new Rect(returnStartX, block.y, Mathf.Max(0f, block.xMax - returnStartX), block.height), new Color(0.3f, 0.5f, 0.7f));
            Color edge = new Color(1f, 1f, 1f, 0.8f);
            EditorGUI.DrawRect(new Rect(block.x, block.y, 3f, block.height), edge);
            EditorGUI.DrawRect(new Rect(block.xMax - 3f, block.y, 3f, block.height), edge);
            EditorGUI.DrawRect(new Rect(approachEndX - 1f, block.y, 2f, block.height), edge);
            EditorGUI.DrawRect(new Rect(returnStartX - 1f, block.y, 2f, block.height), edge);
            DrawSegmentLabel(block.x, approachEndX, block, "접근");
            DrawSegmentLabel(approachEndX, returnStartX, block, "공격 위치");
            DrawSegmentLabel(returnStartX, block.xMax, block, "복귀");
        }

        private static void DrawSegmentLabel(float startX, float endX, Rect block, string label)
        {
            if (endX - startX > 40f)
            {
                GUI.Label(new Rect(startX + 5f, block.y, endX - startX - 8f, block.height), label, EditorStyles.whiteMiniLabel);
            }
        }

        private void DrawShakeRow(Rect area, SerializedProperty shakes, float viewDuration)
        {
            Rect row = GetTrackRow(area, ShakeRow);
            for (int i = 0; i < shakes.arraySize; i++)
            {
                Rect block = GetShakeBlockRect(row, shakes.GetArrayElementAtIndex(i), viewDuration);
                bool isSelected = i == _selectedShake;
                if (isSelected)
                {
                    EditorGUI.DrawRect(new Rect(block.x - 1f, block.y - 1f, block.width + 2f, block.height + 2f), Color.white);
                }
                EditorGUI.DrawRect(block, isSelected ? new Color(0.85f, 0.5f, 0.2f) : new Color(0.7f, 0.42f, 0.18f));
                Color edge = new Color(1f, 1f, 1f, isSelected ? 0.9f : 0.55f);
                EditorGUI.DrawRect(new Rect(block.x, block.y, 3f, block.height), edge);
                EditorGUI.DrawRect(new Rect(block.xMax - 3f, block.y, 3f, block.height), edge);
                if (block.width > 50f)
                {
                    GUI.Label(new Rect(block.x + 6f, block.y, block.width - 10f, block.height), "흔들림 " + (i + 1), EditorStyles.whiteMiniLabel);
                }
            }
        }

        private Rect GetShakeBlockRect(Rect row, SerializedProperty shake, float viewDuration)
        {
            float time = shake.FindPropertyRelative("_time").floatValue;
            float duration = shake.FindPropertyRelative("_duration").floatValue;
            return GetSpanRect(row, time, time + duration, viewDuration);
        }

        private void DrawHitRow(Rect area, SerializedProperty hits, float viewDuration)
        {
            Rect row = GetTrackRow(area, 2);
            for (int i = 0; i < hits.arraySize; i++)
            {
                Color color = i == _selectedHit ? Color.white : new Color(1f, 0.35f, 0.3f);
                float hitTime = hits.GetArrayElementAtIndex(i).floatValue;
                DrawHandle(row, hitTime, viewDuration, color);
                GUI.Label(new Rect(TimeToX(hitTime, viewDuration) + MarkerWidth * 0.5f + 2f, row.y, 50f, row.height), "타격 " + (i + 1), EditorStyles.miniLabel);
            }
        }

        private void DrawEventRow(Rect area, SerializedProperty vfxEvent, int index, float viewDuration)
        {
            Rect block = GetEventBlockRect(area, index, vfxEvent.FindPropertyRelative("_time").floatValue, viewDuration);
            bool isSelected = index == _selectedEvent;
            Color color = Color.HSVToRGB((index * 0.17f + 0.08f) % 1f, 0.5f, isSelected ? 0.85f : 0.65f);
            if (isSelected)
            {
                EditorGUI.DrawRect(new Rect(block.x - 1f, block.y - 1f, block.width + 2f, block.height + 2f), Color.white);
            }
            EditorGUI.DrawRect(block, color);
            // 양 끝 손잡이: 왼쪽 = 시작 시점, 오른쪽 = 길이
            Color edge = new Color(1f, 1f, 1f, isSelected ? 0.9f : 0.55f);
            EditorGUI.DrawRect(new Rect(block.x, block.y, 3f, block.height), edge);
            EditorGUI.DrawRect(new Rect(block.xMax - 3f, block.y, 3f, block.height), edge);
            bool hasPrefab = vfxEvent.FindPropertyRelative("_prefab").objectReferenceValue != null;
            string label = vfxEvent.FindPropertyRelative("_name").stringValue + (hasPrefab ? "" : "  (프리팹 없음)");
            GUI.Label(new Rect(block.x + 6f, block.y, Mathf.Max(0f, block.width - 10f), block.height), label, EditorStyles.whiteMiniLabel);
        }

        private Rect GetEventBlockRect(Rect area, int index, float time, float viewDuration)
        {
            Rect row = GetTrackRow(area, FixedRowCount + index);
            float lifetime = index < _sequence.VfxEvents.Count ? _sequence.VfxEvents[index].GetEffectiveLifetime() : 1f;
            return GetSpanRect(row, time, time + lifetime, viewDuration);
        }

        /// <summary>블럭 사각형. 너무 짧아도 양 끝 손잡이를 잡을 수 있게 최소 폭을 둔다.</summary>
        private Rect GetSpanRect(Rect row, float start, float end, float viewDuration)
        {
            float startX = TimeToX(start, viewDuration);
            float endX = TimeToX(end, viewDuration);
            return new Rect(startX, row.y + 3f, Mathf.Max(EdgeGrabWidth * 2f + 2f, endX - startX), row.height - 6f);
        }

        private void DrawHandle(Rect row, float time, float viewDuration, Color color)
        {
            Rect rect = GetHandleRect(row, time, viewDuration);
            EditorGUI.DrawRect(rect, Color.black);
            EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), color);
        }

        private void HandleTimelineInput(Rect area, SerializedObject serialized, SerializedProperty events, SerializedProperty hits, float viewDuration)
        {
            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            Rect inputRect = new Rect(_trackRect.x - MarkerWidth, area.y, _trackRect.width + MarkerWidth * 2f, area.height);
            switch (current.type)
            {
                case EventType.MouseDown:
                    if (current.button != 0 || !inputRect.Contains(current.mousePosition))
                    {
                        return;
                    }
                    PickDragTarget(area, current.mousePosition, events, hits, viewDuration);
                    _dragViewDuration = viewDuration;
                    GUIUtility.hotControl = controlId;
                    if (_drag != DragTarget.VfxEventStart && _drag != DragTarget.VfxEventEnd && _drag != DragTarget.ShakeStart && _drag != DragTarget.ShakeEnd)
                    {
                        ApplyDrag(serialized, events, hits, current.mousePosition.x, viewDuration);
                    }
                    current.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId)
                    {
                        return;
                    }
                    ApplyDrag(serialized, events, hits, current.mousePosition.x, viewDuration);
                    current.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId)
                    {
                        return;
                    }
                    GUIUtility.hotControl = 0;
                    _drag = DragTarget.None;
                    _dragViewDuration = 0f;
                    Repaint();
                    current.Use();
                    break;
            }
        }

        private void PickDragTarget(Rect area, Vector2 mouse, SerializedProperty events, SerializedProperty hits, float viewDuration)
        {
            _drag = DragTarget.Playhead;
            _dragIndex = -1;
            for (int i = 0; i < events.arraySize; i++)
            {
                float time = events.GetArrayElementAtIndex(i).FindPropertyRelative("_time").floatValue;
                Rect block = GetEventBlockRect(area, i, time, viewDuration);
                Rect grab = new Rect(block.x - 3f, block.y - 2f, block.width + 6f, block.height + 4f);
                if (!grab.Contains(mouse))
                {
                    continue;
                }
                _dragIndex = i;
                _dragEventEnd = time + _sequence.VfxEvents[i].GetEffectiveLifetime();
                if (mouse.x <= block.x + EdgeGrabWidth)
                {
                    _drag = DragTarget.VfxEventStart;
                }
                else if (mouse.x >= block.xMax - EdgeGrabWidth)
                {
                    _drag = DragTarget.VfxEventEnd;
                }
                else
                {
                    _drag = DragTarget.VfxEventMove;
                    _dragGrabOffset = XToTime(mouse.x, viewDuration) - time;
                }
                SelectEvent(i);
                return;
            }
            SerializedProperty shakes = _serialized.FindProperty("_shakeEvents");
            Rect shakeRow = GetTrackRow(area, ShakeRow);
            for (int i = 0; i < shakes.arraySize; i++)
            {
                SerializedProperty shake = shakes.GetArrayElementAtIndex(i);
                Rect shakeBlock = GetShakeBlockRect(shakeRow, shake, viewDuration);
                if (!new Rect(shakeBlock.x - 3f, shakeBlock.y - 2f, shakeBlock.width + 6f, shakeBlock.height + 4f).Contains(mouse))
                {
                    continue;
                }
                float shakeTime = shake.FindPropertyRelative("_time").floatValue;
                _dragIndex = i;
                _dragEventEnd = shakeTime + shake.FindPropertyRelative("_duration").floatValue;
                if (mouse.x <= shakeBlock.x + EdgeGrabWidth)
                {
                    _drag = DragTarget.ShakeStart;
                }
                else if (mouse.x >= shakeBlock.xMax - EdgeGrabWidth)
                {
                    _drag = DragTarget.ShakeEnd;
                }
                else
                {
                    _drag = DragTarget.ShakeMove;
                    _dragGrabOffset = XToTime(mouse.x, viewDuration) - shakeTime;
                }
                _selectedShake = i;
                _selectedEvent = -1;
                _selectedHit = -1;
                return;
            }
            Rect hitRow = GetTrackRow(area, 2);
            for (int i = 0; i < hits.arraySize; i++)
            {
                if (Expand(GetHandleRect(hitRow, hits.GetArrayElementAtIndex(i).floatValue, viewDuration)).Contains(mouse))
                {
                    _drag = DragTarget.Hit;
                    _dragIndex = i;
                    _selectedHit = i;
                    _selectedEvent = -1;
                    _selectedShake = -1;
                    return;
                }
            }
            if (_sequence.UseMovement)
            {
                Rect moveRow = GetTrackRow(area, 1);
                Rect block = GetSpanRect(moveRow, _sequence.ApproachStart, _sequence.ReturnEnd, viewDuration);
                Rect grab = new Rect(block.x - 3f, block.y - 2f, block.width + 6f, block.height + 4f);
                if (grab.Contains(mouse))
                {
                    // 경계 손잡이 우선: 출발 → 복귀 완료 → 도착 → 복귀 시작, 나머지는 전체 이동
                    float approachEndX = TimeToX(_sequence.ApproachEnd, viewDuration);
                    float returnStartX = TimeToX(_sequence.ReturnStart, viewDuration);
                    if (Mathf.Abs(mouse.x - block.x) <= EdgeGrabWidth)
                    {
                        _drag = DragTarget.ApproachStart;
                    }
                    else if (Mathf.Abs(mouse.x - block.xMax) <= EdgeGrabWidth)
                    {
                        _drag = DragTarget.ReturnEnd;
                    }
                    else if (Mathf.Abs(mouse.x - approachEndX) <= 4f)
                    {
                        _drag = DragTarget.ApproachEnd;
                    }
                    else if (Mathf.Abs(mouse.x - returnStartX) <= 4f)
                    {
                        _drag = DragTarget.ReturnStart;
                    }
                    else
                    {
                        _drag = DragTarget.HoldMove;
                        _dragGrabOffset = XToTime(mouse.x, viewDuration) - _sequence.ApproachStart;
                    }
                    return;
                }
            }
            if (_sequence.CasterClip != null)
            {
                Rect animation = GetAnimationRect(area, viewDuration);
                if (new Rect(animation.x - 3f, animation.y - 2f, animation.width + 6f, animation.height + 4f).Contains(mouse))
                {
                    _drag = DragTarget.AnimationMove;
                    _dragGrabOffset = XToTime(mouse.x, viewDuration) - _sequence.ClipStart;
                }
            }
        }

        private void ApplyDrag(SerializedObject serialized, SerializedProperty events, SerializedProperty hits, float mouseX, float viewDuration)
        {
            float rawTime = XToTime(mouseX, viewDuration);
            if (_drag == DragTarget.VfxEventMove || _drag == DragTarget.HoldMove || _drag == DragTarget.AnimationMove || _drag == DragTarget.ShakeMove)
            {
                rawTime -= _dragGrabOffset;
            }
            float time = Mathf.Clamp(rawTime, 0f, Mathf.Max(viewDuration, _sequence.Duration * 2f));
            if (_snapToFrame)
            {
                time = SkillSequenceUtility.SnapToFrame(time, _sequence.FrameRate);
            }
            float minimumLength = 1f / Mathf.Max(1f, _sequence.FrameRate);
            switch (_drag)
            {
                case DragTarget.VfxEventMove:
                    events.GetArrayElementAtIndex(_dragIndex).FindPropertyRelative("_time").floatValue = time;
                    break;
                case DragTarget.VfxEventStart:
                {
                    // 끝은 고정하고 시작만 당기거나 민다.
                    SerializedProperty vfxEvent = events.GetArrayElementAtIndex(_dragIndex);
                    time = Mathf.Min(time, _dragEventEnd - minimumLength);
                    vfxEvent.FindPropertyRelative("_time").floatValue = time;
                    vfxEvent.FindPropertyRelative("_lifetime").floatValue = _dragEventEnd - time;
                    break;
                }
                case DragTarget.VfxEventEnd:
                {
                    SerializedProperty vfxEvent = events.GetArrayElementAtIndex(_dragIndex);
                    float start = vfxEvent.FindPropertyRelative("_time").floatValue;
                    time = Mathf.Max(time, start + minimumLength);
                    vfxEvent.FindPropertyRelative("_lifetime").floatValue = time - start;
                    // 끝 부분 모습을 바로 확인하도록 재생 바를 끝 직전에 둔다.
                    SetTime(time - minimumLength * 0.5f);
                    Repaint();
                    return;
                }
                case DragTarget.ShakeMove:
                    serialized.FindProperty("_shakeEvents").GetArrayElementAtIndex(_dragIndex).FindPropertyRelative("_time").floatValue = time;
                    break;
                case DragTarget.ShakeStart:
                {
                    SerializedProperty shake = serialized.FindProperty("_shakeEvents").GetArrayElementAtIndex(_dragIndex);
                    time = Mathf.Min(time, _dragEventEnd - minimumLength);
                    shake.FindPropertyRelative("_time").floatValue = time;
                    shake.FindPropertyRelative("_duration").floatValue = _dragEventEnd - time;
                    break;
                }
                case DragTarget.ShakeEnd:
                {
                    SerializedProperty shake = serialized.FindProperty("_shakeEvents").GetArrayElementAtIndex(_dragIndex);
                    float start = shake.FindPropertyRelative("_time").floatValue;
                    time = Mathf.Max(time, start + minimumLength);
                    shake.FindPropertyRelative("_duration").floatValue = time - start;
                    break;
                }
                case DragTarget.Hit:
                    hits.GetArrayElementAtIndex(_dragIndex).floatValue = time;
                    break;
                case DragTarget.AnimationMove:
                    serialized.FindProperty("_clipStart").floatValue = time;
                    break;
                case DragTarget.HoldMove:
                {
                    // 접근·공격 위치·복귀를 간격 그대로 통째로 옮긴다.
                    float delta = Mathf.Max(time, 0f) - _sequence.ApproachStart;
                    serialized.FindProperty("_approachStart").floatValue = _sequence.ApproachStart + delta;
                    serialized.FindProperty("_approachEnd").floatValue = _sequence.ApproachEnd + delta;
                    serialized.FindProperty("_returnStart").floatValue = _sequence.ReturnStart + delta;
                    serialized.FindProperty("_returnEnd").floatValue = _sequence.ReturnEnd + delta;
                    time = _sequence.ApproachEnd + delta;
                    break;
                }
                case DragTarget.ApproachStart:
                    serialized.FindProperty("_approachStart").floatValue = Mathf.Min(time, _sequence.ApproachEnd);
                    break;
                case DragTarget.ApproachEnd:
                    serialized.FindProperty("_approachEnd").floatValue = Mathf.Clamp(time, _sequence.ApproachStart, _sequence.ReturnStart);
                    break;
                case DragTarget.ReturnStart:
                    serialized.FindProperty("_returnStart").floatValue = Mathf.Clamp(time, _sequence.ApproachEnd, _sequence.ReturnEnd);
                    break;
                case DragTarget.ReturnEnd:
                    serialized.FindProperty("_returnEnd").floatValue = Mathf.Max(time, _sequence.ReturnStart);
                    break;
                case DragTarget.Playhead:
                    _isPlaying = false;
                    break;
            }
            SetTime(time);
            Repaint();
        }

        private float GetViewDuration()
        {
            float duration = _sequence.Duration;
            if (_sequence.CasterClip != null)
            {
                duration = Mathf.Max(duration, _sequence.ClipStart + _sequence.CasterClip.length);
            }
            if (_sequence.UseMovement)
            {
                duration = Mathf.Max(duration, _sequence.ReturnEnd);
            }
            foreach (SkillVfxEvent vfxEvent in _sequence.VfxEvents)
            {
                duration = Mathf.Max(duration, vfxEvent.Time + vfxEvent.GetEffectiveLifetime());
            }
            foreach (SkillCameraShakeEvent shake in _sequence.ShakeEvents)
            {
                duration = Mathf.Max(duration, shake.Time + shake.Duration);
            }
            return Mathf.Max(0.1f, duration * 1.05f);
        }

        private float TimeToX(float time, float viewDuration)
        {
            return _trackRect.x + time / viewDuration * _trackRect.width;
        }

        private float XToTime(float x, float viewDuration)
        {
            return (x - _trackRect.x) / _trackRect.width * viewDuration;
        }

        private static Rect GetRowRect(Rect area, int row)
        {
            return new Rect(area.x, area.y + RulerHeight + row * RowHeight, area.width, RowHeight);
        }

        private Rect GetTrackRow(Rect area, int row)
        {
            Rect rowRect = GetRowRect(area, row);
            return new Rect(_trackRect.x, rowRect.y, _trackRect.width, rowRect.height);
        }

        private Rect GetHandleRect(Rect row, float time, float viewDuration)
        {
            return new Rect(TimeToX(time, viewDuration) - MarkerWidth * 0.5f, row.y + 3f, MarkerWidth, row.height - 6f);
        }

        private static Rect Expand(Rect rect)
        {
            return new Rect(rect.x - 3f, rect.y - 2f, rect.width + 6f, rect.height + 4f);
        }

        private static Rect Inset(Rect rect)
        {
            return new Rect(rect.x + 4f, rect.y + 2f, LabelWidth - 8f, rect.height - 4f);
        }

        #endregion

        #region 선택 항목 편집

        private void DrawSelection(SerializedObject serialized)
        {
            SerializedProperty events = serialized.FindProperty("_vfxEvents");
            SerializedProperty hits = serialized.FindProperty("_hitTimes");
            SerializedProperty shakes = serialized.FindProperty("_shakeEvents");
            EditorGUILayout.Space(6f);
            if (_selectedEvent >= 0 && _selectedEvent < events.arraySize)
            {
                DrawSelectedEvent(serialized, events, _selectedEvent);
            }
            else if (_selectedShake >= 0 && _selectedShake < shakes.arraySize)
            {
                SerializedProperty shake = shakes.GetArrayElementAtIndex(_selectedShake);
                EditorGUILayout.LabelField("카메라 흔들림 " + (_selectedShake + 1), EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(shake.FindPropertyRelative("_time"), new GUIContent("시점(초)"));
                EditorGUILayout.PropertyField(shake.FindPropertyRelative("_duration"), new GUIContent("길이(초)"));
                EditorGUILayout.PropertyField(shake.FindPropertyRelative("_amplitude"), new GUIContent("위치 세기(m)"));
                EditorGUILayout.PropertyField(shake.FindPropertyRelative("_rotationAmplitude"), new GUIContent("회전 세기(도)"));
                EditorGUILayout.PropertyField(shake.FindPropertyRelative("_frequency"), new GUIContent("빠르기(Hz)"));
                EditorGUILayout.HelpBox("게임 뷰에서 재생·스크러빙하면 흔들림이 보입니다. 기본 공격 위치 0.05~0.1 / 강한 타격 0.15~0.3", MessageType.None);
                if (GUILayout.Button("흔들림 삭제"))
                {
                    shakes.DeleteArrayElementAtIndex(_selectedShake);
                    _selectedShake = -1;
                }
            }
            else if (_selectedHit >= 0 && _selectedHit < hits.arraySize)
            {
                EditorGUILayout.LabelField("타격 " + (_selectedHit + 1), EditorStyles.boldLabel);
                SerializedProperty hit = hits.GetArrayElementAtIndex(_selectedHit);
                hit.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField("시점(초)", hit.floatValue));
                if (GUILayout.Button("타격 삭제"))
                {
                    hits.DeleteArrayElementAtIndex(_selectedHit);
                    _selectedHit = -1;
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "• 단축키: Space 재생/정지, ←/→ 한 프레임, Delete 선택 항목 삭제\n" +
                    "• 슬롯 바: 아군 스킬은 맞는 몬스터, 적 스킬은 시전하는 몬스터를 고릅니다. 모든 슬롯에서 확인하세요.\n" +
                    "• 눈금자/빈 곳 클릭·드래그: 시간 이동\n" +
                    "• 이펙트 블럭: 가운데 잡고 끌기 = 시점 이동, 왼쪽 끝 = 시작 조절(끝 고정), 오른쪽 끝 = 길이 조절\n" +
                    "• 애니메이션 블럭: 끌어서 시작 시점 이동\n" +
                    "• 이동 블럭: 가운데 = 전체 이동, 양 끝 = 출발·복귀 완료, 안쪽 두 선 = 도착·복귀 시작\n" +
                    "• 카메라 흔들림 블럭: 이펙트 블럭과 같은 방식 (+ 흔들림으로 추가, 게임 뷰에서 확인)\n" +
                    "• 타격 마커: 실제 전투에서 피해·피격 반응이 들어가는 순간 (드래그로 이동, Delete로 삭제)\n" +
                    "• 이펙트 이름 클릭 또는 씬에서 이펙트를 직접 클릭: 선택\n" +
                    "• 선택한 이펙트는 일반 오브젝트처럼 이동(W)·회전(E)·크기(R), 인스펙터 Transform 숫자로 조절 → 자동 저장\n" +
                    "• 위치는 '기준점 + 차이값'으로 저장되고, 방향은 '시전자가 대상을 바라보는 방향' 기준이라 슬롯이 바뀌어도 같은 부위·방향에 생성됩니다.\n" +
                    "• 카메라 무빙이 필요한 연출은 시퀀스 설정의 '컷신 Timeline'에 연결합니다(Timeline 창에서 편집).",
                    MessageType.None);
            }
        }

        private void DrawSelectedEvent(SerializedObject serialized, SerializedProperty events, int index)
        {
            SerializedProperty vfxEvent = events.GetArrayElementAtIndex(index);
            SerializedProperty anchor = vfxEvent.FindPropertyRelative("_anchor");
            EditorGUILayout.LabelField("이펙트 " + (index + 1), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(vfxEvent.FindPropertyRelative("_name"), new GUIContent("이름"));
            EditorGUILayout.PropertyField(vfxEvent.FindPropertyRelative("_time"), new GUIContent("시점(초)"));
            EditorGUILayout.PropertyField(vfxEvent.FindPropertyRelative("_prefab"), new GUIContent("프리팹"));
            EditorGUILayout.PropertyField(anchor, new GUIContent("기준점"));
            SkillVfxAnchorPoint anchorPoint = (SkillVfxAnchorPoint)anchor.intValue;
            if (anchorPoint == SkillVfxAnchorPoint.CasterBone)
            {
                EditorGUILayout.PropertyField(vfxEvent.FindPropertyRelative("_bone"), new GUIContent("본"));
            }
            if (anchorPoint == SkillVfxAnchorPoint.TargetCenter || anchorPoint == SkillVfxAnchorPoint.TargetFeet)
            {
                EditorGUILayout.PropertyField(vfxEvent.FindPropertyRelative("_targetMode"), new GUIContent("대상 범위"));
            }
            EditorGUILayout.PropertyField(vfxEvent.FindPropertyRelative("_followAnchor"), new GUIContent("기준점 따라가기"));
            EditorGUILayout.PropertyField(vfxEvent.FindPropertyRelative("_positionOffset"), new GUIContent("위치 차이"));
            EditorGUILayout.PropertyField(vfxEvent.FindPropertyRelative("_rotationOffset"), new GUIContent("회전 차이"));
            EditorGUILayout.PropertyField(vfxEvent.FindPropertyRelative("_scale"), new GUIContent("크기"));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(vfxEvent.FindPropertyRelative("_lifetime"), new GUIContent("길이(0=자동)", "타임라인 블럭 오른쪽 끝을 잡아 조절할 수도 있습니다. 0이면 파티클 길이로 자동 계산합니다."));
            if (GUILayout.Button("자동", GUILayout.Width(45f)))
            {
                vfxEvent.FindPropertyRelative("_lifetime").floatValue = 0f;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(vfxEvent.FindPropertyRelative("_playbackSpeed"), new GUIContent("재생 속도"));
            if (index < _sequence.VfxEvents.Count)
            {
                EditorGUILayout.LabelField(" ", $"실제 유지 시간 {_sequence.VfxEvents[index].GetEffectiveLifetime():0.00}s", EditorStyles.miniLabel);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("시점으로 이동"))
            {
                SetTime(vfxEvent.FindPropertyRelative("_time").floatValue);
            }
            if (GUILayout.Button("위치·회전 초기화"))
            {
                vfxEvent.FindPropertyRelative("_positionOffset").vector3Value = Vector3.zero;
                vfxEvent.FindPropertyRelative("_rotationOffset").vector3Value = Vector3.zero;
            }
            if (GUILayout.Button("복제"))
            {
                events.InsertArrayElementAtIndex(index);
                SerializedProperty copy = events.GetArrayElementAtIndex(index + 1);
                copy.FindPropertyRelative("_name").stringValue += " 복사";
                _selectedEvent = index + 1;
            }
            if (GUILayout.Button("삭제"))
            {
                DeleteVfxEvent(serialized, index);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void AddVfxEvent(SerializedObject serialized)
        {
            SerializedProperty events = serialized.FindProperty("_vfxEvents");
            int index = events.arraySize;
            events.arraySize++;
            SerializedProperty vfxEvent = events.GetArrayElementAtIndex(index);
            vfxEvent.FindPropertyRelative("_name").stringValue = "이펙트 " + (index + 1);
            vfxEvent.FindPropertyRelative("_time").floatValue = _time;
            vfxEvent.FindPropertyRelative("_prefab").objectReferenceValue = null;
            vfxEvent.FindPropertyRelative("_anchor").intValue = (int)SkillVfxAnchorPoint.TargetCenter;
            vfxEvent.FindPropertyRelative("_bone").intValue = (int)HumanBodyBones.RightHand;
            vfxEvent.FindPropertyRelative("_followAnchor").boolValue = false;
            vfxEvent.FindPropertyRelative("_targetMode").intValue = (int)SkillVfxTargetMode.SelectedTarget;
            vfxEvent.FindPropertyRelative("_positionOffset").vector3Value = Vector3.zero;
            vfxEvent.FindPropertyRelative("_rotationOffset").vector3Value = Vector3.zero;
            vfxEvent.FindPropertyRelative("_scale").floatValue = 1f;
            vfxEvent.FindPropertyRelative("_lifetime").floatValue = 0f;
            vfxEvent.FindPropertyRelative("_playbackSpeed").floatValue = 1f;
            _selectedEvent = index;
            _selectedHit = -1;
            _selectedShake = -1;
        }

        private void AddHit(SerializedObject serialized)
        {
            SerializedProperty hits = serialized.FindProperty("_hitTimes");
            hits.arraySize++;
            hits.GetArrayElementAtIndex(hits.arraySize - 1).floatValue = _time;
            _selectedHit = hits.arraySize - 1;
            _selectedEvent = -1;
            _selectedShake = -1;
        }

        private void AddShake(SerializedObject serialized)
        {
            SerializedProperty shakes = serialized.FindProperty("_shakeEvents");
            shakes.arraySize++;
            SerializedProperty shake = shakes.GetArrayElementAtIndex(shakes.arraySize - 1);
            shake.FindPropertyRelative("_time").floatValue = _time;
            shake.FindPropertyRelative("_duration").floatValue = 0.35f;
            shake.FindPropertyRelative("_amplitude").floatValue = 0.1f;
            shake.FindPropertyRelative("_rotationAmplitude").floatValue = 0.8f;
            shake.FindPropertyRelative("_frequency").floatValue = 22f;
            _selectedShake = shakes.arraySize - 1;
            _selectedEvent = -1;
            _selectedHit = -1;
        }

        private void DeleteVfxEvent(SerializedObject serialized, int index)
        {
            serialized.FindProperty("_vfxEvents").DeleteArrayElementAtIndex(index);
            _selectedEvent = -1;
            GUI.FocusControl(null);
        }

        private void SelectEvent(int index)
        {
            _selectedEvent = index;
            _selectedHit = -1;
            _selectedShake = -1;
            GUI.FocusControl(null);
            if (index < _sequence.VfxEvents.Count)
            {
                SetTime(_sequence.VfxEvents[index].Time);
                RefreshPreview();
                PreviewInstance preview = FindPreview(index);
                if (preview != null && preview.Instance != null && preview.Instance.activeSelf)
                {
                    Selection.activeGameObject = preview.Instance;
                }
                SceneView.RepaintAll();
            }
        }

        private PreviewInstance FindPreview(int eventIndex)
        {
            PreviewInstance first = null;
            foreach (PreviewInstance preview in _previews)
            {
                if (preview.EventIndex != eventIndex)
                {
                    continue;
                }
                if (first == null)
                {
                    first = preview;
                }
                if (_context != null && GetPreviewTarget(_sequence.VfxEvents[eventIndex], preview.TargetIndex) == _context.SelectedTarget)
                {
                    return preview;
                }
            }
            return first;
        }

        private void OnSelectionChanged()
        {
            if (_context != null ? IsStageActorSelected() : (_anchor != null && !IsStageActorSelected()))
            {
                RefreshPreview();
                SceneView.RepaintAll();
            }
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }
            foreach (PreviewInstance preview in _previews)
            {
                if (preview.Instance == null || !selected.transform.IsChildOf(preview.Instance.transform))
                {
                    continue;
                }
                // 파티클 자식을 클릭해도 이펙트 전체(루트)를 잡도록 바꾼다.
                if (selected != preview.Instance)
                {
                    Selection.activeGameObject = preview.Instance;
                    return;
                }
                _selectedEvent = preview.EventIndex;
                _selectedHit = -1;
                _selectedShake = -1;
                Repaint();
                return;
            }
        }

        /// <summary>
        /// 씬에서 이펙트 오브젝트를 직접 옮기거나(이동·회전·크기, 인스펙터 숫자 포함) 바꾸면
        /// 그 결과를 '기준점 + 차이값'과 크기로 되돌려 시퀀스에 저장한다.
        /// </summary>
        private bool ApplyManualEdits()
        {
            if (_sampler == null || _sequence == null || _context == null)
            {
                return false;
            }
            foreach (PreviewInstance preview in _previews)
            {
                if (preview.Instance == null || !preview.IsActive || preview.EventIndex >= _sequence.VfxEvents.Count)
                {
                    continue;
                }
                Transform instance = preview.Instance.transform;
                bool changed = (instance.position - preview.AppliedPosition).sqrMagnitude > 1e-8f
                    || Quaternion.Angle(instance.rotation, preview.AppliedRotation) > 0.01f
                    || (instance.localScale - preview.AppliedScale).sqrMagnitude > 1e-8f;
                if (!changed)
                {
                    continue;
                }

                _isPlaying = false;
                SkillVfxEvent vfxEvent = _sequence.VfxEvents[preview.EventIndex];
                Transform target = GetPreviewTarget(vfxEvent, preview.TargetIndex);
                Vector3 position = instance.position;
                Quaternion rotation = instance.rotation;

                // 따라가는 이펙트는 현재 시간의 기준점 기준 위치를 생성 순간으로 되돌려 계산한다.
                _sampler.Sample(_time);
                _context.GetSpawnPose(vfxEvent, target, out _, out _, out Transform parent);
                Vector3 localPosition = Vector3.zero;
                Quaternion localRotation = Quaternion.identity;
                if (parent != null)
                {
                    localPosition = parent.InverseTransformPoint(position);
                    localRotation = Quaternion.Inverse(parent.rotation) * rotation;
                }
                _sampler.Sample(vfxEvent.Time);
                if (parent != null)
                {
                    position = parent.TransformPoint(localPosition);
                    rotation = parent.rotation * localRotation;
                }
                _context.ConvertToOffsets(vfxEvent, target, position, rotation, out Vector3 positionOffset, out Vector3 rotationOffset);
                float baseScale = preview.Prefab.transform.localScale.magnitude;
                float scale = baseScale > 0.0001f ? instance.localScale.magnitude / baseScale : vfxEvent.Scale;

                SerializedObject serialized = GetSerialized();
                SerializedProperty element = serialized.FindProperty("_vfxEvents").GetArrayElementAtIndex(preview.EventIndex);
                element.FindPropertyRelative("_positionOffset").vector3Value = positionOffset;
                element.FindPropertyRelative("_rotationOffset").vector3Value = rotationOffset;
                element.FindPropertyRelative("_scale").floatValue = Mathf.Max(0.01f, scale);
                serialized.ApplyModifiedProperties();
                // 미리보기 오브젝트의 Transform 기록은 지워 Undo가 시퀀스 값만 되돌리게 한다.
                Undo.ClearUndo(instance);
                _selectedEvent = preview.EventIndex;
                _selectedHit = -1;
                return true;
            }
            return false;
        }

        #endregion

        #region 씬 뷰

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_context == null || _sequence == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            DrawStageLabels();
            if (_selectedEvent < 0 || _selectedEvent >= _sequence.VfxEvents.Count || _sampler == null)
            {
                return;
            }

            PreviewInstance selectedPreview = FindPreview(_selectedEvent);
            if (selectedPreview != null && selectedPreview.Instance != null && Selection.activeGameObject == selectedPreview.Instance)
            {
                // 이펙트 오브젝트가 선택돼 있으면 유니티 기본 도구(W/E/R)를 그대로 쓴다.
                return;
            }

            SkillVfxEvent vfxEvent = _sequence.VfxEvents[_selectedEvent];
            Transform target = _context.SelectedTarget;
            // 생성 순간의 포즈 기준으로 핸들을 그린다(본·소켓 기준점은 애니메이션에 따라 달라지므로).
            _sampler.Sample(vfxEvent.Time);
            _context.GetSpawnPose(vfxEvent, target, out Vector3 position, out Quaternion rotation, out _);
            Handles.color = Color.white;
            Handles.Label(position + Vector3.up * 0.25f, vfxEvent.Name, EditorStyles.whiteBoldLabel);
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = position;
            Quaternion newRotation = rotation;
            if (Tools.current == Tool.Rotate)
            {
                newRotation = Handles.RotationHandle(rotation, position);
            }
            else
            {
                Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Local ? rotation : Quaternion.identity;
                newPosition = Handles.PositionHandle(position, handleRotation);
            }
            if (EditorGUI.EndChangeCheck())
            {
                _context.ConvertToOffsets(vfxEvent, target, newPosition, newRotation, out Vector3 positionOffset, out Vector3 rotationOffset);
                SerializedObject serialized = GetSerialized();
                SerializedProperty element = serialized.FindProperty("_vfxEvents").GetArrayElementAtIndex(_selectedEvent);
                element.FindPropertyRelative("_positionOffset").vector3Value = positionOffset;
                element.FindPropertyRelative("_rotationOffset").vector3Value = rotationOffset;
                serialized.ApplyModifiedProperties();
                RefreshPreview();
                Repaint();
            }
            else
            {
                _sampler.Sample(_time);
            }
        }

        private void DrawStageLabels()
        {
            int activeSlot = ActiveSlot;
            bool isEnemySkill = IsEnemySkill;
            List<Transform> slots = GetSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                bool isActive = i == activeSlot;
                string role = isActive ? (isEnemySkill ? " (시전)" : " (대상)") : "";
                DrawActorLabel(slots[i], $"슬롯 {i + 1}{role}", isActive, isEnemySkill ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.8f, 0.2f));
            }
            if (_anchor != null && _anchor.Player != null)
            {
                DrawActorLabel(_anchor.Player, isEnemySkill ? "플레이어 (대상)" : "플레이어 (시전)", true, isEnemySkill ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 0.8f, 1f));
            }
            if (_sequence.UseMovement && _sampler != null && _context.SelectedTarget != null)
            {
                Vector3 home = _context.CasterHome.position;
                Vector3 approach = _sampler.ApproachPoint;
                Handles.color = new Color(0.3f, 0.9f, 1f);
                Handles.DrawDottedLine(home + Vector3.up * 0.05f, approach + Vector3.up * 0.05f, 4f);
                Handles.DrawWireDisc(approach + Vector3.up * 0.02f, Vector3.up, 0.3f, 2f);
                Handles.Label(approach + Vector3.up * 0.1f, "공격 위치", EditorStyles.whiteMiniLabel);
            }
        }

        private void DrawActorLabel(Transform actor, string label, bool highlight, Color color)
        {
            Vector3 feet = _context.GetTargetFeet(actor);
            Vector3 center = _context.GetTargetCenter(actor);
            float radius = _context.GetTargetRadius(actor);
            Handles.color = highlight ? color : new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawWireDisc(feet + Vector3.up * 0.02f, Vector3.up, radius + 0.15f, highlight ? 3f : 1f);
            Handles.Label(center + Vector3.up * (center.y - feet.y + 0.3f), label, EditorStyles.whiteBoldLabel);
        }

        private void FrameStage()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || _anchor == null || _anchor.Player == null)
            {
                return;
            }
            Bounds bounds = new Bounds(_anchor.Player.position + Vector3.up, new Vector3(2f, 2f, 2f));
            foreach (Transform slot in GetSlots())
            {
                bounds.Encapsulate(new Bounds(slot.position + Vector3.up, new Vector3(2f, 2f, 2f)));
            }
            sceneView.Frame(bounds, false);
        }

        #endregion

        #region 씬 무대

        private List<Transform> GetSlots()
        {
            List<Transform> slots = new List<Transform>();
            if (_anchor != null && _anchor.MonsterSlots != null)
            {
                foreach (Transform slot in _anchor.MonsterSlots)
                {
                    if (slot != null && slot != _anchor.Player)
                    {
                        slots.Add(slot);
                    }
                }
            }
            return slots;
        }

        private string GetStageSignature()
        {
            if (_anchor == null || _anchor.Player == null)
            {
                return string.Empty;
            }
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append(_anchor.Player.GetInstanceID());
            foreach (Transform slot in GetSlots())
            {
                builder.Append(',').Append(slot.GetInstanceID());
            }
            return builder.ToString();
        }

        /// <summary>플레이어·몬스터를 선택하면 미리보기를 잠시 멈추고 원래 자리로 돌려 배치를 고칠 수 있게 한다.</summary>
        private bool IsStageActorSelected()
        {
            if (_anchor == null)
            {
                return false;
            }
            foreach (Transform selected in Selection.transforms)
            {
                if (_anchor.Player != null && selected.IsChildOf(_anchor.Player))
                {
                    return true;
                }
                foreach (Transform slot in GetSlots())
                {
                    if (selected.IsChildOf(slot))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool EnsureStage()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }
            if (_anchor == null)
            {
                _anchor = FindStageAnchor();
            }
            if (_anchor == null || _anchor.Player == null || IsStageActorSelected())
            {
                return false;
            }
            if (_stageRoot == null)
            {
                DestroyStaleStages();
                _stageRoot = new GameObject(StageName) { hideFlags = HideFlags.DontSave };
            }
            return true;
        }

        /// <summary>
        /// 아군 스킬: 플레이어가 시전, 슬롯 몬스터 전체가 대상(선택 슬롯이 주 대상).
        /// 적 스킬: 선택 슬롯 몬스터가 시전, 플레이어가 대상.
        /// </summary>
        private void RebuildContext()
        {
            ReleaseActors();
            _contextSlot = ActiveSlot;
            _contextForEnemySkill = IsEnemySkill;
            _contextSignature = GetStageSignature();
            if (_anchor == null || _anchor.Player == null)
            {
                return;
            }
            Transform player = _anchor.Player;
            List<Transform> slots = GetSlots();
            SavePoses(player);
            foreach (Transform slot in slots)
            {
                SavePoses(slot);
            }
            if (IsEnemySkill && _contextSlot < slots.Count)
            {
                _contextTargets.Add(player);
                _context = new SkillSequenceContext(slots[_contextSlot], _contextTargets, _anchor.transform, 0);
            }
            else
            {
                _contextTargets.AddRange(slots);
                _context = new SkillSequenceContext(player, _contextTargets, _anchor.transform, _contextSlot);
            }
        }

        /// <summary>미리보기를 멈추고 씬의 플레이어·몬스터를 원래 자리·포즈로 되돌린다.</summary>
        private void ReleaseActors()
        {
            DisposeSampler();
            ClearPreviews();
            RestorePoses(true);
            _context = null;
            _contextTargets.Clear();
            _contextSignature = null;
            SkillCameraShake.Clear();
        }

        private void SavePoses(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                _savedPoses.Add(new TransformState
                {
                    Transform = child,
                    LocalPosition = child.localPosition,
                    LocalRotation = child.localRotation,
                    LocalScale = child.localScale
                });
            }
        }

        private void RestorePoses(bool clear)
        {
            foreach (TransformState state in _savedPoses)
            {
                if (state.Transform != null)
                {
                    state.Transform.localPosition = state.LocalPosition;
                    state.Transform.localRotation = state.LocalRotation;
                    state.Transform.localScale = state.LocalScale;
                }
            }
            if (clear)
            {
                _savedPoses.Clear();
            }
        }

        private void EnsureSampler()
        {
            if (_sequence == null || _context == null)
            {
                DisposeSampler();
                return;
            }
            if (_sampler != null && _samplerClip == _sequence.CasterClip)
            {
                return;
            }
            DisposeSampler();
            _sampler = new SkillSequenceSampler(_sequence, _context);
            _samplerClip = _sequence.CasterClip;
        }

        private void DisposeSampler()
        {
            if (_sampler != null)
            {
                _sampler.Dispose();
                _sampler = null;
            }
            _samplerClip = null;
        }

        private void DestroyStage()
        {
            ClearSelectionUnder(_stageRoot);
            ReleaseActors();
            if (_stageRoot != null)
            {
                DestroyImmediate(_stageRoot);
            }
            _stageRoot = null;
            _contextSlot = -1;
            RepaintGameView();
        }

        // 씬을 저장할 때는 원래 포즈·설정(루트 모션 등)으로 되돌려 저장하고, 저장이 끝나면 다시 미리보기한다.
        private void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            DisposeSampler();
            RestorePoses(false);
        }

        private void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            _needsRefresh = true;
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    RefreshPreview();
                }
            };
        }

        private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene next)
        {
            DestroyStage();
            _anchor = null;
            _needsRefresh = true;
            Repaint();
        }

        private static SkillStudioStageAnchor FindStageAnchor()
        {
            return FindFirstObjectByType<SkillStudioStageAnchor>();
        }

        private static void DestroyStaleStages()
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate != null && (candidate.name == StageName || candidate.name == LegacyStageName)
                    && candidate.transform.parent == null && !EditorUtility.IsPersistent(candidate))
                {
                    DestroyImmediate(candidate);
                }
            }
        }

        /// <summary>지울 오브젝트가 선택돼 있으면 인스펙터 오류가 나므로 먼저 선택을 해제한다.</summary>
        private static void ClearSelectionUnder(GameObject root)
        {
            GameObject selected = Selection.activeGameObject;
            if (root != null && selected != null && selected.transform.IsChildOf(root.transform))
            {
                Selection.activeObject = null;
            }
        }

        private static void SetHideFlagsRecursive(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.DontSave;
            }
        }

        #endregion

        #region 이펙트 미리보기

        private void RefreshPreview()
        {
            _needsRefresh = false;
            if (!EnsureStage())
            {
                // 무대 설정이 없거나 플레이어·몬스터를 옮기는 중이면 원래 자리로 두고 미리보기를 쉰다.
                if (_context != null)
                {
                    ReleaseActors();
                }
                RepaintGameView();
                return;
            }
            if (_context == null || _contextSlot != ActiveSlot || _contextForEnemySkill != IsEnemySkill || _contextSignature != GetStageSignature())
            {
                RebuildContext();
            }
            EnsureSampler();
            if (_sampler == null)
            {
                ClearPreviews();
                return;
            }
            SyncPreviewInstances();

            // 1) 각 이펙트의 생성 순간 포즈를 구한다.
            IReadOnlyList<SkillVfxEvent> events = _sequence.VfxEvents;
            foreach (PreviewInstance preview in _previews)
            {
                SkillVfxEvent vfxEvent = events[preview.EventIndex];
                preview.LocalTime = _time - vfxEvent.Time;
                preview.IsActive = preview.LocalTime >= 0f && preview.LocalTime <= vfxEvent.GetEffectiveLifetime();
                if (!preview.IsActive)
                {
                    continue;
                }
                _sampler.Sample(vfxEvent.Time);
                Transform target = GetPreviewTarget(vfxEvent, preview.TargetIndex);
                _context.GetSpawnPose(vfxEvent, target, out preview.Position, out preview.Rotation, out preview.Parent);
                if (preview.Parent != null)
                {
                    preview.LocalPosition = preview.Parent.InverseTransformPoint(preview.Position);
                    preview.LocalRotation = Quaternion.Inverse(preview.Parent.rotation) * preview.Rotation;
                }
            }

            // 2) 현재 시간 포즈를 적용하고, 따라가는 이펙트는 기준점 기준으로 옮긴 뒤 파티클을 해당 시간만큼 시뮬레이션한다.
            _sampler.Sample(_time);
            foreach (PreviewInstance preview in _previews)
            {
                if (preview.Instance == null)
                {
                    continue;
                }
                if (!preview.IsActive)
                {
                    preview.Instance.SetActive(false);
                    continue;
                }
                SkillVfxEvent vfxEvent = events[preview.EventIndex];
                Vector3 position = preview.Position;
                Quaternion rotation = preview.Rotation;
                if (preview.Parent != null)
                {
                    position = preview.Parent.TransformPoint(preview.LocalPosition);
                    rotation = preview.Parent.rotation * preview.LocalRotation;
                }
                preview.Instance.SetActive(true);
                preview.Instance.transform.SetPositionAndRotation(position, rotation);
                preview.Instance.transform.localScale = preview.Prefab.transform.localScale * vfxEvent.Scale;
                preview.AppliedPosition = preview.Instance.transform.position;
                preview.AppliedRotation = preview.Instance.transform.rotation;
                preview.AppliedScale = preview.Instance.transform.localScale;
                float simulateTime = preview.LocalTime * vfxEvent.PlaybackSpeed;
                foreach (ParticleSystem particle in preview.Particles)
                {
                    if (particle != null)
                    {
                        particle.Simulate(simulateTime, false, true, false);
                    }
                }
                EvaluateTimelineAndAnimators(preview, simulateTime);
            }
            UpdateGameViewShake();
        }

        /// <summary>
        /// 이펙터가 Timeline(PlayableDirector)이나 Animator로 만든 이펙트를 해당 시간 모습으로 맞춘다.
        /// Timeline의 Control 트랙이 파티클을 다루는 경우를 위해 파티클 시뮬레이션 뒤에 평가한다.
        /// </summary>
        private static void EvaluateTimelineAndAnimators(PreviewInstance preview, float time)
        {
            if (preview.Animators != null)
            {
                foreach (Animator animator in preview.Animators)
                {
                    if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled)
                    {
                        continue;
                    }
                    animator.Rebind();
                    animator.Update(0f);
                    animator.Update(time);
                }
            }
            if (preview.Directors != null)
            {
                foreach (UnityEngine.Playables.PlayableDirector director in preview.Directors)
                {
                    if (director == null || director.playableAsset == null || !director.isActiveAndEnabled)
                    {
                        continue;
                    }
                    director.time = Mathf.Min(time, (float)director.playableAsset.duration);
                    director.Evaluate();
                }
            }
        }

        /// <summary>게임 뷰(씬 메인 카메라)에 카메라 흔들림을 적용한다. 렌더링할 때만 더하므로 카메라 위치는 바뀌지 않는다.</summary>
        private void UpdateGameViewShake()
        {
            Camera camera = Camera.main;
            if (_sequence != null && camera != null && SkillCameraShake.Evaluate(_sequence.ShakeEvents, _time, out Vector3 shakePosition, out Vector3 shakeRotation))
            {
                SkillCameraShake.SetOffset(camera, shakePosition, shakeRotation);
            }
            else
            {
                SkillCameraShake.Clear();
            }
            RepaintGameView();
        }

        private static void RepaintGameView()
        {
            System.Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                return;
            }
            foreach (EditorWindow gameView in Resources.FindObjectsOfTypeAll(gameViewType))
            {
                gameView.Repaint();
            }
        }

        private Transform GetPreviewTarget(SkillVfxEvent vfxEvent, int targetIndex)
        {
            int index = 0;
            foreach (Transform target in _context.GetEventTargets(vfxEvent))
            {
                if (index == targetIndex)
                {
                    return target;
                }
                index++;
            }
            return _context.SelectedTarget;
        }

        private void SyncPreviewInstances()
        {
            List<KeyValuePair<int, int>> wanted = new List<KeyValuePair<int, int>>();
            IReadOnlyList<SkillVfxEvent> events = _sequence.VfxEvents;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Prefab == null)
                {
                    continue;
                }
                int targetCount = 0;
                foreach (Transform unused in _context.GetEventTargets(events[i]))
                {
                    targetCount++;
                }
                for (int t = 0; t < targetCount; t++)
                {
                    wanted.Add(new KeyValuePair<int, int>(i, t));
                }
            }

            bool matches = wanted.Count == _previews.Count;
            for (int i = 0; matches && i < wanted.Count; i++)
            {
                PreviewInstance preview = _previews[i];
                matches = preview.Instance != null && preview.EventIndex == wanted[i].Key && preview.TargetIndex == wanted[i].Value
                    && preview.Prefab == events[wanted[i].Key].Prefab;
            }
            if (matches)
            {
                return;
            }

            ClearPreviews();
            foreach (KeyValuePair<int, int> pair in wanted)
            {
                GameObject prefab = events[pair.Key].Prefab;
                GameObject instance = Instantiate(prefab, _stageRoot.transform);
                instance.name = "Vfx_" + (pair.Key + 1) + "_" + (pair.Value + 1);
                SetHideFlagsRecursive(instance);
                ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (ParticleSystem particle in particles)
                {
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    // 슬라이더를 움직여도 같은 모양이 나오도록 시드를 고정한다.
                    particle.useAutoRandomSeed = false;
                    particle.randomSeed = (uint)(pair.Key * 7919 + pair.Value * 104729 + 1);
                }
                // 이펙트 안의 Timeline은 스튜디오가 시간을 직접 정하므로 자동 재생을 끈다.
                UnityEngine.Playables.PlayableDirector[] directors = instance.GetComponentsInChildren<UnityEngine.Playables.PlayableDirector>(true);
                foreach (UnityEngine.Playables.PlayableDirector director in directors)
                {
                    director.playOnAwake = false;
                    director.timeUpdateMode = UnityEngine.Playables.DirectorUpdateMode.Manual;
                }
                Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
                instance.SetActive(false);
                _previews.Add(new PreviewInstance
                {
                    EventIndex = pair.Key,
                    TargetIndex = pair.Value,
                    Prefab = prefab,
                    Instance = instance,
                    Particles = particles,
                    Directors = directors,
                    Animators = animators
                });
            }
        }

        private void ClearPreviews()
        {
            foreach (PreviewInstance preview in _previews)
            {
                if (preview.Instance != null)
                {
                    ClearSelectionUnder(preview.Instance);
                    DestroyImmediate(preview.Instance);
                }
            }
            _previews.Clear();
        }

        #endregion

        #region 기타

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(now - _lastUpdateTime);
            _lastUpdateTime = now;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && ApplyManualEdits())
            {
                RefreshPreview();
                Repaint();
                SceneView.RepaintAll();
            }
            if (!_isPlaying || _sequence == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            float time = _time + deltaTime * _playSpeed;
            if (time >= _sequence.Duration)
            {
                if (_loop)
                {
                    time = 0f;
                }
                else
                {
                    time = _sequence.Duration;
                    _isPlaying = false;
                }
            }
            _time = time;
            RefreshPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                _isPlaying = false;
                DestroyStage();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                _needsRefresh = true;
                Repaint();
            }
        }

        private void OnUndoRedo()
        {
            if (_serialized != null)
            {
                _serialized.Update();
            }
            _needsRefresh = true;
            Repaint();
        }

        private void OnSequenceChanged()
        {
            _serialized = null;
            _selectedEvent = -1;
            _selectedHit = -1;
            _selectedShake = -1;
            _isPlaying = false;
            _time = 0f;
            RebuildContext();
            _needsRefresh = true;
        }

        private void SetTime(float time)
        {
            _time = Mathf.Max(0f, time);
            _needsRefresh = true;
        }

        private SerializedObject GetSerialized()
        {
            if (_serialized == null || _serialized.targetObject != _sequence)
            {
                _serialized = new SerializedObject(_sequence);
            }
            _serialized.Update();
            return _serialized;
        }

        private void CreateSequence()
        {
            string folder = AssetDatabase.IsValidFolder(DefaultSequenceFolder) ? DefaultSequenceFolder : "Assets/CK_Semester_Project";
            string path = EditorUtility.SaveFilePanelInProject("새 스킬 시퀀스", "SkillSequence", "asset", "스킬 시퀀스를 저장할 위치를 고르세요.", folder);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            SkillSequenceAsset asset = CreateInstance<SkillSequenceAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _sequence = asset;
            OnSequenceChanged();
            EditorGUIUtility.PingObject(asset);
        }

        #endregion
    }
}
