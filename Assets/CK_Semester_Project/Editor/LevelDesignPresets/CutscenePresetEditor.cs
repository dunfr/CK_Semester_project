using CK.SemesterProject.LevelDesign;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign.EditorTools
{
    /// <summary>컷씬 프리셋 인스펙터. 단계 목록을 끌어서 순서를 바꾸고, 버튼으로 단계·카메라 샷·도착 지점을 만든다.</summary>
    [CustomEditor(typeof(CutscenePreset))]
    public sealed class CutscenePresetEditor : UnityEditor.Editor
    {
        private ReorderableList _list;

        private void OnEnable()
        {
            SerializedProperty steps = serializedObject.FindProperty("_steps");
            _list = new ReorderableList(serializedObject, steps, true, true, false, true);
            _list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "단계 (위에서 아래 순서로 재생 · 끌어서 순서 변경)");
            _list.elementHeightCallback = index =>
                EditorGUI.GetPropertyHeight(steps.GetArrayElementAtIndex(index), true) + EditorGUIUtility.singleLineHeight + 10f;
            _list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(index);
                Rect title = new Rect(rect.x, rect.y + 2f, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(title, "#" + (index + 1) + "  " + CutsceneStepDrawer.Summarize(step), EditorStyles.boldLabel);
                Rect body = new Rect(rect.x, title.yMax + 4f, rect.width, rect.height - title.height - 8f);
                EditorGUI.PropertyField(body, step, GUIContent.none, true);
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_trigger"), new GUIContent("트리거 영역"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_playOnce"), new GUIContent("1회만 재생"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_lockPlayer"), new GUIContent("플레이어 조작 잠금"));
            EditorGUILayout.Space(6f);

            _list.DoLayoutList();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 대사"))
            {
                AddStep(CutsceneStepType.Dialogue);
            }
            if (GUILayout.Button("+ 이동"))
            {
                AddStep(CutsceneStepType.Move);
            }
            if (GUILayout.Button("+ 카메라"))
            {
                AddStep(CutsceneStepType.Camera);
            }
            if (GUILayout.Button("+ 대기"))
            {
                AddStep(CutsceneStepType.Wait);
            }
            EditorGUILayout.EndHorizontal();
            DrawSelectedStepTools();

            EditorGUILayout.Space(6f);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_typingSpeed"), new GUIContent("대사 글자 속도(글자/초)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_dialogueFont"), new GUIContent("대사창 글꼴"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_walkState"), new GUIContent("걷기 Animator 상태"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_idleState"), new GUIContent("대기 Animator 상태"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_cameraFieldOfView"), new GUIContent("카메라 시야각"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_started"), new GUIContent("시작 시 이벤트"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_finished"), new GUIContent("종료 시 이벤트"));
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSelectedStepTools()
        {
            SerializedProperty steps = serializedObject.FindProperty("_steps");
            if (_list.index < 0 || _list.index >= steps.arraySize)
            {
                EditorGUILayout.HelpBox("단계를 선택하면 카메라 샷·도착 지점을 만드는 버튼이 나타납니다.", MessageType.None);
                return;
            }
            SerializedProperty step = steps.GetArrayElementAtIndex(_list.index);
            var type = (CutsceneStepType)step.FindPropertyRelative("_type").intValue;
            var preset = (CutscenePreset)target;
            if (type == CutsceneStepType.Camera)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("지금 씬 뷰 시점으로 샷 추가"))
                {
                    SceneView view = SceneView.lastActiveSceneView;
                    if (view != null)
                    {
                        Transform shot = LevelPresetUtility.CreatePoint(preset.transform, "Shots", "Shot", view.camera.transform.position, view.camera.transform.rotation);
                        SerializedProperty shots = step.FindPropertyRelative("_shots");
                        shots.arraySize++;
                        shots.GetArrayElementAtIndex(shots.arraySize - 1).objectReferenceValue = shot;
                    }
                }
                SerializedProperty shotList = step.FindPropertyRelative("_shots");
                using (new EditorGUI.DisabledScope(shotList.arraySize == 0))
                {
                    if (GUILayout.Button("첫 샷으로 씬 뷰 맞추기"))
                    {
                        var first = shotList.GetArrayElementAtIndex(0).objectReferenceValue as Transform;
                        if (first != null && SceneView.lastActiveSceneView != null)
                        {
                            SceneView.lastActiveSceneView.AlignViewToObject(first);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("씬 뷰를 원하는 구도로 맞춘 뒤 '샷 추가'를 누르면 그 위치·각도가 샷이 됩니다. 샷이 여러 개면 순서대로 카메라가 이동합니다.", MessageType.None);
            }
            else if (type == CutsceneStepType.Move)
            {
                if (GUILayout.Button("도착 지점 만들기 (씬 뷰 중앙 바닥)"))
                {
                    Vector3 point = LevelPresetUtility.GetSceneViewGroundPoint(preset.transform.position);
                    Transform destination = LevelPresetUtility.CreatePoint(preset.transform, "MovePoints", "Point", point, Quaternion.identity);
                    step.FindPropertyRelative("_destination").objectReferenceValue = destination;
                }
            }
        }

        private void AddStep(CutsceneStepType type)
        {
            SerializedProperty steps = serializedObject.FindProperty("_steps");
            steps.arraySize++;
            SerializedProperty step = steps.GetArrayElementAtIndex(steps.arraySize - 1);
            LevelPresetUtility.ResetStep(step, type);
            _list.index = steps.arraySize - 1;
        }
    }
}
