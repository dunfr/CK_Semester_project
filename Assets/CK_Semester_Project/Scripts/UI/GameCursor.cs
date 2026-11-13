using UnityEngine;

namespace CK.SemesterProject.UI
{
    // 게임 마우스 커서를 그림으로 바꾼다.
    // Resources/GameCursor 프리팹을 게임 시작 때 스스로 꺼내 쓰므로 씬에 따로 둘 필요가 없고, 씬이 바뀌어도 유지된다.
    // 커서를 바꾸려면 그 프리팹의 '커서 이미지'에 다른 그림을 끌어다 놓기만 하면 된다.
    // 그림의 임포트 설정(Sprite/Default, 압축, Read/Write)은 상관없고, 흰 배경 같은 단색 배경과 투명 여백은 알아서 정리한다.
    [DisallowMultipleComponent]
    public sealed class GameCursor : MonoBehaviour
    {
        private const string ResourcePath = "GameCursor";
        // 배경을 지울 때 읽는 해상도 상한. 커서로 줄이기 전 단계라 이 정도면 충분하다.
        private const int WorkMaxSize = 1024;
        // 배경색과 채널마다 이만큼(0~255) 이내로 비슷하면 배경으로 본다.
        private const int BackgroundTolerance = 30;
        // 배경과 맞닿은 가장자리 몇 픽셀은 배경색을 빼내 반투명하게 만들어 흰 테두리가 남지 않게 한다.
        private const int EdgeSoftenPixels = 2;
        private const byte VisibleAlpha = 8;

        [Header("커서 이미지")]
        [Tooltip("커서로 쓸 그림. 프로젝트 창의 PNG를 그대로 끌어다 놓으면 된다.")]
        [SerializeField] private Texture2D _image;

        [Header("크기 · 클릭 지점")]
        [Tooltip("화면에 보이는 커서 그림의 긴 변 길이(픽셀)")]
        [SerializeField, Range(16, 256)] private int _size = 64;
        [Tooltip("클릭이 일어나는 가로 위치. 0 = 그림의 왼쪽 끝, 1 = 오른쪽 끝 (투명 여백은 빼고 잰다)")]
        [SerializeField, Range(0f, 1f)] private float _clickPointX;
        [Tooltip("클릭이 일어나는 세로 위치. 0 = 그림의 위쪽 끝, 1 = 아래쪽 끝 (투명 여백은 빼고 잰다)")]
        [SerializeField, Range(0f, 1f)] private float _clickPointY;

        [Header("배경")]
        [Tooltip("그림 네 모서리가 같은 불투명 색(흰 배경 등)이면 테두리와 이어진 그 색을 투명하게 지운다. 이미 투명한 PNG는 그대로 둔다.")]
        [SerializeField] private bool _removeBackground = true;

        private static GameCursor s_instance;

        private Texture2D _runtimeImage;
        private Texture2D _cursorTexture;
        private bool _dirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => s_instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateFromResources()
        {
            if (s_instance != null)
                return;

            GameCursor prefab = Resources.Load<GameCursor>(ResourcePath);
            if (prefab == null)
                return;

            GameCursor instance = Instantiate(prefab);
            instance.name = prefab.name;
        }

        /// <summary>게임 중에 커서 그림을 바꾼다. null을 넘기면 프리팹에 정해 둔 그림으로 돌아간다.</summary>
        public static void SetImage(Texture2D image)
        {
            if (s_instance == null)
                return;

            s_instance._runtimeImage = image;
            s_instance._dirty = true;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (_dirty)
                Apply();
        }

        private void OnDestroy()
        {
            if (s_instance != this)
                return;

            s_instance = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (_cursorTexture != null)
                Destroy(_cursorTexture);
        }

#if UNITY_EDITOR
        // 플레이 중에 값을 바꾸면 바로 커서에 반영한다.
        // 프로젝트 창의 프리팹 원본을 고쳐도 지금 떠 있는 커서에 그대로 옮겨 주므로, 고친 값이 플레이 후에도 남는다.
        private void OnValidate()
        {
            if (!Application.isPlaying)
                return;

            if (s_instance == this)
            {
                _dirty = true;
                return;
            }

            bool isPrefabSource = UnityEditor.EditorUtility.IsPersistent(this)
                || UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null;
            if (s_instance != null && isPrefabSource)
            {
                s_instance._image = _image;
                s_instance._size = _size;
                s_instance._clickPointX = _clickPointX;
                s_instance._clickPointY = _clickPointY;
                s_instance._removeBackground = _removeBackground;
                s_instance._dirty = true;
            }
        }
#endif

