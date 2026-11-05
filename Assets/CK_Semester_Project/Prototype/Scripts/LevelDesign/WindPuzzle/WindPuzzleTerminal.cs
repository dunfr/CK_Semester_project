using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.LevelDesign.WindPuzzle
{
    /// <summary>
    /// 05 구역 바람길 퍼즐 장치(홀로그램). F로 조작을 시작하면 퍼즐 카메라로 바뀌고 마우스로 ㄱ자 관을 돌리며
    /// 버튼(A, B, C…)으로 송풍구를 켠다. 상자를 모두 출구로 빼내면 _solved 이벤트로 바람 연출을 시작한다.
    /// 퍼즐 판은 기획용 HTML 에디터에서 만든 퍼즐 코드(P1-…)를 그대로 붙여 넣어 바꾼다.
    /// </summary>
    public sealed class WindPuzzleTerminal : LevelInteractable
    {
        [Header("Puzzle")]
        [SerializeField, TextArea(2, 4), Tooltip("바람길 퍼즐 HTML 에디터의 [코드 복사] 결과(P1-…)")]
        private string _puzzleCode = "P1-WyLqvKzsnbgg7KeA67aVIiw1LDUsIkwtLS03LjctRkYufC58fC58Li58LnwuLnwiLFs5XSxbMTZdLFs5OCwwLDM3LDU0XSxbWyJBIixbMV1dLFsiQiIsWzEsMiw0XV0sWyJDIixbMiwzLDRdXV1d";
        [SerializeField, Tooltip("완료 후 다시 조작할 수 없게 합니다.")]
        private bool _lockAfterSolved = true;

        [Header("Hologram")]
        [SerializeField, Tooltip("홀로그램 판을 만들 기준점. 이 오브젝트의 앞(+Z)이 플레이어 반대쪽을 향하게 둡니다.")]
        private Transform _hologramRoot;
        [SerializeField, Min(0.05f)] private float _cellSize = 0.3f;
        [SerializeField, Tooltip("홀로그램용 반투명(가산) 머티리얼")]
        private Material _hologramMaterial;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private Color _frameColor = new Color(0.02f, 0.08f, 0.16f, 0.6f);
        [SerializeField] private Color _gridColor = new Color(0.25f, 0.7f, 1f, 0.14f);
        [SerializeField] private Color _fixedPipeColor = new Color(0.45f, 0.8f, 1f, 0.9f);
        [SerializeField] private Color _bendPipeColor = new Color(1f, 0.55f, 0.12f, 0.95f);
        [SerializeField] private Color _boxColor = new Color(1f, 0.82f, 0.45f, 0.95f);
        [SerializeField] private Color _exitColor = new Color(0.3f, 1f, 0.45f, 0.95f);
        [SerializeField] private Color _ventColor = new Color(0.3f, 0.55f, 1f, 0.95f);
        [SerializeField] private Color _windColor = new Color(0.65f, 0.95f, 1f, 1f);
        [SerializeField] private Color _solvedColor = new Color(0.35f, 1f, 0.55f, 0.9f);

        [Header("Camera")]
        [SerializeField, Tooltip("퍼즐 조작 중에 켤 카메라(평소 꺼 둠)")]
        private CinemachineCamera _puzzleCamera;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float _windSeconds = 0.32f;
        [SerializeField, Min(0.01f)] private float _slideSecondsPerTile = 0.075f;

        [Header("Events")]
        [SerializeField, Tooltip("퍼즐을 풀었을 때(바람 연출 시작)")]
        private UnityEvent _solved = new UnityEvent();

        private WindPuzzleLevel _level;
        private WindPuzzleScreen _screen;
        private WindPuzzleState _state;
        private readonly List<WindPuzzleState> _undo = new List<WindPuzzleState>();
        private Transform _board;
        private Transform[,] _cellPipes;
        private readonly Dictionary<Collider, Vector2Int> _cellColliders = new Dictionary<Collider, Vector2Int>();
        private readonly Dictionary<Collider, string> _buttonColliders = new Dictionary<Collider, string>();
        private readonly Dictionary<string, Renderer> _buttonRenderers = new Dictionary<string, Renderer>();
        private readonly List<Transform> _boxVisuals = new List<Transform>();
        private readonly List<(Renderer renderer, Color color)> _tinted = new List<(Renderer, Color)>();
        private TextMeshPro _statusText;
        private readonly Dictionary<LineRenderer, Vector3[]> _linePoints = new Dictionary<LineRenderer, Vector3[]>();
        private MaterialPropertyBlock _block;
        private bool _isActive;
        private bool _isBusy;
        private bool _isSolved;
        private Behaviour _cameraController;
        private Renderer[] _hiddenPlayerRenderers = Array.Empty<Renderer>();
        private float _flickerSeed;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public override bool CanInteract => !_isActive && !(_isSolved && _lockAfterSolved);
        public bool IsSolved => _isSolved;
        // 퍼즐 화면을 조작 중이면 Esc는 퍼즐 나가기라서 인게임 메뉴가 열리지 않는다.
        public bool IsActive => _isActive;

        protected override void Awake()
        {
            base.Awake();
            _block = new MaterialPropertyBlock();
            _flickerSeed = UnityEngine.Random.value * 10f;
            try
            {
                _level = WindPuzzleEngine.Decode(_puzzleCode);
            }
            catch (Exception exception)
            {
                Debug.LogError(name + ": 퍼즐 코드를 읽을 수 없습니다. " + exception.Message, this);
                enabled = false;
                return;
            }
            _state = WindPuzzleEngine.InitialState(_level);
            if (_puzzleCamera != null)
            {
                _puzzleCamera.gameObject.SetActive(false);
            }
            BuildBoard();
            SetCollidersEnabled(false);
            // 조작 중에는 화면 UI로 또렷하게 보여 준다(월드 홀로그램은 분위기용으로 같은 상태를 따라간다).
            _screen = new WindPuzzleScreen(_level, _font, _level.Name);
            _screen.CellClicked += OnScreenCellClicked;
            _screen.ButtonClicked += OnScreenButtonClicked;
            _screen.Refresh(_state);
        }

        private void OnDestroy()
        {
            _screen?.Destroy();
        }

        private void OnScreenCellClicked(int x, int y)
        {
            if (!_isActive || _isBusy || !WindPuzzleEngine.CanRotate(_level, _state, x, y))
            {
                return;
            }
            _undo.Add(_state);
            _state = WindPuzzleEngine.Rotate(_state, x, y);
            BuildPipes(x, y);
            _screen.RefreshPipe(_state, x, y);
        }

        private void OnScreenButtonClicked(string key)
        {
            if (_isActive && !_isBusy)
            {
                StartCoroutine(Press(key));
            }
        }

        public override void Interact()
        {
            if (!CanInteract)
            {
                return;
            }
            _isActive = true;
            Progress.LockControls(this);
            Transform player = Progress.Player;
            if (player != null)
            {
                // 퍼즐 화면을 가리지 않도록 조작하는 동안 캐릭터를 숨긴다.
                _hiddenPlayerRenderers = Array.FindAll(player.GetComponentsInChildren<Renderer>(), r => r.enabled);
                foreach (Renderer r in _hiddenPlayerRenderers)
                {
                    r.enabled = false;
                }
                foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>())
                {
                    if (behaviour.GetType().Name == "CameraController")
                    {
                        _cameraController = behaviour;
                        // 커서를 풀어 두고, 클릭해도 다시 잠기지 않게 잠시 끈다.
                        behaviour.enabled = false;
                    }
                }
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_puzzleCamera != null)
            {
                _puzzleCamera.gameObject.SetActive(true);
            }
            _screen.SetVisible(true);
            SetStatus(_level.Name + "\n<size=60%>클릭: 주황 관 회전 · " + string.Join("/", _level.ButtonKeys) + ": 송풍 · Z: 되돌리기 · R: 처음부터 · Esc: 나가기</size>");
        }

        private void ExitPuzzle()
        {
            _isActive = false;
            SetCollidersEnabled(false);
            _screen.SetVisible(false);
            if (_puzzleCamera != null)
            {
                _puzzleCamera.gameObject.SetActive(false);
            }
            if (_cameraController != null)
            {
                _cameraController.enabled = true;
            }
            foreach (Renderer r in _hiddenPlayerRenderers)
            {
                if (r != null)
                {
                    r.enabled = true;
                }
            }
            _hiddenPlayerRenderers = Array.Empty<Renderer>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Progress.UnlockControls(this);
            if (!_isSolved)
            {
                SetStatus(_level.Name);
            }
        }

        private void Update()
        {
            Flicker();
            if (!_isActive || _isBusy)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    ExitPuzzle();
                    return;
                }
                if (keyboard.zKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                {
                    Undo();
                    return;
                }
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    ResetBoard();
                    return;
                }
                foreach (string key in _level.ButtonKeys)
                {
                    if (Enum.TryParse(key, out Key parsed) && keyboard[parsed].wasPressedThisFrame)
                    {
                        StartCoroutine(Press(key));
                        return;
                    }
                }
            }

        }

        private void HandleClick(Vector2 screenPoint)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            Ray ray = camera.ScreenPointToRay(screenPoint);
            RaycastHit[] hits = Physics.RaycastAll(ray, 20f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (_buttonColliders.TryGetValue(hit.collider, out string key))
                {
                    StartCoroutine(Press(key));
                    return;
                }
                if (_cellColliders.TryGetValue(hit.collider, out Vector2Int cell))
                {
                    if (WindPuzzleEngine.CanRotate(_level, _state, cell.x, cell.y))
                    {
                        _undo.Add(_state);
                        _state = WindPuzzleEngine.Rotate(_state, cell.x, cell.y);
                        BuildPipes(cell.x, cell.y);
                    }
                    return;
                }
            }
        }

        private void Undo()
        {
            if (_undo.Count == 0)
            {
                return;
            }
            _state = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            RefreshAll();
        }

        private void ResetBoard()
        {
            _undo.Add(_state);
            _state = WindPuzzleEngine.InitialState(_level);
            RefreshAll();
        }

        private IEnumerator Press(string key)
        {
            _isBusy = true;
            if (_buttonRenderers.TryGetValue(key, out Renderer buttonRenderer))
            {
                SetBaseColor(buttonRenderer, _windColor);
            }
            WindPuzzleEngine.PressResult result = WindPuzzleEngine.PressButton(_level, _state, key);
            // 1) 바람이 관을 따라 흐른다.
            var lines = new List<LineRenderer>();
            var screenWind = new List<List<UnityEngine.UI.Image>>();
            _screen.SetButtonActive(key, true);
            foreach (WindPuzzleEngine.WindTrace trace in result.Traces)
            {
                if (trace.Points.Count > 1)
                {
                    lines.Add(CreateWindLine(trace.Points));
                    screenWind.Add(_screen.CreateWind(trace.Points));
                }
            }
            for (float elapsed = 0f; elapsed < _windSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / _windSeconds;
                foreach (LineRenderer line in lines)
                {
                    RevealLine(line, t);
                }
                foreach (List<UnityEngine.UI.Image> wind in screenWind)
                {
                    WindPuzzleScreen.RevealWind(wind, t);
                }
                yield return null;
            }
            foreach (LineRenderer line in lines)
            {
                RevealLine(line, 1f);
            }
            foreach (List<UnityEngine.UI.Image> wind in screenWind)
            {
                WindPuzzleScreen.RevealWind(wind, 1f);
            }
            // 2) 바람을 맞은 상자가 막힐 때까지 미끄러진다.
            int longest = 0;
            foreach (WindPuzzleEngine.BoxMove move in result.Moves)
            {
                longest = Mathf.Max(longest, move.Path.Count - 1);
            }
            float slideSeconds = Mathf.Max(0.05f, longest * _slideSecondsPerTile);
            for (float elapsed = 0f; elapsed < slideSeconds; elapsed += Time.deltaTime)
            {
                foreach (WindPuzzleEngine.BoxMove move in result.Moves)
                {
                    PlaceBoxAlongPath(move, Mathf.Clamp01(elapsed / (Mathf.Max(1, move.Path.Count - 1) * _slideSecondsPerTile)));
                }
                yield return null;
            }
            foreach (LineRenderer line in lines)
            {
                _linePoints.Remove(line);
                Destroy(line.gameObject);
            }
            foreach (List<UnityEngine.UI.Image> wind in screenWind)
            {
                WindPuzzleScreen.ClearWind(wind);
            }
            _screen.SetButtonActive(key, false);
            _undo.Add(_state);
            _state = result.State;
            RefreshAll();
            if (buttonRenderer != null)
            {
                SetBaseColor(buttonRenderer, _ventColor);
            }
            _isBusy = false;
            if (WindPuzzleEngine.IsWon(_state))
            {
                yield return Solve();
            }
        }

        private IEnumerator Solve()
        {
            _isSolved = true;
            _isBusy = true;
            SetStatus("바람길 개방");
            _screen.FlashSolved();
            for (int i = 0; i < 3; i++)
            {
                SetBoardColor(_solvedColor);
                yield return new WaitForSeconds(0.15f);
                SetBoardColor(null);
                yield return new WaitForSeconds(0.1f);
            }
            SetBoardColor(_solvedColor);
            yield return new WaitForSeconds(0.9f);
            _isBusy = false;
            ExitPuzzle();
            _solved.Invoke();
        }

        // ------------------------------------------------------------------ 화면 구성

        private Vector3 CellToLocal(float gx, float gy)
        {
            // 격자 좌표(0..W, 0..H, y는 아래로 증가)를 판 로컬 좌표로.
            return new Vector3((gx - _level.W * 0.5f) * _cellSize, (_level.H * 0.5f - gy) * _cellSize, 0f);
        }

        private void BuildBoard()
        {
            if (_hologramRoot == null)
            {
                _hologramRoot = transform;
            }
            _board = new GameObject("WindPuzzle_Board").transform;
            _board.SetParent(_hologramRoot, false);
            _cellPipes = new Transform[_level.H, _level.W];
            Quad(_board, "Frame", CellToLocal(_level.W * 0.5f, _level.H * 0.5f) + Vector3.forward * 0.02f,
                new Vector2((_level.W + 0.6f) * _cellSize, (_level.H + 0.6f) * _cellSize), 0f, _frameColor);
            for (int y = 0; y < _level.H; y++)
            {
                for (int x = 0; x < _level.W; x++)
                {
                    Transform cell = Quad(_board, "Cell_" + x + "_" + y, CellToLocal(x + 0.5f, y + 0.5f) + Vector3.forward * 0.01f,
                        Vector2.one * _cellSize * 0.94f, 0f, _gridColor).transform;
                    var collider = cell.gameObject.AddComponent<BoxCollider>();
                    collider.isTrigger = true;
                    collider.size = new Vector3(1f, 1f, 0.05f);
                    _cellColliders[collider] = new Vector2Int(x, y);
                    BuildPipes(x, y);
                }
            }
            foreach (WindPuzzleLevel.Exit exit in _level.Exits)
            {
                Vector3 center = CellToLocal(exit.X + 0.5f + WindPuzzleEngine.DX[exit.Side] * 0.75f, exit.Y + 0.5f + WindPuzzleEngine.DY[exit.Side] * 0.75f);
                float angle = SideAngle(exit.Side);
                Quad(_board, "Exit", center, new Vector2(_cellSize * 0.5f, _cellSize * 0.12f), angle + 45f, _exitColor, -0.005f, center + (Vector3)(Rotate2(new Vector2(-0.06f, -0.06f), angle) * _cellSize));
                Quad(_board, "Exit", center, new Vector2(_cellSize * 0.5f, _cellSize * 0.12f), angle - 45f, _exitColor, -0.005f, center + (Vector3)(Rotate2(new Vector2(0.06f, -0.06f), angle) * _cellSize));
                Label(_board, "EXIT", center + Vector3.up * 0f + (Vector3)(Rotate2(new Vector2(0f, 0.28f), angle) * _cellSize), 0.5f, _exitColor);
            }
            foreach (WindPuzzleLevel.Vent vent in _level.Vents)
            {
                Vector3 center = CellToLocal(vent.X + 0.5f + WindPuzzleEngine.DX[vent.Side] * 0.62f, vent.Y + 0.5f + WindPuzzleEngine.DY[vent.Side] * 0.62f);
                bool horizontal = vent.Side == 0 || vent.Side == 2;
                Quad(_board, "Vent_" + vent.Id, center, horizontal ? new Vector2(_cellSize * 0.6f, _cellSize * 0.2f) : new Vector2(_cellSize * 0.2f, _cellSize * 0.6f), 0f, _ventColor, -0.005f);
                Label(_board, vent.Id.ToString(), center + (Vector3)(new Vector2(WindPuzzleEngine.DX[vent.Side], -WindPuzzleEngine.DY[vent.Side]) * _cellSize * 0.28f), 0.55f, _ventColor);
            }
            float buttonY = -(_level.H * 0.5f + 1.0f) * _cellSize;
            for (int i = 0; i < _level.ButtonKeys.Count; i++)
            {
                string key = _level.ButtonKeys[i];
                float x = (i - (_level.ButtonKeys.Count - 1) * 0.5f) * _cellSize * 1.4f;
                Renderer button = Quad(_board, "Button_" + key, new Vector3(x, buttonY, 0f), new Vector2(_cellSize * 1.2f, _cellSize * 0.7f), 0f, _ventColor);
                var collider = button.gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(1f, 1f, 0.05f);
                _buttonColliders[collider] = key;
                _buttonRenderers[key] = button;
                string vents = string.Join(",", Array.ConvertAll(_level.Buttons[key], v => v.ToString()));
                Label(_board, key + "<size=55%>\n" + vents + "</size>", new Vector3(x, buttonY, -0.01f), 0.8f, Color.white);
            }
            var statusObject = new GameObject("Status");
            statusObject.transform.SetParent(_board, false);
            statusObject.transform.localPosition = new Vector3(0f, -(_level.H * 0.5f + 2.0f) * _cellSize, -0.01f);
            _statusText = statusObject.AddComponent<TextMeshPro>();
            if (_font != null)
            {
                _statusText.font = _font;
            }
            _statusText.alignment = TextAlignmentOptions.Center;
            _statusText.fontSize = _cellSize * 2.6f;
            _statusText.fontStyle = FontStyles.Bold;
            _statusText.rectTransform.sizeDelta = new Vector2(_cellSize * 12f, _cellSize * 1.6f);
            _statusText.color = _fixedPipeColor;
            SetStatus(_level.Name);
            RefreshBoxes();
        }

        private static float SideAngle(int side) => side == 0 ? 0f : side == 1 ? -90f : side == 2 ? 180f : 90f;

        private static Vector2 Rotate2(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
        }

        private void BuildPipes(int x, int y)
        {
            if (_cellPipes[y, x] != null)
            {
                foreach (Renderer r in _cellPipes[y, x].GetComponentsInChildren<Renderer>())
                {
                    _tinted.RemoveAll(entry => entry.renderer == r);
                }
                Destroy(_cellPipes[y, x].gameObject);
            }
            var root = new GameObject("Pipes_" + x + "_" + y).transform;
            root.SetParent(_board, false);
            _cellPipes[y, x] = root;
            char c = _state.Grid[y, x];
            int mask = WindPuzzleEngine.Mask(c);
            if (mask == 0)
            {
                return;
            }
            Color color = WindPuzzleEngine.IsBend(c) ? _bendPipeColor : _fixedPipeColor;
            Vector3 center = CellToLocal(x + 0.5f, y + 0.5f);
            float thickness = _cellSize * 0.26f;
            for (int d = 0; d < 4; d++)
            {
                if ((mask & WindPuzzleEngine.Bit(d)) == 0)
                {
                    continue;
                }
                Vector3 offset = new Vector3(WindPuzzleEngine.DX[d], -WindPuzzleEngine.DY[d], 0f) * _cellSize * 0.25f;
                bool horizontal = d == 1 || d == 3;
                Quad(root, "Arm", center + offset, horizontal ? new Vector2(_cellSize * 0.5f + thickness * 0.5f, thickness) : new Vector2(thickness, _cellSize * 0.5f + thickness * 0.5f), 0f, color, -0.004f);
            }
        }

        private void RefreshAll()
        {
            _screen?.Refresh(_state);
            for (int y = 0; y < _level.H; y++)
            {
                for (int x = 0; x < _level.W; x++)
                {
                    BuildPipes(x, y);
                }
            }
            RefreshBoxes();
        }

        private void RefreshBoxes()
        {
            while (_boxVisuals.Count < _state.Boxes.Length)
            {
                Renderer box = Quad(_board, "Box", Vector3.zero, Vector2.one * _cellSize * 0.55f, 0f, _boxColor, -0.008f);
                Quad(box.transform, "BoxCore", new Vector3(0f, 0f, -0.002f), Vector2.one * 0.55f, 45f, new Color(1f, 1f, 1f, 0.6f), 0f, null, true);
                _boxVisuals.Add(box.transform);
            }
            for (int i = 0; i < _state.Boxes.Length; i++)
            {
                bool present = _state.Boxes[i].HasValue;
                _boxVisuals[i].gameObject.SetActive(present);
                if (present)
                {
                    Vector2Int b = _state.Boxes[i].Value;
                    _boxVisuals[i].localPosition = CellToLocal(b.x + 0.5f, b.y + 0.5f) + Vector3.back * 0.008f;
                }
            }
        }

        private void PlaceBoxAlongPath(WindPuzzleEngine.BoxMove move, float t)
        {
            Transform visual = _boxVisuals[move.Box];
            float f = t * (move.Path.Count - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), move.Path.Count - 2);
            Vector2 p = Vector2.Lerp(move.Path[i], move.Path[i + 1], f - i);
            visual.localPosition = CellToLocal(p.x, p.y) + Vector3.back * 0.008f;
            float shrink = move.Exited ? Mathf.Lerp(1f, 0.3f, Mathf.Clamp01((t - 0.7f) / 0.3f)) : 1f;
            if (move.Exited)
            {
                visual.localScale = Vector3.one * shrink * _cellSize * 0.55f;
            }
            _screen.PlaceBox(move.Box, p, shrink);
        }

        private LineRenderer CreateWindLine(List<Vector2> points)
        {
            var go = new GameObject("WindLine");
            go.transform.SetParent(_board, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = _hologramMaterial;
            line.widthMultiplier = _cellSize * 0.12f;
            line.numCapVertices = 2;
            line.startColor = line.endColor = _windColor;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, _windColor);
            line.SetPropertyBlock(block);
            var local = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                local[i] = CellToLocal(points[i].x, points[i].y) + Vector3.back * 0.012f;
            }
            _linePoints[line] = local;
            line.positionCount = 0;
            return line;
        }

        private void RevealLine(LineRenderer line, float t)
        {
            Vector3[] points = _linePoints[line];
            float f = t * (points.Length - 1);
            int whole = Mathf.FloorToInt(f);
            int count = Mathf.Min(points.Length, whole + 2);
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                line.SetPosition(i, points[Mathf.Min(i, points.Length - 1)]);
            }
            if (whole + 1 < points.Length)
            {
                line.SetPosition(count - 1, Vector3.Lerp(points[whole], points[whole + 1], f - whole));
            }
        }

        private Renderer Quad(Transform parent, string objectName, Vector3 localPosition, Vector2 size, float angle, Color color, float depth = 0f, Vector3? overridePosition = null, bool sizeIsLocal = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = objectName;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (overridePosition ?? localPosition) + Vector3.forward * depth;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _hologramMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _tinted.Add((renderer, color));
            Tint(renderer, color);
            return renderer;
        }

        private void Label(Transform parent, string text, Vector3 localPosition, float scale, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition + Vector3.back * 0.012f;
            var tmp = go.AddComponent<TextMeshPro>();
            if (_font != null)
            {
                tmp.font = _font;
            }
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = _cellSize * 2.4f * scale;
            tmp.rectTransform.sizeDelta = new Vector2(_cellSize * 1.4f, _cellSize * 1.2f);
            tmp.color = color;
        }

        private void SetStatus(string text)
        {
            if (_statusText != null)
            {
                _statusText.text = text;
            }
        }

        private void SetBaseColor(Renderer renderer, Color color)
        {
            for (int i = 0; i < _tinted.Count; i++)
            {
                if (_tinted[i].renderer == renderer)
                {
                    _tinted[i] = (renderer, color);
                }
            }
            Tint(renderer, color);
        }

        private void Tint(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        private Color? _boardOverride;

        private void SetBoardColor(Color? color)
        {
            _boardOverride = color;
        }

        private void Flicker()
        {
            // 홀로그램 느낌: 밝기가 아주 조금씩 흔들리고, 가끔 짧게 깜빡인다.
            float noise = Mathf.PerlinNoise(Time.time * 3f, _flickerSeed);
            float glitch = Mathf.PerlinNoise(Time.time * 17f, _flickerSeed + 3f) > 0.82f ? 0.55f : 1f;
            float k = (0.82f + noise * 0.25f) * glitch;
            for (int i = _tinted.Count - 1; i >= 0; i--)
            {
                (Renderer renderer, Color color) = _tinted[i];
                if (renderer == null)
                {
                    _tinted.RemoveAt(i);
                    continue;
                }
                Color baseColor = _boardOverride.HasValue && !renderer.name.StartsWith("Box") ? new Color(_boardOverride.Value.r, _boardOverride.Value.g, _boardOverride.Value.b, color.a) : color;
                Tint(renderer, new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * k));
            }
            if (_statusText != null)
            {
                _statusText.alpha = Mathf.Clamp01(0.75f + noise * 0.25f) * glitch;
            }
        }

        private void SetCollidersEnabled(bool isEnabled)
        {
            foreach (Collider c in _cellColliders.Keys)
            {
                c.enabled = isEnabled;
            }
            foreach (Collider c in _buttonColliders.Keys)
            {
                c.enabled = isEnabled;
            }
        }
    }
}
