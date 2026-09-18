using UnityEngine;
using UnityEngine.SceneManagement;

public class MainUI : MonoBehaviour
{
    [SerializeField]
    [Tooltip("확정된 게임 시작 씬의 이름 또는 경로. Build Profiles의 Scene List에 등록해야 합니다.")]
    private string _startSceneName = "";

    [SerializeField]
    [Tooltip("설정 팝업 프리팹. 프로젝트 창에서 직접 연결합니다.")]
    private GameObject _settingsPrefab;

    public void OnClickGameStart()
    {
        // TODO: 게임 시작 씬이 확정되면 Inspector에서 연결합니다.
        if (string.IsNullOrWhiteSpace(_startSceneName))
        {
            Debug.LogWarning("MainUI: 게임 시작 씬이 지정되지 않았습니다.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(_startSceneName))
        {
            Debug.LogError($"MainUI: '{_startSceneName}' 씬을 불러올 수 없습니다. Scene List 등록을 확인하세요.", this);
            return;
        }

        SceneManager.LoadScene(_startSceneName);
    }

    public void OnClickGameExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnClickSetting()
    {
        if (_settingsPrefab == null)
        {
            Debug.LogWarning("MainUI: 설정 팝업 프리팹이 지정되지 않았습니다.", this);
            return;
        }

        UIManager manager = UIManager.Instance;
        if (manager != null)
        {
            manager.Open(_settingsPrefab);
        }
    }
}