        private void Apply()
        {
            _dirty = false;
            Texture2D image = _runtimeImage != null ? _runtimeImage : _image;
            Texture2D previous = _cursorTexture;
            _cursorTexture = null;

            if (image == null)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
            else
            {
                Color32[] pixels = ReadImage(image, out int width, out int height);
                if (_removeBackground)
                    RemoveBackground(pixels, width, height);

                RectInt content = FindContent(pixels, width, height);
                _cursorTexture = BuildCursor(pixels, width, content, _size, new Vector2(_clickPointX, _clickPointY),
                    out Vector2 hotspot);
                Cursor.SetCursor(_cursorTexture, hotspot, CursorMode.Auto);
            }

            if (previous != null)
                Destroy(previous);
        }

        // GPU로 한 번 그려서 읽어 오므로 Read/Write가 꺼져 있거나 압축된 그림도 읽을 수 있다.
        private static Color32[] ReadImage(Texture2D image, out int width, out int height)
        {
            float scale = Mathf.Min(1f, (float)WorkMaxSize / Mathf.Max(image.width, image.height));
            width = Mathf.Max(1, Mathf.RoundToInt(image.width * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(image.height * scale));

            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previousActive = RenderTexture.active;
            Graphics.Blit(image, target);

            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture.active = target;
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);

            Color32[] pixels = readable.GetPixels32();
            Destroy(readable);
            return pixels;
        }

        private static void RemoveBackground(Color32[] pixels, int width, int height)
        {
            // 네 모서리가 모두 같은 불투명 색일 때만 배경이 깔린 그림으로 본다.
            Color32 background = pixels[0];
            int[] corners = { 0, width - 1, (height - 1) * width, height * width - 1 };
            foreach (int corner in corners)
                if (pixels[corner].a < 250 || !IsNear(pixels[corner], background))
                    return;

            // 테두리에서 시작해 배경색으로 이어진 곳만 지운다. 눈 하이라이트처럼 그림 안에 갇힌 같은 색은 남는다.
            const byte Unvisited = byte.MaxValue;
            var distance = new byte[pixels.Length];
            for (int i = 0; i < distance.Length; i++)
                distance[i] = Unvisited;

            var queue = new int[pixels.Length];
            int head = 0, tail = 0;
            for (int x = 0; x < width; x++)
            {
                TrySeed(x);
                TrySeed((height - 1) * width + x);
            }
            for (int y = 0; y < height; y++)
            {
                TrySeed(y * width);
                TrySeed(y * width + width - 1);
            }

            while (head < tail)
            {
                int index = queue[head++];
                int x = index % width;
                if (x > 0) TrySeed(index - 1);
                if (x < width - 1) TrySeed(index + 1);
                if (index >= width) TrySeed(index - width);
                if (index < pixels.Length - width) TrySeed(index + width);
            }

            // 배경에서 1, 2 픽셀 떨어진 가장자리를 표시한다.
            for (byte step = 1; step <= EdgeSoftenPixels; step++)
            {
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (distance[index] == Unvisited && TouchesDistance(distance, width, height, x, y, (byte)(step - 1)))
                        distance[index] = step;
                }
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                if (distance[i] == 0)
                    pixels[i] = new Color32(0, 0, 0, 0);
                else if (distance[i] != Unvisited)
                    pixels[i] = RemoveColor(pixels[i], background);
            }

            void TrySeed(int index)
            {
                if (distance[index] != Unvisited || !IsNear(pixels[index], background))
                    return;

                distance[index] = 0;
                queue[tail++] = index;
            }
        }

