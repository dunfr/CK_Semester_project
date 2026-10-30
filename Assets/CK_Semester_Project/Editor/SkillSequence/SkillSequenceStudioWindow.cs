using System.Collections.Generic;
using CK.SemesterProject.SkillSequence;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.SkillSequence.EditorTools
{
    /// <summary>
    /// 스킬 연출 스튜디오. 애니메이션을 배치하고, 타임라인에 이펙트 생성 시점·타격 시점을 찍고,
    /// 씬 뷰 핸들로 이펙트 위치를 잡는다. 몬스터 접근 이동과 몬스터 위치(포메이션 슬롯) 구분은 선택 기능이다.
    /// 미리보기 무대는 저장되지 않는 임시 오브젝트(HideFlags.DontSave)로 만든다.
    /// </summary>
    public sealed class SkillSequenceStudioWindow : EditorWindow
    {
        private const string StageName = "__SkillSequenceStudio";
        private const string DefaultCasterPath = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Prefabs/FuchsiaUpdatedAnimated.prefab";
        private const string DefaultSequenceFolder = "Assets/CK_Semester_Project/Data/SkillSequences";
        private const float LabelWidth = 120f;
        private const float RulerHeight = 20f;
        private const float RowHeight = 22f;
        private const float MarkerWidth = 9f;
        private const float EdgeGrabWidth = 6f;
        private const int FixedRowCount = 3;
        private static readonly Vector3 StageOrigin = new Vector3(500f, 0f, 500f);

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
            ReturnStart,
            ReturnEnd
        }

        private sealed class PreviewInstance
        {
            public int EventIndex;
            public int TargetIndex;
            public GameObject Prefab;
            public GameObject Instance;
            public ParticleSystem[] Particles;
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
        [SerializeField] private GameObject _casterPrefab;
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private bool _useFormation = true;
        [SerializeField] private int _enemyCount = 3;
        [SerializeField] private int _targetSlot = 1;
        [SerializeField] private float _slotSpacing = 2.5f;
        [SerializeField] private float _stageDistance = 7f;
        [SerializeField] private float _enemyScale = 1f;
        [SerializeField] private bool _snapToFrame = true;
        [SerializeField] private bool _loop = true;
        [SerializeField] private float _playSpeed = 1f;
        [SerializeField] private bool _showStageSettings = true;
        [SerializeField] private bool _showSequenceSettings = true;

        private readonly List<Transform> _enemies = new List<Transform>();
        private readonly List<PreviewInstance> _previews = new List<PreviewInstance>();
        private float _time;
        private bool _isPlaying;
        private double _lastUpdateTime;
        private int _selectedEvent = -1;
        private int _selectedHit = -1;
        private DragTarget _drag;
        private int _dragIndex;
        private float _dragGrabOffset;
        private float _dragEventEnd;
        private float _dragViewDuration;
        private bool _needsRefresh;
        private Vector2 _scroll;
        private Rect _trackRect;
        private GameObject _stageRoot;
        private GameObject _caster;
        private Transform _stageCenter;
        private SkillSequenceContext _context;
        private SkillSequenceSampler _sampler;
        private AnimationClip _samplerClip;
        private SerializedObject _serialized;

        private int TargetIndex => _useFormation ? Mathf.Clamp(_targetSlot, 0, Mathf.Max(0, _enemyCount - 1)) : 0;

        [MenuItem("CK/스킬 연출 스튜디오")]
        private static void Open()
        {
            GetWindow<SkillSequenceStudioWindow>("스킬 연출 스튜디오").Show();
        }

        #region Unity 이벤트

        private void OnEnable()
        {
            titleContent = new GUIContent("스킬 연출 스튜디오");
            if (_casterPrefab == null)
            {
                _casterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCasterPath);
            }
            SceneView.duringSceneGui += OnSceneGUI;
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
            DrawSequenceSettings(serialized);
            DrawTransport(serialized);
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
            _showStageSettings = EditorGUILayout.Foldout(_showStageSettings, "미리보기 무대", true);
            if (!_showStageSettings)
            {
                return;
            }
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            _casterPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("시전자", "애니메이션을 적용할 캐릭터 프리팹(Animator 필요)"), _casterPrefab, typeof(GameObject), false);
            _enemyPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("몬스터", "비우면 캡슐 더미를 사용합니다."), _enemyPrefab, typeof(GameObject), false);
            _enemyScale = EditorGUILayout.Slider(new GUIContent("몬스터 크기", "몬스터 크기가 달라도 이펙트가 맞는지 확인용"), _enemyScale, 0.3f, 4f);
            _stageDistance = EditorGUILayout.Slider(new GUIContent("아군-적 거리", "시전자 제자리와 몬스터 줄 사이 거리(m)"), _stageDistance, 2f, 20f);
            _useFormation = EditorGUILayout.Toggle(new GUIContent("몬스터 위치 구분", "켜면 여러 몬스터를 슬롯(포메이션)에 세우고 대상 슬롯을 고릅니다."), _useFormation);
            if (_useFormation)
            {
                _enemyCount = EditorGUILayout.IntSlider("몬스터 수", _enemyCount, 1, 5);
                _slotSpacing = EditorGUILayout.Slider("슬롯 간격", _slotSpacing, 1f, 6f);
                string[] labels = new string[_enemyCount];
                for (int i = 0; i < _enemyCount; i++)
                {
                    labels[i] = "슬롯 " + (i + 1);
                }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("대상 슬롯");
                _targetSlot = GUILayout.Toolbar(Mathf.Clamp(_targetSlot, 0, _enemyCount - 1), labels);
                EditorGUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck())
            {
                BuildStage();
                _needsRefresh = true;
            }
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 15f);
            if (GUILayout.Button("무대 다시 만들기"))
            {
                BuildStage();
                _needsRefresh = true;
            }
            if (GUILayout.Button("씬 뷰로 보기"))
            {
                FrameStage();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
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
            SerializedProperty useMovement = serialized.FindProperty("_useMovement");
            EditorGUILayout.PropertyField(useMovement, new GUIContent("몬스터에게 다가가기", "켜면 접근→공격→복귀 이동을 합니다(기본 공격용). 특수 스킬처럼 제자리에서 쓰면 끄세요."));
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
            EditorGUILayout.EndHorizontal();
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
                for (int i = 0; i < events.arraySize; i++)
                {
                    DrawEventRow(area, events.GetArrayElementAtIndex(i), i, viewDuration);
                }
                float endX = TimeToX(_sequence.Duration, viewDuration);
                EditorGUI.DrawRect(new Rect(endX, area.y, 1f, area.height), new Color(1f, 0.4f, 0.3f, 0.8f));
                float playheadX = TimeToX(_time, viewDuration);
                EditorGUI.DrawRect(new Rect(playheadX - 1f, area.y, 2f, area.height), new Color(1f, 0.85f, 0.2f));
            }
            DrawRowLabels(area, events);
            for (int i = 0; i < events.arraySize; i++)
            {
                AddBlockCursors(GetEventBlockRect(area, i, events.GetArrayElementAtIndex(i).FindPropertyRelative("_time").floatValue, viewDuration));
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
                    if (_drag != DragTarget.VfxEventStart && _drag != DragTarget.VfxEventEnd)
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
                case EventType.KeyDown:
                    if (current.keyCode == KeyCode.Delete && GUIUtility.keyboardControl == 0)
                    {
                        if (_selectedEvent >= 0 && _selectedEvent < events.arraySize)
                        {
                            DeleteVfxEvent(serialized, _selectedEvent);
                            current.Use();
                        }
                        else if (_selectedHit >= 0 && _selectedHit < hits.arraySize)
                        {
                            hits.DeleteArrayElementAtIndex(_selectedHit);
                            _selectedHit = -1;
                            current.Use();
                        }
                    }
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
            Rect hitRow = GetTrackRow(area, 2);
            for (int i = 0; i < hits.arraySize; i++)
            {
                if (Expand(GetHandleRect(hitRow, hits.GetArrayElementAtIndex(i).floatValue, viewDuration)).Contains(mouse))
                {
                    _drag = DragTarget.Hit;
                    _dragIndex = i;
                    _selectedHit = i;
                    _selectedEvent = -1;
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
            if (_drag == DragTarget.VfxEventMove || _drag == DragTarget.HoldMove || _drag == DragTarget.AnimationMove)
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
            EditorGUILayout.Space(6f);
            if (_selectedEvent >= 0 && _selectedEvent < events.arraySize)
            {
                DrawSelectedEvent(serialized, events, _selectedEvent);
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
                    "• 눈금자/빈 곳 클릭·드래그: 시간 이동\n" +
                    "• 이펙트 블럭: 가운데 잡고 끌기 = 시점 이동, 왼쪽 끝 = 시작 조절(끝 고정), 오른쪽 끝 = 길이 조절\n" +
                    "• 애니메이션 블럭: 끌어서 시작 시점 이동\n" +
                    "• 이동 블럭: 가운데 = 전체 이동, 양 끝 = 출발·복귀 완료, 안쪽 두 선 = 도착·복귀 시작\n" +
                    "• 타격 마커: 실제 전투에서 피해·피격 반응이 들어가는 순간 (드래그로 이동, Delete로 삭제)\n" +
                    "• 이펙트 이름 클릭 또는 씬에서 이펙트를 직접 클릭: 선택\n" +
                    "• 선택한 이펙트는 일반 오브젝트처럼 이동(W)·회전(E)·크기(R), 인스펙터 Transform 숫자로 조절 → 자동 저장\n" +
                    "• 위치는 '기준점 + 차이값'으로 저장되므로 몬스터 크기·위치가 달라도 같은 부위에 생성됩니다.",
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
        }

        private void AddHit(SerializedObject serialized)
        {
            SerializedProperty hits = serialized.FindProperty("_hitTimes");
            hits.arraySize++;
            hits.GetArrayElementAtIndex(hits.arraySize - 1).floatValue = _time;
            _selectedHit = hits.arraySize - 1;
            _selectedEvent = -1;
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
            if (_stageRoot == null || _context == null || _sequence == null || EditorApplication.isPlayingOrWillChangePlaymode)
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
            int targetIndex = _context.SelectedTargetIndex;
            for (int i = 0; i < _enemies.Count; i++)
            {
                Transform enemy = _enemies[i];
                if (enemy == null)
                {
                    continue;
                }
                bool isTarget = i == targetIndex;
                Vector3 feet = _context.GetTargetFeet(enemy);
                float radius = _context.GetTargetRadius(enemy);
                Handles.color = isTarget ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 1f, 1f, 0.35f);
                Handles.DrawWireDisc(feet + Vector3.up * 0.02f, Vector3.up, radius + 0.15f, isTarget ? 3f : 1f);
                Vector3 top = _context.GetTargetCenter(enemy) + Vector3.up * (_context.GetTargetCenter(enemy).y - feet.y + 0.3f);
                Handles.Label(top, _useFormation ? $"슬롯 {i + 1}{(isTarget ? " (대상)" : "")}" : "대상", EditorStyles.whiteBoldLabel);
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

        private void FrameStage()
        {
            if (!EnsureStage())
            {
                return;
            }
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }
            Bounds bounds = new Bounds(_stageCenter.position + Vector3.up, new Vector3(_slotSpacing * Mathf.Max(1, _enemyCount) + 4f, 3f, _stageDistance + 4f));
            sceneView.Frame(bounds, false);
        }

        #endregion

        #region 미리보기 무대

        private bool EnsureStage()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }
            if (_stageRoot == null || _caster == null)
            {
                BuildStage();
            }
            return _stageRoot != null;
        }

        private void BuildStage()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            DestroyStage();
            _stageRoot = new GameObject(StageName);
            _stageRoot.transform.position = StageOrigin;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.SetParent(_stageRoot.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0f, _stageDistance * 0.5f);
            floor.transform.localScale = new Vector3(Mathf.Max(3f, _slotSpacing * _enemyCount * 0.2f + 1f), 1f, Mathf.Max(2f, _stageDistance * 0.2f + 1f));

            _stageCenter = new GameObject("StageCenter").transform;
            _stageCenter.SetParent(_stageRoot.transform, false);
            _stageCenter.localPosition = new Vector3(0f, 0f, _stageDistance * 0.5f);

            _caster = _casterPrefab != null ? Instantiate(_casterPrefab) : CreateDummy();
            _caster.name = "Caster";
            _caster.transform.SetParent(_stageRoot.transform, false);
            _caster.transform.localPosition = Vector3.zero;
            _caster.transform.localRotation = Quaternion.identity;

            int count = _useFormation ? _enemyCount : 1;
            for (int i = 0; i < count; i++)
            {
                GameObject enemy = _enemyPrefab != null ? Instantiate(_enemyPrefab) : CreateDummy();
                enemy.name = "Enemy_" + (i + 1);
                enemy.transform.SetParent(_stageRoot.transform, false);
                float x = (i - (count - 1) * 0.5f) * _slotSpacing;
                enemy.transform.localPosition = new Vector3(x, 0f, _stageDistance);
                enemy.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                enemy.transform.localScale *= _enemyScale;
                _enemies.Add(enemy.transform);
            }
            SetHideFlagsRecursive(_stageRoot);
            // 씬에서 클릭하면 이펙트만 잡히도록 바닥·캐릭터·몬스터는 선택을 막는다.
            SceneVisibilityManager.instance.DisablePicking(floor, true);
            SceneVisibilityManager.instance.DisablePicking(_caster, true);
            foreach (Transform enemy in _enemies)
            {
                SceneVisibilityManager.instance.DisablePicking(enemy.gameObject, true);
            }
            RebuildContext();
        }

        private static GameObject CreateDummy()
        {
            GameObject root = new GameObject("Dummy");
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up;
            return root;
        }

        private void RebuildContext()
        {
            DisposeSampler();
            ClearPreviews();
            if (_caster == null)
            {
                _context = null;
                return;
            }
            _caster.transform.localPosition = Vector3.zero;
            _caster.transform.localRotation = Quaternion.identity;
            _context = new SkillSequenceContext(_caster.transform, _enemies, _stageCenter, TargetIndex);
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
            DisposeSampler();
            ClearPreviews();
            if (_stageRoot != null)
            {
                DestroyImmediate(_stageRoot);
            }
            _stageRoot = null;
            _caster = null;
            _stageCenter = null;
            _enemies.Clear();
            _context = null;
        }

        private static void DestroyStaleStages()
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate != null && candidate.name == StageName && candidate.transform.parent == null && !EditorUtility.IsPersistent(candidate))
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
                return;
            }
            if (_context != null && _context.SelectedTargetIndex != TargetIndex)
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
                instance.SetActive(false);
                _previews.Add(new PreviewInstance
                {
                    EventIndex = pair.Key,
                    TargetIndex = pair.Value,
                    Prefab = prefab,
                    Instance = instance,
                    Particles = particles
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
