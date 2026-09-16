using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// 씬 전환과 관계없이 UI 프리팹을 열고 닫는 전역 UI 관리자입니다.
/// 명시한 _uiRoot는 해당 씬의 수명을 따르고, 자동 생성한 루트는 매니저와 함께 유지됩니다.
/// Instance에 처음 접근할 때 씬의 매니저를 우선 사용하고, 없으면 생성합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIManager : MonoBehaviour
{
    [Serializable]
    private sealed class UIPrefabEntry
    {
        [Tooltip("Open/Close에서 사용할 키입니다. 비워 두면 프리팹 이름을 사용합니다.")]
        [FormerlySerializedAs("key")]
        [SerializeField] private string _key;

        [FormerlySerializedAs("prefab")]
        [SerializeField] private GameObject _prefab;

        public string Key => _key;
        public GameObject Prefab => _prefab;
    }

    [Header("UI Root")]
    [FormerlySerializedAs("uiRoot")]
    [SerializeField]
    private Transform _uiRoot;

    [Header("Registered UI Prefabs")]
    [FormerlySerializedAs("registeredPrefabs")]
    [SerializeField]
    private List<UIPrefabEntry> _registeredPrefabs = new();

    [FormerlySerializedAs("resourcesFolder")]
    [SerializeField]
    private string _resourcesFolder = "UI";

    private static UIManager _instance;
    private static bool _isQuitting;

    private readonly Dictionary<string, GameObject> _prefabByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameObject> _openedByKey = new(StringComparer.Ordinal);
    private Transform _fallbackRoot;
    private bool _initialized;

    public static UIManager Instance
    {
        get
        {
            if (_isQuitting || !Application.isPlaying)
            {
                return null;
            }

            if (_instance == null)
            {
                UIManager existing = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
                if (existing != null)
                {
                    existing.Initialize();
                }
                else
                {
                    new GameObject(nameof(UIManager)).AddComponent<UIManager>();
                }
            }

            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _isQuitting = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this)
        {
            _instance = null;
            CloseAll();
        }
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    private void Initialize()
    {
        if (_instance != null && _instance != this)
        {
            _instance.AdoptSceneConfiguration(this);
            // 같은 오브젝트의 다른 컴포넌트와 자식 UI는 씬 소유이므로 보존합니다.
            Destroy(this);
            return;
        }

        _instance = this;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        // UIManager가 다른 오브젝트의 자식으로 배치되어도 씬 전환 시 유지되도록 루트로 올립니다.
        if (transform.parent != null)
        {
            transform.SetParent(null, false);
        }

        DontDestroyOnLoad(gameObject);
        MergePrefabRegistry(_registeredPrefabs);
    }

    private void AdoptSceneConfiguration(UIManager sceneManager)
    {
        if (sceneManager._uiRoot != null)
        {
            _uiRoot = sceneManager._uiRoot;
        }

        if (sceneManager._registeredPrefabs != null && sceneManager._registeredPrefabs.Count > 0)
        {
            MergePrefabRegistry(sceneManager._registeredPrefabs);
        }

        _resourcesFolder = sceneManager._resourcesFolder;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 이전 씬의 EventSystem이 제거되어도 영속 UI가 입력을 받을 수 있게 합니다.
        foreach (GameObject opened in _openedByKey.Values)
        {
            if (opened != null && opened.activeInHierarchy)
            {
                EnsureEventSystem();
                break;
            }
        }
    }

    /// <summary>
    /// 인스펙터에 등록한 프리팹과 Resources/UI 폴더의 프리팹을 키로 엽니다.
    /// </summary>
    public GameObject Open(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogError("UIManager.Open: UI 키가 비어 있습니다.");
            return null;
        }

        if (TryGet(key, out GameObject opened))
        {
            return Show(opened);
        }

        if (!TryFindPrefab(key, out GameObject prefab))
        {
            Debug.LogError($"UIManager.Open: '{key}' 프리팹을 찾을 수 없습니다. 인스펙터에 등록하거나 Resources/{_resourcesFolder}에 넣어 주세요.");
            return null;
        }

        return Open(prefab, key);
    }

    public GameObject OpenUI(string key)
    {
        return Open(key);
    }

    /// <summary>
    /// 프리팹 참조를 직접 전달해 UI를 엽니다. 프리팹을 어느 씬의 스크립트에서 호출해도 됩니다.
    /// </summary>
    public GameObject Open(GameObject prefab, string key = null)
    {
        if (prefab == null)
        {
            Debug.LogError("UIManager.Open: 프리팹 참조가 null입니다.");
            return null;
        }

        string resolvedKey = string.IsNullOrWhiteSpace(key) ? prefab.name : key;

        if (TryGet(resolvedKey, out GameObject opened))
        {
            return Show(opened);
        }

        Transform root = ResolveUIRoot();
        GameObject uiInstance = Instantiate(prefab, root, false);
        uiInstance.name = resolvedKey;
        _openedByKey[resolvedKey] = uiInstance;
        return Show(uiInstance);
    }

    /// <summary>
    /// 등록된 UI의 루트 또는 자식에서 원하는 컴포넌트를 반환합니다.
    /// 키를 생략하면 컴포넌트 타입 이름을 사용합니다. 컴포넌트가 없으면 열지 않습니다.
    /// 예: UIManager.Instance.Open&lt;SettingsUI&gt;();
    /// </summary>
    public T Open<T>(string key = null) where T : Component
    {
        string resolvedKey = string.IsNullOrWhiteSpace(key) ? typeof(T).Name : key;
        if (!TryGet(resolvedKey, out GameObject source) && !TryFindPrefab(resolvedKey, out source))
        {
            Debug.LogError($"UIManager.Open: '{resolvedKey}' 프리팹을 찾을 수 없습니다.", this);
            return null;
        }

        if (source.GetComponentInChildren<T>(true) == null)
        {
            Debug.LogError($"UIManager.Open: '{resolvedKey}'에 {typeof(T).Name} 컴포넌트가 없습니다.", this);
            return null;
        }

        GameObject opened = Open(resolvedKey);
        return opened == null ? null : opened.GetComponentInChildren<T>(true);
    }

    public T OpenUI<T>(string key = null) where T : Component
    {
        return Open<T>(key);
    }

    /// <summary>
    /// 키에 해당하는 UI를 닫고 인스턴스를 제거합니다.
    /// </summary>
    public bool Close(string key)
    {
        if (!TryGet(key, out GameObject opened))
        {
            return false;
        }

        _openedByKey.Remove(key);

        if (opened != null)
        {
            opened.SetActive(false);
            Destroy(opened);
        }

        return true;
    }

    public bool CloseUI(string key)
    {
        return Close(key);
    }

    /// <summary>
    /// 현재 열린 모든 UI를 닫습니다.
    /// </summary>
    public void CloseAll()
    {
        // OnDisable에서 다시 매니저를 호출해도 컬렉션 순회가 무효화되지 않게 합니다.
        var snapshot = new List<GameObject>(_openedByKey.Values);
        _openedByKey.Clear();
        foreach (GameObject opened in snapshot)
        {
            if (opened != null)
            {
                opened.SetActive(false);
                Destroy(opened);
            }
        }
    }

    /// <summary>생성된 UI를 조회합니다. 숨겨진 UI도 포함하며 파괴된 참조는 정리합니다.</summary>
    public bool TryGet(string key, out GameObject opened)
    {
        opened = null;
        if (string.IsNullOrWhiteSpace(key) || !_openedByKey.TryGetValue(key, out opened))
        {
            return false;
        }

        if (opened != null)
        {
            return true;
        }

        _openedByKey.Remove(key);
        opened = null;
        return false;
    }

    /// <summary>인스턴스를 보존하며 숨깁니다. 다시 Open하면 기존 인스턴스를 재사용합니다.</summary>
    public bool Hide(string key)
    {
        if (!TryGet(key, out GameObject opened))
        {
            return false;
        }

        opened.SetActive(false);
        return true;
    }

    private static GameObject Show(GameObject opened)
    {
        EnsureEventSystem();
        opened.transform.SetAsLastSibling();
        opened.SetActive(true);
        return opened;
    }

    /// <summary>
    /// 런타임에 프리팹을 등록해야 할 때 사용합니다.
    /// </summary>
    public void Register(string key, GameObject prefab)
    {
        if (string.IsNullOrWhiteSpace(key) || prefab == null)
        {
            Debug.LogError("UIManager.Register: 키와 프리팹을 모두 지정해야 합니다.");
            return;
        }

        if (_prefabByKey.TryGetValue(key, out GameObject existing) && existing != null && existing != prefab)
        {
            Debug.LogError($"UIManager.Register: '{key}'는 다른 프리팹에 이미 등록되어 있습니다.", this);
            return;
        }

        _prefabByKey[key] = prefab;
    }

    private void MergePrefabRegistry(List<UIPrefabEntry> entries)
    {
        if (entries == null)
        {
            return;
        }

        foreach (UIPrefabEntry entry in entries)
        {
            if (entry == null || entry.Prefab == null)
            {
                continue;
            }

            string key = string.IsNullOrWhiteSpace(entry.Key) ? entry.Prefab.name : entry.Key;
            Register(key, entry.Prefab);
        }
    }

    private bool TryFindPrefab(string key, out GameObject prefab)
    {
        if (_prefabByKey.TryGetValue(key, out prefab) && prefab != null)
        {
            return true;
        }

        string folder = _resourcesFolder?.Trim().Trim('/');
        prefab = Resources.Load<GameObject>(string.IsNullOrEmpty(folder) ? key : $"{folder}/{key}");
        if (prefab == null)
        {
            // _resourcesFolder를 비운 경우에도 Resources 루트에서 찾을 수 있게 합니다.
            prefab = Resources.Load<GameObject>(key);
        }

        if (prefab != null)
        {
            _prefabByKey[key] = prefab;
            return true;
        }

        return false;
    }

    private Transform ResolveUIRoot()
    {
        if (_uiRoot != null)
        {
            EnsureEventSystem();
            return _uiRoot;
        }

        if (_fallbackRoot != null)
        {
            return _fallbackRoot;
        }

        // 임의의 씬 Canvas를 선택하지 않고 관리자 전용 영속 루트를 사용합니다.
        GameObject canvasObject = new("UIRoot", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        Canvas newCanvas = canvasObject.AddComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        UnityEngine.UI.CanvasScaler scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        _fallbackRoot = canvasObject.transform;
        EnsureEventSystem();
        return _fallbackRoot;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }
}