        private static bool TouchesDistance(byte[] distance, int width, int height, int x, int y, byte value)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && distance[ny * width + nx] == value)
                    return true;
            }
            return false;
        }

        private static bool IsNear(Color32 a, Color32 b)
        {
            return Mathf.Abs(a.r - b.r) <= BackgroundTolerance
                && Mathf.Abs(a.g - b.g) <= BackgroundTolerance
                && Mathf.Abs(a.b - b.b) <= BackgroundTolerance;
        }

        // 배경색이 섞여 연해진 외곽선 픽셀에서 배경색만 빼내고, 그만큼 투명하게 만든다.
        private static Color32 RemoveColor(Color32 color, Color32 background)
        {
            float alpha = Mathf.Max(ChannelAlpha(color.r, background.r),
                Mathf.Max(ChannelAlpha(color.g, background.g), ChannelAlpha(color.b, background.b)));
            if (alpha <= 0.001f)
                return new Color32(0, 0, 0, 0);

            return new Color32(
                Unmix(color.r, background.r, alpha),
                Unmix(color.g, background.g, alpha),
                Unmix(color.b, background.b, alpha),
                (byte)Mathf.RoundToInt(alpha * color.a));
        }

        private static float ChannelAlpha(byte value, byte background)
        {
            if (value > background) return (value - background) / (255f - background);
            if (value < background) return (background - value) / (float)background;
            return 0f;
        }

        private static byte Unmix(byte value, byte background, float alpha)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt((value - background) / alpha + background), 0, 255);
        }

        // 보이는 부분만 감싸는 사각형. 투명 여백을 잘라 크기와 클릭 지점이 그림 기준이 되게 한다.
        private static RectInt FindContent(Color32[] pixels, int width, int height)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a < VisibleAlpha)
                    continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            return maxX < 0 ? new RectInt(0, 0, width, height) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        // 그림을 size×size 정사각형 안에 비율을 지켜 가운데 맞춰 넣는다.
        // 투명도로 가중해 평균을 내야 줄였을 때 지워진 배경색이 외곽선에 번지지 않는다.
        private static Texture2D BuildCursor(Color32[] pixels, int sourceWidth, RectInt content, int size,
            Vector2 clickPoint, out Vector2 hotspot)
        {
            float scale = (float)size / Mathf.Max(content.width, content.height);
            int width = Mathf.Clamp(Mathf.RoundToInt(content.width * scale), 1, size);
            int height = Mathf.Clamp(Mathf.RoundToInt(content.height * scale), 1, size);
            int offsetX = (size - width) / 2;
            int offsetY = (size - height) / 2;

            var result = new Color32[size * size];
            for (int y = 0; y < height; y++)
            {
                int y0 = content.y + y * content.height / height;
                int y1 = Mathf.Max(y0 + 1, content.y + (y + 1) * content.height / height);
                for (int x = 0; x < width; x++)
                {
                    int x0 = content.x + x * content.width / width;
                    int x1 = Mathf.Max(x0 + 1, content.x + (x + 1) * content.width / width);

                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int sy = y0; sy < y1; sy++)
                    for (int sx = x0; sx < x1; sx++)
                    {
                        Color32 c = pixels[sy * sourceWidth + sx];
                        r += c.r * c.a;
                        g += c.g * c.a;
                        b += c.b * c.a;
                        a += c.a;
                    }

                    if (a <= 0f)
                        continue;

                    int count = (x1 - x0) * (y1 - y0);
                    result[(offsetY + y) * size + offsetX + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(r / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(g / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(b / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a / count), 0, 255));
                }
            }

            // 커서 텍스처는 RGBA32, 읽기 가능, 밉맵 없음이어야 한다.
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "GameCursor (Runtime)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
#if UNITY_EDITOR
            texture.alphaIsTransparency = true;
#endif
            texture.SetPixels32(result);
            texture.Apply(false, false);

            // 커서의 클릭 지점은 텍스처 왼쪽 위에서부터 픽셀로 잰다(아래로 갈수록 y 증가).
            float topMargin = size - offsetY - height;
            hotspot = new Vector2(
                Mathf.Clamp(offsetX + clickPoint.x * width, 0f, size - 1),
                Mathf.Clamp(topMargin + clickPoint.y * height, 0f, size - 1));
            return texture;
        }
    }
}
