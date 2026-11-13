using UnityEngine;

namespace CK.SemesterProject.UI
{
    // Resources/GameCursor 프리팹에 넣은 그림을 게임 시작 때 마우스 커서로 쓴다. 씬에 둘 필요는 없다.
    // 그림은 Texture Type을 Cursor로 임포트해야 한다. 커서를 보이고 숨길 때는 Show/Hide를 쓴다.
    public sealed class GameCursor : MonoBehaviour
    {
        [SerializeField] private Texture2D _image;
        [Tooltip("클릭 지점(픽셀). 그림 왼쪽 위가 (0, 0)")]
        [SerializeField] private Vector2 _hotspot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            GameCursor settings = Resources.Load<GameCursor>(nameof(GameCursor));
            if (settings != null)
                Cursor.SetCursor(settings._image, settings._hotspot, CursorMode.Auto);
        }

        /// <summary>메뉴처럼 마우스로 고르는 화면: 커서를 보이고 풀어 준다.</summary>
        public static void Show()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>필드처럼 마우스로 카메라를 돌리는 화면: 커서를 숨기고 화면 가운데에 잠근다.</summary>
        public static void Hide()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
