using CK.SemesterProject.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign.EditorTools
{
    [CustomEditor(typeof(EnemyRoutePreset))]
    public sealed class EnemyRoutePresetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "• 순찰: 'Waypoints' 아래 자식 오브젝트를 옮기면 경로가 바뀝니다. 하이어라키 순서가 순찰 순서입니다.\n" +
                "• 반응: 'Trigger' 박스 크기·위치와 'ReactionPoint'를 옮겨 설정합니다.\n" +
                "• 적에게 기존 AI(EnemyStateMachine)가 있으면 순찰 중 감지·추적은 그대로 동작합니다.",
                MessageType.None);
            serializedObject.Update();
            Field("_enemy", "움직일 적");
            Field("_waypointRoot", "웨이포인트 묶음");
            Field("_loopMode", "순찰 방식");
            Field("_patrolSpeed", "순찰 속도 (m/s)");
            Field("_waitAtWaypoint", "웨이포인트 대기 (초)");
            Field("_trigger", "트리거 영역");
            if (serializedObject.FindProperty("_trigger").objectReferenceValue != null)
            {
                SerializedProperty mode = serializedObject.FindProperty("_reactionTarget");
                Field("_reactionTarget", "반응 이동 방식");
                if (mode.intValue == (int)EnemyReactionTargetMode.Point)
                {
                    Field("_reactionPoint", "반응 지점");
                }
                else if (mode.intValue == (int)EnemyReactionTargetMode.Direction)
                {
                    Field("_reactionDirection", "반응 방향 (월드)");
                    Field("_reactionDistance", "이동 거리 (m)");
                }
                Field("_reactionSpeed", "반응 속도 (m/s)");
                Field("_lookAtOnArrive", "도착 후 바라볼 곳");
                Field("_returnMode", "복귀 시점");
                Field("_returnDelay", "복귀 대기 (초)");
                Field("_triggerOnce", "1회만 반응");
                Field("_retriggerCooldown", "재반응 대기 (초)");
                Field("_ignoreWhileChasing", "추적 중이면 무시");
            }
            serializedObject.ApplyModifiedProperties();
            var preset = (EnemyRoutePreset)target;
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("웨이포인트 추가"))
            {
                AddWaypoint(preset);
            }
            if (GUILayout.Button("반응 지점 만들기"))
            {
                CreateReactionPoint(preset);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void Field(string propertyName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
        }

        private void AddWaypoint(EnemyRoutePreset preset)
        {
            serializedObject.Update();
            SerializedProperty rootProperty = serializedObject.FindProperty("_waypointRoot");
            var root = rootProperty.objectReferenceValue as Transform;
            if (root == null)
            {
                root = LevelPresetUtility.GetOrCreateChild(preset.transform, "Waypoints");
                rootProperty.objectReferenceValue = root;
            }
            Vector3 position = root.childCount > 0
                ? root.GetChild(root.childCount - 1).position + Vector3.forward * 3f
                : (preset.Enemy != null ? preset.Enemy.transform.position : preset.transform.position);
            Transform point = LevelPresetUtility.CreatePoint(preset.transform, "Waypoints", "WP", position, Quaternion.identity);
            serializedObject.ApplyModifiedProperties();
            Selection.activeTransform = point;
        }

        private void CreateReactionPoint(EnemyRoutePreset preset)
        {
            serializedObject.Update();
            Vector3 position = preset.Trigger != null ? preset.Trigger.transform.position : preset.transform.position + Vector3.forward * 3f;
            Transform point = LevelPresetUtility.CreatePoint(preset.transform, null, "ReactionPoint", position, Quaternion.identity);
            serializedObject.FindProperty("_reactionPoint").objectReferenceValue = point;
            serializedObject.FindProperty("_reactionTarget").intValue = (int)EnemyReactionTargetMode.Point;
            serializedObject.ApplyModifiedProperties();
            Selection.activeTransform = point;
        }
    }
}
