using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.Battle.Demo
{
    // OS 동적 폰트는 자산으로 저장하면 재로드 후 글리프가 사라져 매번 생성한다.
    [ExecuteAlways]
    public sealed class BattleHudFont : MonoBehaviour
    {
        private Font _font;

        private void OnEnable()
        {
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Arial" }, 24);
            _font.hideFlags = HideFlags.DontSave;
            foreach (Text label in GetComponentsInChildren<Text>(true))
            {
                label.font = _font;
            }
        }

        private void OnDisable()
        {
            if (_font == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(_font);
            }
            else
            {
                DestroyImmediate(_font);
            }
            _font = null;
        }
    }
}
