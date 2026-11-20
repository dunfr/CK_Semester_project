using UnityEditor;

namespace CK.SemesterProject.Editor
{
    public static class FuchsiaUltCinemachineValidation
    {
        [MenuItem("Tools/Fuchsia/궁극기 Cinemachine 검증")]
        public static void Validate() => FuchsiaUltReferenceTimelineValidation.Validate();
    }
}
