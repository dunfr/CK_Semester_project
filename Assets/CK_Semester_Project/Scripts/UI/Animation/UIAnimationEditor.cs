using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation
{
    // 씬에 두는 UI 애니메이션 작업대다. 화면은 UIAnimationEditorInspector가 그린다.
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/UI 애니메이션 에디터")]
    public sealed class UIAnimationEditor : MonoBehaviour
    {
        public const string DefaultFolder = "Assets/CK_Semester_Project/Animations/UI";

        [SerializeField] private Image _image;

        public Image Image => _image;
    }
}
