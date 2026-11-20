using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.Editor
{
    public sealed class BattleTutorialSpotlightPreview : EditorWindow
    {
        private static readonly string[] StageNames = { "1. 테스트_각인", "2. 투자", "3. 스킬 사용", "적 대상 지정 링" };
        private const int EnemyReticleStage = 3;
        private const float EnemyPreviewSeconds = 1.3f;
        private static readonly string[] RegionFields =
            { "_spotlightRegion", "_investmentSpotlightRegion", "_executeSpotlightRegion" };
        private static readonly string[] StyleFields =
            { "_skillSpotlightStyle", "_investmentSpotlightStyle", "_executeSpotlightStyle" };

        [SerializeField] private BattleTutorialDirector _director;
        [SerializeField] private int _stage;
        private TutorialSpotlightGraphic _preview;
        private TutorialEnemyReticle _reticlePreview;
        private bool _show;
        private float _time;
        private double _startedAt = -1d;
        private double _nextRefresh;

        [MenuItem("CK/Tutorial Spotlight Preview")]
        private static void Open()
        {
            var window = GetWindow<BattleTutorialSpotlightPreview>("Tutorial Spotlight");
            if (Selection.activeGameObject != null
                && Selection.activeGameObject.TryGetComponent(out BattleTutorialDirector selected))
            {
                window._director = selected;
            }
            else if (window._director == null)
            {
                window._director = FindFirstObjectByType<BattleTutorialDirector>();
            }
        }

        private void OnEnable()
        {
            EditorApplication.update += RefreshPreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshPreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            StopPreview();
        }

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var director = (BattleTutorialDirector)EditorGUILayout.ObjectField(
                    "Battle Tutorial", _director, typeof(BattleTutorialDirector), true);
                if (director != _director)
                {
                    StopPreview();
                    _director = director;
                }
                if (_director == null || EditorUtility.IsPersistent(_director))
                {
                    EditorGUILayout.HelpBox("씬의 Battle Tutorial 컴포넌트를 지정하세요.", MessageType.Info);
                    return;
                }

                _stage = EditorGUILayout.Popup("강조 단계", _stage, StageNames);
                using var settings = new SerializedObject(_director);
                if (_stage == EnemyReticleStage)
                {
                    EditorGUILayout.PropertyField(settings.FindProperty("_enemyReticleColor"), new GUIContent("링 색상"));
                    EditorGUILayout.PropertyField(settings.FindProperty("_enemyReticleGlow"), new GUIContent("글로우 강도"));
                    EditorGUILayout.PropertyField(settings.FindProperty("_enemyReticleSize"), new GUIContent("지름"));
                    EditorGUILayout.PropertyField(settings.FindProperty("_enemyReticleHeight"), new GUIContent("적 기준 높이"));
                    settings.ApplyModifiedProperties();
                    EditorGUILayout.HelpBox("편집 모드에서는 캔버스 중앙에 표시합니다.", MessageType.Info);
                    DrawPlayback(EnemyPreviewSeconds);
                    return;
                }
                SerializedProperty region = settings.FindProperty(RegionFields[_stage]);
                EditorGUILayout.PropertyField(region, new GUIContent("강조 영역"));
                EditorGUILayout.PropertyField(settings.FindProperty(StyleFields[_stage]), new GUIContent("발광 스타일"), true);
                EditorGUILayout.PropertyField(settings.FindProperty("_spotlightPadding"), new GUIContent("공통 여백"));
                EditorGUILayout.PropertyField(settings.FindProperty("_spotlightOffset"), new GUIContent("공통 위치 보정"));
                EditorGUILayout.PropertyField(settings.FindProperty("_weaknessDimAlpha"), new GUIContent("배경 어둡기"));
                SerializedProperty dimTime = settings.FindProperty("_spotlightDimInSeconds");
                SerializedProperty ringTime = settings.FindProperty("_spotlightRingInSeconds");
                EditorGUILayout.PropertyField(dimTime, new GUIContent("명암 전환 시간"));
                EditorGUILayout.PropertyField(ringTime, new GUIContent("링 등장 시간"));
                float duration = Mathf.Max(0.01f, dimTime.floatValue, ringTime.floatValue);
                settings.ApplyModifiedProperties();

                if (!(region.objectReferenceValue is RectTransform targetRegion))
                {
                    EditorGUILayout.HelpBox("미리 볼 RectTransform을 지정하세요.", MessageType.Info);
                    return;
                }
                if (!targetRegion.gameObject.activeInHierarchy)
                {
                    EditorGUILayout.HelpBox("현재 비활성인 HUD 영역입니다. 미리보기는 해당 위치에 명암과 링을 표시합니다.", MessageType.Info);
                }
                if (GUILayout.Button("Hierarchy에서 강조 영역 선택"))
                {
                    Selection.activeObject = targetRegion.gameObject;
                    EditorGUIUtility.PingObject(targetRegion);
                }
                DrawPlayback(duration);
            }
        }

        private void DrawPlayback(float duration)
        {
            if (GUILayout.Button(_show ? "미리보기 끄기" : "미리보기 켜기"))
            {
                if (_show)
                {
                    StopPreview();
                }
                else
                {
                    _show = true;
                    _time = duration;
                }
            }
            using (new EditorGUI.DisabledScope(!_show))
            {
                if (GUILayout.Button("등장 애니메이션 재생"))
                {
                    _startedAt = EditorApplication.timeSinceStartup;
                    _time = 0f;
                }
                EditorGUI.BeginChangeCheck();
                float time = EditorGUILayout.Slider("미리보기 시간", _time, 0f, duration);
                if (EditorGUI.EndChangeCheck())
                {
                    _startedAt = -1d;
                    _time = time;
                }
            }
        }

        private void RefreshPreview()
        {
            double now = EditorApplication.timeSinceStartup;
            if (!_show || now < _nextRefresh)
            {
                return;
            }
            _nextRefresh = now + 1d / 30d;
            if (_director == null || !_director.gameObject.activeInHierarchy)
            {
                StopPreview();
                return;
            }
            using var settings = new SerializedObject(_director);
            if (_stage == EnemyReticleStage)
            {
                DestroyPreviewGraphic();
                if (_reticlePreview == null)
                {
                    _reticlePreview = CreatePreviewGraphic<TutorialEnemyReticle>();
                }
                UpdatePlayback(now, EnemyPreviewSeconds);
                var canvas = (RectTransform)_director.transform;
                _director.RenderEnemyReticle(_reticlePreview, canvas.rect.center, _time);
                RefreshViews();
                return;
            }
            DestroyReticlePreview();
            var region = settings.FindProperty(RegionFields[_stage]).objectReferenceValue as RectTransform;
            if (region == null)
            {
                DestroyPreviewGraphic();
                return;
            }
            if (_preview == null)
            {
                _preview = CreatePreviewGraphic<TutorialSpotlightGraphic>();
            }
            float duration = Mathf.Max(settings.FindProperty("_spotlightDimInSeconds").floatValue,
                settings.FindProperty("_spotlightRingInSeconds").floatValue);
            UpdatePlayback(now, duration);
            _director.RenderSpotlightPreview(_preview, region, _stage, _time);
            RefreshViews();
        }

        private T CreatePreviewGraphic<T>() where T : UnityEngine.UI.Graphic
        {
            var overlay = new GameObject("Tutorial Spotlight (Editor Preview)", typeof(T))
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = _director.gameObject.layer,
            };
            T graphic = overlay.GetComponent<T>();
            graphic.raycastTarget = false;
            overlay.transform.SetParent(_director.transform, false);
            overlay.transform.SetAsFirstSibling();
            return graphic;
        }

        private void UpdatePlayback(double now, float duration)
        {
            if (_startedAt >= 0d)
            {
                _time = Mathf.Min((float)(now - _startedAt), duration);
                if (_time >= duration)
                {
                    _startedAt = -1d;
                }
                Repaint();
            }
        }

        private static void RefreshViews()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                StopPreview();
            }
        }

        private void StopPreview()
        {
            _show = false;
            _startedAt = -1d;
            DestroyPreviewGraphic();
            DestroyReticlePreview();
        }

        private void DestroyPreviewGraphic()
        {
            if (_preview != null)
            {
                DestroyImmediate(_preview.gameObject);
                _preview = null;
                RefreshViews();
            }
        }

        private void DestroyReticlePreview()
        {
            if (_reticlePreview != null)
            {
                DestroyImmediate(_reticlePreview.gameObject);
                _reticlePreview = null;
                RefreshViews();
            }
        }
    }
}
