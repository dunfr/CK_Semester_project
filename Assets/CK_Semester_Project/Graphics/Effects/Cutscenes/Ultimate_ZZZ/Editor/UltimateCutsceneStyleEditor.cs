using UnityEditor;
using UnityEngine;

namespace CK.CutsceneTools
{
    [CustomEditor(typeof(UltimateCutsceneStyle))]
    public sealed class UltimateCutsceneStyleEditor : Editor
    {
        private string _lastReport = "";

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "SC_Ultimate_Workspace 씬을 연 상태에서 사용하세요.\n" +
                "값을 바꾼 뒤 [적용]을 누르면 컷신 인스턴스에 반영되고 프리팹에 Apply 됩니다. Auto Apply 를 켜면 바꿀 때마다 자동 적용됩니다.\n" +
                "휘두름·착지 시각은 Caster 트랙의 Take_B* 클립에서 읽으므로, 모션 타이밍은 Timeline 에서 클립을 옮기면 됩니다.",
                MessageType.Info);

            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("적용 (프리팹 Apply 포함)", GUILayout.Height(30)))
                {
                    _lastReport = UltimateCutsceneBuilder.ApplyAll((UltimateCutsceneStyle)target);
                }
                if (GUILayout.Button("적용 (씬만, 프리팹 Apply 안 함)", GUILayout.Height(30)))
                {
                    _lastReport = UltimateCutsceneBuilder.ApplyAll((UltimateCutsceneStyle)target, false);
                }
            }
            if (!string.IsNullOrEmpty(_lastReport))
            {
                EditorGUILayout.HelpBox(_lastReport, MessageType.None);
            }
        }
    }
}
