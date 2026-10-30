using CK.SemesterProject.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign.EditorTools
{
    /// <summary>컷씬 단계를 종류에 맞는 항목만 보이게 그린다.</summary>
    [CustomPropertyDrawer(typeof(CutsceneStep))]
    public sealed class CutsceneStepDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            foreach (string field in GetFields(property))
            {
                SerializedProperty child = property.FindPropertyRelative(field);
                line.height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(line, child, new GUIContent(GetLabel(field, property), child.tooltip), true);
                line.y += line.height + Spacing;
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = 0f;
            foreach (string field in GetFields(property))
            {
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + Spacing;
            }
            return height;
        }

        public static string Summarize(SerializedProperty property)
        {
            var type = (CutsceneStepType)property.FindPropertyRelative("_type").intValue;
            bool together = property.FindPropertyRelative("_start").intValue == (int)CutsceneStepStart.WithPrevious;
            string prefix = together ? "＋ " : "";
            switch (type)
            {
                case CutsceneStepType.Dialogue:
                    string text = property.FindPropertyRelative("_text").stringValue;
                    if (text.Length > 18)
                    {
                        text = text.Substring(0, 18) + "…";
                    }
                    return prefix + "대사 · " + property.FindPropertyRelative("_speaker").stringValue + " : " + text;
                case CutsceneStepType.Move:
                    Object actor = property.FindPropertyRelative("_actor").objectReferenceValue;
                    return prefix + "이동 · " + (actor != null ? actor.name : "플레이어");
                case CutsceneStepType.Camera:
                    return prefix + "카메라 · 샷 " + property.FindPropertyRelative("_shots").arraySize + "개";
                default:
                    return prefix + "대기 · " + property.FindPropertyRelative("_waitSeconds").floatValue + "초";
            }
        }

        private static string[] GetFields(SerializedProperty property)
        {
            switch ((CutsceneStepType)property.FindPropertyRelative("_type").intValue)
            {
                case CutsceneStepType.Dialogue:
                    return new[] { "_type", "_start", "_delay", "_speaker", "_text", "_duration" };
                case CutsceneStepType.Move:
                    return new[] { "_type", "_start", "_delay", "_actor", "_destination", "_moveSpeed", "_matchDestinationRotation" };
                case CutsceneStepType.Camera:
                    return new[] { "_type", "_start", "_delay", "_shots", "_travelSeconds", "_holdSeconds", "_lookAt", "_easeInOut" };
                default:
                    return new[] { "_type", "_start", "_delay", "_waitSeconds" };
            }
        }

        private static string GetLabel(string field, SerializedProperty property)
        {
            switch (field)
            {
                case "_type": return "종류";
                case "_start": return "시작";
                case "_delay": return "시작 전 대기(초)";
                case "_speaker": return "말하는 캐릭터";
                case "_text": return "대사";
                case "_duration": return "표시 시간(0=키 입력)";
                case "_actor": return "캐릭터(비우면 플레이어)";
                case "_destination": return "도착 지점";
                case "_moveSpeed": return "이동 속도";
                case "_matchDestinationRotation": return "도착 방향으로 회전";
                case "_shots": return "카메라 샷(순서대로)";
                case "_travelSeconds": return "샷 사이 이동 시간(초)";
                case "_holdSeconds": return "마지막 샷 유지(초)";
                case "_lookAt": return "계속 바라볼 대상";
                case "_easeInOut": return "부드럽게 시작·정지";
                case "_waitSeconds": return "대기 시간(초)";
                default: return ObjectNames.NicifyVariableName(field);
            }
        }
    }
}
