using System.Collections.Generic;
using CK.SemesterProject.LevelDesign;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.LevelDesign.EditorTools
{
    /// <summary>레벨 프리셋 생성 메뉴, 씬 뷰 이름표, 공용 도우미.</summary>
    public static class LevelPresetUtility
    {
        private const string MenuRoot = "GameObject/CK Level Presets/";

        [MenuItem(MenuRoot + "적 이동 프리셋 (Enemy Route)", false, 10)]
        private static void CreateEnemyRouteMenu(MenuCommand command)
        {
            NavMeshAgent enemy = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<NavMeshAgent>() : null;
            GameObject root = CreateEnemyRoute(enemy, true);
            Selection.activeGameObject = root;
            if (enemy == null)
            {
                Debug.Log("적 이동 프리셋: 인스펙터에서 움직일 적(NavMeshAgent)을 지정하세요. 적을 선택한 채로 메뉴를 실행하면 자동으로 연결됩니다.", root);
            }
        }

        /// <summary>적 이동 프리셋을 만든다. withTrigger가 false면 순찰만 하는 프리셋(트리거·반응 지점 없음).</summary>
        public static GameObject CreateEnemyRoute(NavMeshAgent enemy, bool withTrigger)
        {
            Vector3 origin = enemy != null ? enemy.transform.position : GetSceneViewGroundPoint(Vector3.zero);
            Vector3 forward = enemy != null ? Vector3.ProjectOnPlane(enemy.transform.forward, Vector3.up).normalized : Vector3.forward;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }

            var root = new GameObject(enemy != null ? "EnemyRoute_" + enemy.name : "EnemyRoute");
            Undo.RegisterCreatedObjectUndo(root, "적 이동 프리셋 만들기");
            if (enemy != null && enemy.transform.parent != null)
            {
                Undo.SetTransformParent(root.transform, enemy.transform.parent, "적 이동 프리셋 만들기");
            }
            root.transform.position = origin;
            var preset = Undo.AddComponent<EnemyRoutePreset>(root);

            Transform waypointRoot = GetOrCreateChild(root.transform, "Waypoints");
            CreatePoint(root.transform, "Waypoints", "WP", origin, Quaternion.identity);
            CreatePoint(root.transform, "Waypoints", "WP", origin + forward * 4f, Quaternion.identity);
            CreatePoint(root.transform, "Waypoints", "WP", origin + forward * 4f + Vector3.Cross(Vector3.up, forward) * 4f, Quaternion.identity);

            var serialized = new SerializedObject(preset);
            serialized.FindProperty("_enemy").objectReferenceValue = enemy;
            serialized.FindProperty("_waypointRoot").objectReferenceValue = waypointRoot;
            if (withTrigger)
            {
                serialized.FindProperty("_trigger").objectReferenceValue = CreateTrigger(root.transform, origin + forward * 8f);
                serialized.FindProperty("_reactionPoint").objectReferenceValue = CreatePoint(root.transform, null, "ReactionPoint", origin + forward * 6f, Quaternion.identity);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        [MenuItem(MenuRoot + "컷씬 프리셋 (Cutscene)", false, 11)]
        private static void CreateCutscene(MenuCommand command)
        {
            Vector3 origin = GetSceneViewGroundPoint(Vector3.zero);
            var root = new GameObject("Cutscene");
            Undo.RegisterCreatedObjectUndo(root, "컷씬 프리셋 만들기");
            if (Selection.activeTransform != null)
            {
                Undo.SetTransformParent(root.transform, Selection.activeTransform, "컷씬 프리셋 만들기");
            }
            root.transform.position = origin;
            var preset = Undo.AddComponent<CutscenePreset>(root);
            PresetTriggerZone trigger = CreateTrigger(root.transform, origin);
            Transform shot = null;
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                shot = CreatePoint(root.transform, "Shots", "Shot", view.camera.transform.position, view.camera.transform.rotation);
            }
            Transform movePoint = CreatePoint(root.transform, "MovePoints", "Point", origin + Vector3.forward * 2f, Quaternion.identity);

            var serialized = new SerializedObject(preset);
            serialized.FindProperty("_trigger").objectReferenceValue = trigger;
            TMP_Text sceneText = Object.FindFirstObjectByType<TMP_Text>();
            if (sceneText != null)
            {
                serialized.FindProperty("_dialogueFont").objectReferenceValue = sceneText.font;
            }
            SerializedProperty steps = serialized.FindProperty("_steps");
            steps.arraySize = 3;
            ResetStep(steps.GetArrayElementAtIndex(0), CutsceneStepType.Camera);
            if (shot != null)
            {
                SerializedProperty shots = steps.GetArrayElementAtIndex(0).FindPropertyRelative("_shots");
                shots.arraySize = 1;
                shots.GetArrayElementAtIndex(0).objectReferenceValue = shot;
            }
            ResetStep(steps.GetArrayElementAtIndex(1), CutsceneStepType.Dialogue);
            steps.GetArrayElementAtIndex(1).FindPropertyRelative("_start").intValue = (int)CutsceneStepStart.WithPrevious;
            ResetStep(steps.GetArrayElementAtIndex(2), CutsceneStepType.Move);
            steps.GetArrayElementAtIndex(2).FindPropertyRelative("_destination").objectReferenceValue = movePoint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeGameObject = root;
        }

        public static void ResetStep(SerializedProperty step, CutsceneStepType type)
        {
            step.FindPropertyRelative("_type").intValue = (int)type;
            step.FindPropertyRelative("_start").intValue = (int)CutsceneStepStart.AfterPrevious;
            step.FindPropertyRelative("_delay").floatValue = 0f;
            step.FindPropertyRelative("_speaker").stringValue = "푸시아";
            step.FindPropertyRelative("_text").stringValue = type == CutsceneStepType.Dialogue ? "대사를 입력하세요." : "";
            step.FindPropertyRelative("_duration").floatValue = 0f;
            step.FindPropertyRelative("_actor").objectReferenceValue = null;
            step.FindPropertyRelative("_destination").objectReferenceValue = null;
            step.FindPropertyRelative("_moveSpeed").floatValue = 2f;
            step.FindPropertyRelative("_matchDestinationRotation").boolValue = true;
            step.FindPropertyRelative("_shots").arraySize = 0;
            step.FindPropertyRelative("_travelSeconds").floatValue = 2f;
            step.FindPropertyRelative("_holdSeconds").floatValue = 1f;
            step.FindPropertyRelative("_lookAt").objectReferenceValue = null;
            step.FindPropertyRelative("_easeInOut").boolValue = true;
            step.FindPropertyRelative("_waitSeconds").floatValue = 1f;
        }

        public static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }
            var created = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(created, "프리셋 오브젝트 만들기");
            Undo.SetTransformParent(created.transform, parent, "프리셋 오브젝트 만들기");
            created.transform.localPosition = Vector3.zero;
            created.transform.localRotation = Quaternion.identity;
            return created.transform;
        }

        /// <summary>parent(아래 group) 아래에 번호 붙은 빈 지점을 만든다. group이 null이면 parent 바로 아래.</summary>
        public static Transform CreatePoint(Transform parent, string group, string prefix, Vector3 position, Quaternion rotation)
        {
            Transform container = string.IsNullOrEmpty(group) ? parent : GetOrCreateChild(parent, group);
            int number = container.childCount + 1;
            string pointName = string.IsNullOrEmpty(group) ? prefix : prefix + "_" + number.ToString("00");
            var point = new GameObject(pointName);
            Undo.RegisterCreatedObjectUndo(point, "프리셋 지점 만들기");
            Undo.SetTransformParent(point.transform, container, "프리셋 지점 만들기");
            point.transform.SetPositionAndRotation(position, rotation);
            return point.transform;
        }

        public static Vector3 GetSceneViewGroundPoint(Vector3 fallback)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                return fallback;
            }
            Vector3 pivot = view.pivot;
            return Physics.Raycast(pivot + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : pivot;
        }

        private static PresetTriggerZone CreateTrigger(Transform parent, Vector3 position)
        {
            var triggerObject = new GameObject("Trigger");
            Undo.RegisterCreatedObjectUndo(triggerObject, "트리거 영역 만들기");
            Undo.SetTransformParent(triggerObject.transform, parent, "트리거 영역 만들기");
            triggerObject.transform.position = position;
            var box = triggerObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4f, 3f, 4f);
            box.center = new Vector3(0f, 1.5f, 0f);
            return triggerObject.AddComponent<PresetTriggerZone>();
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawEnemyRouteLabels(EnemyRoutePreset preset, GizmoType gizmoType)
        {
            var seen = new HashSet<Transform>();
            List<Transform> route = preset.GetRoute();
            for (int i = 0; i < route.Count; i++)
            {
                if (route[i] != null && seen.Add(route[i]))
                {
                    Handles.Label(route[i].position + Vector3.up * 0.6f, (i + 1).ToString(), EditorStyles.whiteBoldLabel);
                }
            }
            if (preset.Trigger != null)
            {
                Handles.Label(preset.Trigger.transform.position + Vector3.up * 3.2f, "반응 영역 · " + preset.name, EditorStyles.whiteMiniLabel);
            }
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawCutsceneLabels(CutscenePreset preset, GizmoType gizmoType)
        {
            if (preset.Trigger != null)
            {
                Handles.Label(preset.Trigger.transform.position + Vector3.up * 3.2f, "컷씬 · " + preset.name, EditorStyles.whiteMiniLabel);
            }
            for (int i = 0; i < preset.Steps.Count; i++)
            {
                CutsceneStep step = preset.Steps[i];
                if (step == null)
                {
                    continue;
                }
                if (step.Type == CutsceneStepType.Camera && step.Shots != null)
                {
                    for (int s = 0; s < step.Shots.Length; s++)
                    {
                        if (step.Shots[s] != null)
                        {
                            Handles.Label(step.Shots[s].position + Vector3.up * 0.2f, "#" + (i + 1) + " 샷 " + (s + 1), EditorStyles.whiteMiniLabel);
                        }
                    }
                }
                else if (step.Type == CutsceneStepType.Move && step.Destination != null)
                {
                    Handles.Label(step.Destination.position + Vector3.up * 0.5f, "#" + (i + 1) + " 이동", EditorStyles.whiteMiniLabel);
                }
            }
        }
    }
}
