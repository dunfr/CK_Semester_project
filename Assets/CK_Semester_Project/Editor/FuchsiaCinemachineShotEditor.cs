using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Editor
{
    [CustomEditor(typeof(FuchsiaCinemachineShotClip))]
    public sealed class FuchsiaCinemachineShotEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("상단 Timing의 Start = 시작 시점, Duration = 카메라 무브 길이입니다. Timeline에서 클립을 이동하거나 양끝을 드래그해도 같습니다. Source In/Out은 사용할 원본 구간입니다.", MessageType.Info);
            DrawDefaultInspector();
            var shot = (FuchsiaCinemachineShotClip)target;
            var director = TimelineEditor.inspectedDirector;
            var camera = director != null ? shot.Camera.Resolve(director) : null;
            using (new EditorGUI.DisabledScope(camera == null))
                if (GUILayout.Button("Hierarchy에서 Cinemachine 카메라 찾기")) EditorGUIUtility.PingObject(camera.gameObject);
        }
    }

    [CustomTimelineEditor(typeof(FuchsiaCinemachineShotClip))]
    public sealed class FuchsiaCinemachineShotClipEditor : ClipEditor
    {
        public override ClipDrawOptions GetClipOptions(TimelineClip clip)
        {
            var options = base.GetClipOptions(clip);
            var shot = (FuchsiaCinemachineShotClip)clip.asset;
            options.tooltip = $"시작 {clip.start:F2}초 · 길이 {clip.duration:F2}초\n원본 {shot.SourceIn:F2}~{shot.SourceOut:F2}초\n양끝을 늘리거나 줄이면 카메라 이동도 같은 길이로 재생됩니다.";
            options.icons = new[] { EditorGUIUtility.IconContent("Camera Icon").image as Texture2D };
            if (shot.Motion == null) options.errorText = "카메라 모션 클립을 지정하세요.";
            else if (TimelineEditor.inspectedDirector != null && shot.Camera.Resolve(TimelineEditor.inspectedDirector) == null)
                options.errorText = "Cinemachine Camera를 연결하세요.";
            return options;
        }
    }
}
