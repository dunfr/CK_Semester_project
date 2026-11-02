using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation
{
    // 씬에 두는 UI 애니메이션 작업대다. 화면은 UIAnimationEditorInspector가 그린다.
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/UI 애니메이션 에디터")]
    public sealed class UIAnimationEditor : MonoBehaviour
    {
        public const string DefaultFolder = "Assets/UI/Animation";

        [SerializeField] private Image _image;
        [SerializeField] private string _assetName = string.Empty;
        [SerializeField] private string _folder = DefaultFolder;
        [SerializeField] private UIPointerEffectMask _mouseEvents;
        [SerializeField] private UIAnimationStyle _startStyle;
        [SerializeField] private UIAnimationPreset _startPreset;

        public Image Image => _image;
        public string AssetName => _assetName;
        public string Folder => _folder;
        public UIPointerEffectMask MouseEvents => _mouseEvents;
    }
}
