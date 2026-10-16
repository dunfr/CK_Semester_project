using System.Collections.Generic;
using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CK.SemesterProject.EditorTools
{
    [CustomEditor(typeof(BattleVfxTestController))]
    public sealed class BattleVfxTestControllerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Button(BattleVfxPlacementWindow.Open) { text = "이펙트 배치 · 미리보기 · 저장" });
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }
    }

    public sealed class BattleVfxPlacementWindow : EditorWindow
    {
        private const string SettingsPath = "Assets/CK_Semester_Project/Prototype/Data/BattleVfxTest/BattleVfxTestEnhancement.asset";
        private BasicSkillEnhancementAsset _settings;
        private Transform _actor;
        private Transform _target;
        [SerializeField] private string _skillId;
        [SerializeField] private int _stage = 1;
        [SerializeField] private bool _charge;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private SkillVfxPlacement _placement;
        [SerializeField] private AnimationClip _animation;
        [SerializeField] private float _actionSpeed = 1f;
        [SerializeField] private float _impactFraction = .35f;
        [SerializeField] private float _previewSpeed = 1f;
        [SerializeField] private float _previewSeconds;
        [SerializeField] private bool _loop = true;
        private GameObject _preview;
        private ParticleSystem[] _particles;
        private float[] _particleTimes;
        private TutorialSlashVfxPlacement _slashPreview;
        private SakuraSlashFade[] _slashFades;
        private SakuraImpactStreakPlayer[] _impactStreaks;
        private Renderer[] _previewRenderers;
        private bool[] _rendererEnabled;
        private float _loopSeconds = 1f;
        private bool _previewManual;
        private Label _previewStatus;
        private BattleVfxTestController _pendingBattle;
        private double _battleDeadline;
        private bool _playing;
        private float _time;
        private double _lastTime;
        [SerializeField] private bool _dirty;
        private Label _status;
        private VisualElement _fields;
        private Vector3Field _positionField;
        private Vector3Field _rotationField;
        private Toggle _enabledField;
        private Slider _timeline;
        private readonly List<string> _skillIds = new List<string>();

        [MenuItem("Tools/CK/Battle VFX Placement")]
        public static void Open()
        {
            GetWindow<BattleVfxPlacementWindow>("이펙트 배치");
        }

        private void OnEnable()
        {
            minSize = new Vector2(440f, 600f);
            SceneView.duringSceneGui += DrawHandles;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode += SceneChanged;
            Undo.undoRedoPerformed += ReloadAfterUndo;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawHandles;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= SceneChanged;
            Undo.undoRedoPerformed -= ReloadAfterUndo;
            ClearPreview();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var scroll = new ScrollView();
            scroll.style.paddingLeft = 10;
            scroll.style.paddingRight = 10;
            rootVisualElement.Add(scroll);
            scroll.Add(new HelpBox("스킬·단계·차징/타격을 선택하면 이펙트를 미리 봅니다.\n위치·회전은 수동 배치 모드에서 W/E로 조절합니다.\n차징 설정은 전 단계 공통, 타격·공격 동작 설정은 단계별입니다.", HelpBoxMessageType.Info));
            scroll.Add(new Button(() =>
            {
                if (EditorApplication.isPlaying || !ConfirmDiscard())
                {
                    return;
                }
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    ClearPreview();
                    EditorSceneManager.OpenScene(BattleVfxTestIntegration.ScenePath);
                    CreateGUI();
                }
            }) { text = "테스트 씬 열기" });
            _settings = AssetDatabase.LoadAssetAtPath<BasicSkillEnhancementAsset>(SettingsPath);
            if (_settings == null)
            {
                scroll.Add(new HelpBox("테스트 설정을 찾을 수 없습니다. BattleVfxTest 데이터 폴더의 BattleVfxTestEnhancement.asset을 확인하세요.", HelpBoxMessageType.Warning));
                return;
            }
            _actor = GameObject.Find("Fuchsia Player")?.transform;
            _target = GameObject.Find("VFX Training Dummy")?.transform;
            var actorField = new ObjectField("시전자 기준점") { objectType = typeof(Transform), allowSceneObjects = true, value = _actor };
            actorField.RegisterValueChangedCallback(evt => { ClearPreview(); _actor = evt.newValue as Transform; });
            scroll.Add(actorField);
            var targetField = new ObjectField("대상 기준점") { objectType = typeof(Transform), allowSceneObjects = true, value = _target };
            targetField.RegisterValueChangedCallback(evt => { ClearPreview(); _target = evt.newValue as Transform; });
            scroll.Add(targetField);
            _skillIds.Clear();
            var serialized = new SerializedObject(_settings);
            SerializedProperty skills = serialized.FindProperty("_skills");
            for (int i = 0; i < skills.arraySize; i++)
            {
                _skillIds.Add(skills.GetArrayElementAtIndex(i).FindPropertyRelative("_skillId").stringValue);
            }
            if (_skillIds.Count == 0)
            {
                scroll.Add(new HelpBox("스킬 설정이 비어 있습니다.", HelpBoxMessageType.Warning));
                return;
            }
            int selected = Mathf.Max(0, _skillIds.IndexOf(_skillId));
            _skillId = _skillIds[selected];
            var skillField = new PopupField<string>("스킬 ID", _skillIds, selected);
            skillField.RegisterValueChangedCallback(evt =>
            {
                if (!ConfirmDiscard()) { skillField.SetValueWithoutNotify(evt.previousValue); return; }
                _skillId = evt.newValue;
                LoadSlot();
            });
            scroll.Add(skillField);
            var stageField = new PopupField<string>("강화 단계", new List<string> { "1", "2", "3" }, _stage - 1);
            stageField.RegisterValueChangedCallback(evt =>
            {
                if (!ConfirmDiscard()) { stageField.SetValueWithoutNotify(evt.previousValue); return; }
                _stage = int.Parse(evt.newValue);
                LoadSlot();
            });
            scroll.Add(stageField);
            var typeField = new Toggle("차징 편집 (끄면 타격)") { value = _charge };
            typeField.RegisterValueChangedCallback(evt =>
            {
                if (!ConfirmDiscard()) { typeField.SetValueWithoutNotify(evt.previousValue); return; }
                _charge = evt.newValue;
                LoadSlot();
            });
            scroll.Add(typeField);
            _fields = new VisualElement();
            scroll.Add(_fields);
            scroll.Add(new Button(BuildPreview) { text = "미리보기 생성 / 다시 재생" });
            scroll.Add(new Button(() => { if (_preview == null) { BuildPreview(); } else { _playing = !_playing; } }) { text = "재생 / 일시정지" });
            scroll.Add(new Button(() => { _playing = false; Seek(_time + 1f / 30f); }) { text = "1프레임 앞으로 (1/30초)" });
            var previewSpeed = new FloatField("미리보기 배속 (저장 안 함)") { value = _previewSpeed, isDelayed = true };
            previewSpeed.RegisterValueChangedCallback(evt => { _previewSpeed = Mathf.Clamp(evt.newValue, .05f, 4f); previewSpeed.SetValueWithoutNotify(_previewSpeed); });
            scroll.Add(previewSpeed);
            var loopField = new Toggle("반복 재생") { value = _loop };
            loopField.RegisterValueChangedCallback(evt => _loop = evt.newValue);
            scroll.Add(loopField);
            var durationField = new FloatField("미리보기 구간 (초, 0=자동)") { value = _previewSeconds, isDelayed = true };
            durationField.RegisterValueChangedCallback(evt => { _previewSeconds = Mathf.Clamp(evt.newValue, 0f, 60f); durationField.SetValueWithoutNotify(_previewSeconds); RefreshDuration(); });
            scroll.Add(durationField);
            _timeline = new Slider("미리보기 시간 (초)", 0f, _loopSeconds) { showInputField = true };
            _timeline.RegisterValueChangedCallback(evt => { _playing = false; _time = evt.newValue; SampleParticles(true); });
            scroll.Add(_timeline);
            scroll.Add(new Button(AlignToGameCamera) { text = "Scene 뷰를 게임 카메라 시점으로" });
            _previewStatus = new Label();
            _previewStatus.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(_previewStatus);
            scroll.Add(new Button(SavePlacement) { text = "배치·재생·공격 설정 저장 (Play 중에도 유지)" });
            scroll.Add(new Button(TestSelectedSkill) { text = "선택 스킬 실제 전투 재생 (Play 중)" });
            scroll.Add(new Button(() => { if (ConfirmDiscard()) { LoadSlot(); } }) { text = "저장한 배치 다시 불러오기" });
            scroll.Add(new Button(ClearPreview) { text = "미리보기 제거" });
            _status = new Label();
            _status.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(_status);
            if (_dirty)
            {
                BuildFields();
                Changed(false);
            }
            else
            {
                LoadSlot();
            }
        }

        private bool ConfirmDiscard()
        {
            if (_dirty && !EditorUtility.DisplayDialog("저장하지 않은 배치", "현재 배치를 버리고 이동할까요?", "버리기", "계속 편집"))
            {
                return false;
            }
            _dirty = false;
            hasUnsavedChanges = false;
            return true;
        }

        private void LoadSlot()
        {
            ClearPreview();
            _placement = _settings.GetPlacement(_skillId, _stage, _charge);
            if (_placement.Scale <= 0f)
            {
                _placement.Anchor = _charge ? SkillVfxAnchor.Actor : SkillVfxAnchor.Target;
                _placement.Scale = 1f;
                _placement.Follow = _charge;
            }
            _prefab = _charge ? _settings.GetChargeEffect(_skillId) : _settings.GetEffect(_skillId, _stage);
            _animation = _settings.GetAnimation(_skillId, _stage);
            _actionSpeed = _settings.GetActionSpeed(_skillId, _stage);
            _impactFraction = _settings.GetImpactFraction(_skillId, _stage);
            _dirty = false;
            hasUnsavedChanges = false;
            BuildFields();
            _status.text = "편집 대상: " + _skillId + " / " + _stage + "단계 / " + (_charge ? "차징 (전 단계 공통)" : "타격")
                + "\n수동 배치를 저장하면 기존 자동 카메라 보정·좌우 무작위 회전 대신 프리팹의 자식 배치를 사용합니다.";
            if (_prefab != null && _actor != null && _target != null)
            {
                BuildPreview();
            }
            else
            {
                _previewStatus.text = _prefab == null ? "이 슬롯에는 이펙트 프리팹이 없습니다. 위에서 프리팹을 연결하세요."
                    : "테스트 씬을 열고 기준점을 지정하세요.";
            }
        }

        private void BuildFields()
        {
            _fields.Clear();
            var prefabField = new ObjectField("이펙트 프리팹") { objectType = typeof(GameObject), allowSceneObjects = false, value = _prefab };
            prefabField.RegisterValueChangedCallback(evt => { _prefab = evt.newValue as GameObject; Changed(false); BuildPreview(); });
            _fields.Add(prefabField);
            _enabledField = new Toggle("수동 배치 사용") { value = _placement.Enabled };
            _enabledField.RegisterValueChangedCallback(evt => { _placement.Enabled = evt.newValue; Changed(false); BuildPreview(); });
            _fields.Add(_enabledField);
            var anchorField = new PopupField<string>("배치 기준", new List<string> { "시전자", "대상" }, (int)_placement.Anchor);
            anchorField.RegisterValueChangedCallback(evt => { _placement.Anchor = (SkillVfxAnchor)anchorField.index; Changed(); UpdatePose(); });
            _fields.Add(anchorField);
            _positionField = new Vector3Field("상대 위치 (m)") { value = _placement.Position };
            _positionField.RegisterValueChangedCallback(evt => { _placement.Position = evt.newValue; Changed(); UpdatePose(); });
            _fields.Add(_positionField);
            _rotationField = new Vector3Field("상대 회전 (도)") { value = _placement.Rotation };
            _rotationField.RegisterValueChangedCallback(evt => { _placement.Rotation = evt.newValue; Changed(); UpdatePose(); });
            _fields.Add(_rotationField);
            var scaleField = new FloatField("크기 배율") { value = _placement.Scale, isDelayed = true };
            scaleField.RegisterValueChangedCallback(evt => { _placement.Scale = Mathf.Max(.01f, evt.newValue); scaleField.SetValueWithoutNotify(_placement.Scale); Changed(); UpdatePose(); });
            _fields.Add(scaleField);
            var followField = new Toggle("생성 후 기준점 따라가기") { value = _placement.Follow };
            followField.RegisterValueChangedCallback(evt => { _placement.Follow = evt.newValue; Changed(); });
            _fields.Add(followField);
            var speedField = new FloatField("이펙트 재생 속도 (배)") { value = _placement.EffectivePlaybackSpeed, isDelayed = true };
            speedField.RegisterValueChangedCallback(evt => { _placement.PlaybackSpeed = Mathf.Clamp(evt.newValue, .05f, 4f); speedField.SetValueWithoutNotify(_placement.PlaybackSpeed); Changed(false); BuildPreview(); });
            _fields.Add(speedField);
            var delayField = new FloatField("이펙트 시작 지연 (초)") { value = _placement.StartDelay, isDelayed = true };
            delayField.RegisterValueChangedCallback(evt => { _placement.StartDelay = Mathf.Clamp(evt.newValue, 0f, 10f); delayField.SetValueWithoutNotify(_placement.StartDelay); Changed(false); RefreshDuration(); SampleParticles(true); });
            _fields.Add(delayField);
            var lifetimeField = new FloatField("이펙트 표시 시간 (초, 0=자동)") { value = _placement.Lifetime, isDelayed = true };
            lifetimeField.RegisterValueChangedCallback(evt => { _placement.Lifetime = Mathf.Clamp(evt.newValue, 0f, 30f); lifetimeField.SetValueWithoutNotify(_placement.Lifetime); Changed(false); RefreshDuration(); SampleParticles(true); });
            _fields.Add(lifetimeField);
            _fields.Add(new HelpBox("차징 재생 속도는 기존 공통 속도에 곱해집니다.\n공격 동작은 실제 전투 재생으로 확인하세요.", HelpBoxMessageType.Info));
            var clipField = new ObjectField("공격 애니메이션 클립") { objectType = typeof(AnimationClip), allowSceneObjects = false, value = _animation };
            clipField.RegisterValueChangedCallback(evt => { _animation = evt.newValue as AnimationClip; Changed(false); });
            _fields.Add(clipField);
            var actionSpeedField = new FloatField("공격 동작 속도 (배)") { value = _actionSpeed, isDelayed = true };
            actionSpeedField.RegisterValueChangedCallback(evt => { _actionSpeed = Mathf.Clamp(evt.newValue, .05f, 4f); actionSpeedField.SetValueWithoutNotify(_actionSpeed); Changed(false); });
            _fields.Add(actionSpeedField);
            var impactField = new Slider("타격 시점 (공격 진행률)", .05f, .9f) { value = _impactFraction, showInputField = true };
            impactField.RegisterValueChangedCallback(evt => { _impactFraction = evt.newValue; Changed(false); });
            _fields.Add(impactField);
        }

        private void Changed(bool enableManual = true)
        {
            if (enableManual)
            {
                _placement.Enabled = true;
                _enabledField?.SetValueWithoutNotify(true);
            }
            _dirty = true;
            hasUnsavedChanges = true;
            _status.text = "저장되지 않은 변경이 있습니다. 실제 전투에 반영하려면 배치 저장을 누르세요.";
        }

        private void BuildPreview()
        {
            ClearPreview();
            if (_prefab == null || _actor == null || _target == null)
            {
                _status.text = "프리팹·시전자·대상 기준점을 먼저 지정하세요.";
                return;
            }
            _preview = Instantiate(_prefab);
            _preview.name = "VFX Placement Preview";
            _preview.SetActive(true);
            foreach (Transform item in _preview.GetComponentsInChildren<Transform>(true))
            {
                item.gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }
            _previewManual = _placement.Enabled;
            Camera camera = Camera.main != null ? Camera.main : SceneView.lastActiveSceneView?.camera;
            if (_placement.Enabled)
            {
                SkillVfxPlacement previewPlacement = _placement;
                previewPlacement.Follow = false;
                previewPlacement.Apply(_preview, _actor, _target);
            }
            else if (_charge)
            {
                _preview.transform.SetPositionAndRotation(_actor.position, _actor.rotation);
                TutorialChargeVfxPlacement charge = _preview.GetComponent<TutorialChargeVfxPlacement>();
                if (charge != null)
                {
                    charge.Initialize(camera);
                    charge.enabled = false;
                }
            }
            else
            {
                _preview.transform.SetPositionAndRotation(_target.position, _target.rotation);
                _slashPreview = _preview.GetComponent<TutorialSlashVfxPlacement>();
                if (_slashPreview != null)
                {
                    _slashPreview.Initialize(_actor, _target, camera);
                    _slashPreview.enabled = false;
                }
            }
            foreach (Collider collider in _preview.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            // Play 모드에서도 런타임 Update와 에디터 시간 샘플링이 충돌하지 않게 한다.
            _slashFades = _preview.GetComponentsInChildren<SakuraSlashFade>(true);
            foreach (SakuraSlashFade fade in _slashFades)
            {
                fade.enabled = false;
            }
            _impactStreaks = _preview.GetComponentsInChildren<SakuraImpactStreakPlayer>(true);
            foreach (SakuraImpactStreakPlayer streak in _impactStreaks)
            {
                streak.enabled = false;
            }
            _previewRenderers = _preview.GetComponentsInChildren<Renderer>(true);
            _rendererEnabled = new bool[_previewRenderers.Length];
            for (int i = 0; i < _previewRenderers.Length; i++)
            {
                _rendererEnabled[i] = _previewRenderers[i].enabled;
            }
            _particles = _preview.GetComponentsInChildren<ParticleSystem>(true);
            _particleTimes = new float[_particles.Length];
            for (int i = 0; i < _particleTimes.Length; i++)
            {
                _particleTimes[i] = -1f;
            }
            foreach (ParticleSystem particle in _particles)
            {
                var main = particle.main;
                main.stopAction = ParticleSystemStopAction.None;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.simulationSpeed *= _placement.EffectivePlaybackSpeed;
                if (_charge)
                {
                    main.simulationSpeed *= _settings.ChargePlaybackSpeed;
                }
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.useAutoRandomSeed = false;
                particle.randomSeed = 12345u;
            }
            RefreshDuration();
            _time = Mathf.Min(_placement.StartDelay + .08f / _placement.EffectivePlaybackSpeed, _loopSeconds);
            SampleParticles();
            _playing = true;
            _lastTime = EditorApplication.timeSinceStartup;
            Selection.activeObject = null;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.Frame(new Bounds(_preview.transform.position, Vector3.one * 5f), false);
            }
            SceneView.RepaintAll();
        }

        private void UpdatePose()
        {
            if (_preview == null || _actor == null || _target == null)
            {
                return;
            }
            if (!_previewManual && _placement.Enabled)
            {
                BuildPreview();
                return;
            }
            Transform anchor = _placement.GetAnchor(_actor, _target);
            if (!_placement.Enabled)
            {
                return;
            }
            _preview.transform.SetPositionAndRotation(anchor.position + anchor.rotation * _placement.Position,
                anchor.rotation * Quaternion.Euler(_placement.Rotation));
            _preview.transform.localScale = _prefab.transform.localScale * _placement.Scale;
            SampleParticles(true);
        }

        private void DrawHandles(SceneView view)
        {
            if (_preview == null || _actor == null || _target == null)
            {
                return;
            }
            if (!_placement.Enabled)
            {
                Handles.Label(_preview.transform.position, "자동 배치 미리보기 · 위치를 바꾸려면 수동 배치를 켜세요");
                return;
            }
            Transform anchor = _placement.GetAnchor(_actor, _target);
            Handles.Label(_preview.transform.position, "이펙트 배치 · W 이동 / E 회전");
            EditorGUI.BeginChangeCheck();
            Vector3 position = _preview.transform.position;
            Quaternion rotation = _preview.transform.rotation;
            if (Tools.current == Tool.Rotate)
            {
                rotation = Handles.RotationHandle(rotation, position);
            }
            else
            {
                position = Handles.PositionHandle(position, Tools.pivotRotation == PivotRotation.Local ? rotation : Quaternion.identity);
            }
            if (EditorGUI.EndChangeCheck())
            {
                _placement.Position = Quaternion.Inverse(anchor.rotation) * (position - anchor.position);
                _placement.Rotation = (Quaternion.Inverse(anchor.rotation) * rotation).eulerAngles;
                _positionField.SetValueWithoutNotify(_placement.Position);
                _rotationField.SetValueWithoutNotify(_placement.Rotation);
                Changed();
                UpdatePose();
            }
        }

        private void SavePlacement()
        {
            if (_settings == null || !EditorUtility.IsPersistent(_settings))
            {
                return;
            }
            Undo.RecordObject(_settings, "이펙트 배치 저장");
            _settings.SetPlacement(_skillId, _stage, _charge, _prefab, _placement);
            _settings.SetActionSettings(_skillId, _stage, _animation, _actionSpeed, _impactFraction);
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssetIfDirty(_settings);
            _dirty = false;
            hasUnsavedChanges = false;
            _status.text = "저장 완료. 다음 스킬 사용부터 반영됩니다.\n" + SettingsPath;
        }

        private void RefreshDuration()
        {
            float duration = _preview != null ? SkillVfxPlayback.EstimateDuration(_preview) : 1f;
            float speed = PreviewEffectSpeed;
            if (_slashFades != null)
            {
                foreach (SakuraSlashFade fade in _slashFades)
                {
                    duration = Mathf.Max(duration, fade.Duration / speed);
                }
            }
            if (_impactStreaks != null)
            {
                foreach (SakuraImpactStreakPlayer streak in _impactStreaks)
                {
                    float offset = _slashPreview != null
                        ? -_slashPreview.GetPreviewEffectTime(streak.transform, 0f, speed) : 0f;
                    duration = Mathf.Max(duration, offset + streak.Duration / speed);
                }
            }
            if (_placement.Lifetime > 0f)
            {
                duration = _placement.Lifetime;
            }
            _loopSeconds = _previewSeconds > 0f ? _previewSeconds
                : Mathf.Clamp(Mathf.Max(0f, _placement.StartDelay) + duration + .15f, .2f, 60f);
            if (_timeline != null)
            {
                _timeline.highValue = _loopSeconds;
                _time = Mathf.Min(_time, _loopSeconds);
                _timeline.SetValueWithoutNotify(_time);
            }
        }

        private void Seek(float time)
        {
            _time = Mathf.Clamp(time, 0f, _loopSeconds);
            _timeline?.SetValueWithoutNotify(_time);
            SampleParticles(true);
        }

        private void AlignToGameCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                _status.text = "Main Camera를 찾을 수 없습니다. 테스트 씬을 먼저 여세요.";
                return;
            }
            SceneView view = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView : GetWindow<SceneView>();
            view.AlignViewToObject(camera.transform);
            view.Focus();
            SceneView.RepaintAll();
        }

        private void TestSelectedSkill()
        {
            if (!EditorApplication.isPlaying || _dirty)
            {
                _status.text = _dirty ? "설정을 먼저 저장한 뒤 실제 전투 재생을 누르세요." : "Play 모드에서 실제 전투 재생을 사용할 수 있습니다.";
                return;
            }
            BattleVfxTestController lab = Object.FindFirstObjectByType<BattleVfxTestController>();
            if (lab == null)
            {
                _status.text = "이펙트 테스트 씬에서 실행하세요.";
                return;
            }
            ClearPreview();
            for (int i = 0; i < 3 && lab.Battle.TestSkillStage != _stage; i++)
            {
                lab.NextStage();
            }
            lab.RestartBattle();
            _pendingBattle = lab;
            _battleDeadline = EditorApplication.timeSinceStartup + 10f;
            _status.text = "전투 초기화 후 선택 스킬을 재생합니다.";
        }

        private void SampleParticles(bool restart = false)
        {
            if (_particles == null)
            {
                return;
            }
            float elapsed = _time - Mathf.Max(0f, _placement.StartDelay);
            float speed = PreviewEffectSpeed;
            if (_slashPreview != null)
            {
                _slashPreview.SamplePreview(elapsed * speed);
            }
            int count = 0;
            bool hidden = elapsed < 0f || (_placement.Lifetime > 0f && elapsed >= _placement.Lifetime);
            for (int i = 0; i < _previewRenderers.Length; i++)
            {
                _previewRenderers[i].enabled = !hidden && _rendererEnabled[i];
            }
            foreach (SakuraSlashFade fade in _slashFades)
            {
                fade.Preview(hidden ? 1f : elapsed * speed / fade.Duration);
            }
            foreach (SakuraImpactStreakPlayer streak in _impactStreaks)
            {
                float streakTime = _slashPreview != null
                    ? _slashPreview.GetPreviewEffectTime(streak.transform, elapsed, speed) : elapsed;
                streak.Preview(hidden || streakTime < 0f ? 1f : streakTime * speed / streak.Duration);
            }
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem particle = _particles[i];
                if (particle == null)
                {
                    continue;
                }
                if (hidden || !particle.gameObject.activeInHierarchy)
                {
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _particleTimes[i] = -1f;
                    continue;
                }
                float particleTime = _slashPreview != null
                    ? _slashPreview.GetPreviewParticleTime(particle, elapsed, speed) : elapsed;
                bool reset = restart || _particleTimes[i] < 0f || particleTime < _particleTimes[i];
                // 짧은 에디터 갱신 간격이 고정 스텝에서 버려지지 않도록 직접 경과 시간을 샘플링한다.
                particle.Simulate(reset ? particleTime : particleTime - _particleTimes[i], false, reset, false);
                particle.Pause(false);
                _particleTimes[i] = particleTime;
                count += particle.particleCount;
            }
            _previewStatus.text = "시간 " + _time.ToString("0.00") + " / " + _loopSeconds.ToString("0.00")
                + "초 · 파티클 " + count + "개 · " + (_placement.Enabled ? "수동 배치" : "기존 자동 배치")
                + (hidden ? "\n시작 지연 또는 표시 시간 밖입니다." : count == 0 ? "\n현재 시점에 파티클이 없습니다. 시간 슬라이더와 프리팹을 확인하세요." : "");
            SceneView.RepaintAll();
        }

        private float PreviewEffectSpeed => _placement.EffectivePlaybackSpeed
            * (_charge ? _settings.ChargePlaybackSpeed : 1f);

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastTime);
            _lastTime = now;
            if (_pendingBattle != null)
            {
                if (!EditorApplication.isPlaying || now > _battleDeadline)
                {
                    _pendingBattle = null;
                    _status.text = "전투 준비가 완료되지 않았습니다. Play 상태와 Console을 확인하세요.";
                }
                else if (_pendingBattle.Battle.CanChooseAction && _pendingBattle.Battle.HasSelectedBattleTarget)
                {
                    _pendingBattle.Battle.UseSkill(_skillIds.IndexOf(_skillId));
                    _pendingBattle = null;
                    _status.text = "선택 스킬을 재생했습니다.";
                }
            }
            if (_preview != null && _playing)
            {
                bool restart = false;
                _time += Mathf.Min(delta, .1f) * _previewSpeed;
                if (_time >= _loopSeconds)
                {
                    if (_loop)
                    {
                        _time = Mathf.Repeat(_time, _loopSeconds);
                        restart = true;
                    }
                    else
                    {
                        _time = _loopSeconds;
                        _playing = false;
                    }
                }
                _timeline?.SetValueWithoutNotify(_time);
                SampleParticles(restart);
            }
        }

        private void ClearPreview()
        {
            _playing = false;
            _pendingBattle = null;
            _particles = null;
            _particleTimes = null;
            _slashPreview = null;
            _slashFades = null;
            _impactStreaks = null;
            _previewRenderers = null;
            _rendererEnabled = null;
            if (_preview != null)
            {
                DestroyImmediate(_preview);
            }
            _preview = null;
        }

        private void PlayModeChanged(PlayModeStateChange state)
        {
            ClearPreview();
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                CreateGUI();
            }
        }

        private void SceneChanged(Scene previous, Scene next)
        {
            ClearPreview();
            CreateGUI();
        }

        private void ReloadAfterUndo()
        {
            if (_settings != null && _fields != null)
            {
                LoadSlot();
            }
        }

        public override void SaveChanges()
        {
            SavePlacement();
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            _dirty = false;
            base.DiscardChanges();
        }
    }
}
