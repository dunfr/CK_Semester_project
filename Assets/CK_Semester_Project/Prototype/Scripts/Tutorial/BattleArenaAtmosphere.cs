using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    // 전투 장소(DummyBattleBackground)에 들어가 있는 동안만 이펙트 작업 씬과 같은 분위기(안개·배경색·주변광)로 바꾸고,
    // 필드 전용 조명은 끈다. 전투가 끝나면 원래 값으로 되돌린다.
    public sealed class BattleArenaAtmosphere : MonoBehaviour
    {
        [SerializeField] private TutorialBattleController _battle;
        [SerializeField] private Camera _camera;
        [SerializeField, Tooltip("전투 중 끌 필드 조명(필드 Directional Light 등)")]
        private Light[] _fieldLights = new Light[0];
        [SerializeField] private Color _fogColor = new Color(0.354f, 0.358f, 0.459f, 1f);
        [SerializeField, Min(0f)] private float _fogDensity = 0.045f;
        [SerializeField] private Color _ambientColor = new Color(0.25f, 0.296f, 0.384f, 1f);

        private bool _isApplied;
        private bool _fog;
        private Color _savedFogColor;
        private FogMode _fogMode;
        private float _savedFogDensity;
        private UnityEngine.Rendering.AmbientMode _ambientMode;
        private Color _ambientLight;
        private CameraClearFlags _clearFlags;
        private Color _background;
        private bool[] _lightStates = new bool[0];

        private void LateUpdate()
        {
            bool inBattle = _battle != null && _battle.IsInBattle;
            if (inBattle != _isApplied)
            {
                if (inBattle)
                {
                    Apply();
                }
                else
                {
                    Restore();
                }
            }
        }

        private void OnDisable()
        {
            if (_isApplied)
            {
                Restore();
            }
        }

        private void Apply()
        {
            _isApplied = true;
            _fog = RenderSettings.fog;
            _savedFogColor = RenderSettings.fogColor;
            _fogMode = RenderSettings.fogMode;
            _savedFogDensity = RenderSettings.fogDensity;
            _ambientMode = RenderSettings.ambientMode;
            _ambientLight = RenderSettings.ambientLight;
            RenderSettings.fog = true;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = _ambientColor;
            if (_camera != null)
            {
                _clearFlags = _camera.clearFlags;
                _background = _camera.backgroundColor;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = _fogColor;
            }
            _lightStates = new bool[_fieldLights.Length];
            for (int i = 0; i < _fieldLights.Length; i++)
            {
                if (_fieldLights[i] != null)
                {
                    _lightStates[i] = _fieldLights[i].enabled;
                    _fieldLights[i].enabled = false;
                }
            }
        }

        private void Restore()
        {
            _isApplied = false;
            RenderSettings.fog = _fog;
            RenderSettings.fogColor = _savedFogColor;
            RenderSettings.fogMode = _fogMode;
            RenderSettings.fogDensity = _savedFogDensity;
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientLight = _ambientLight;
            if (_camera != null)
            {
                _camera.clearFlags = _clearFlags;
                _camera.backgroundColor = _background;
            }
            for (int i = 0; i < _fieldLights.Length && i < _lightStates.Length; i++)
            {
                if (_fieldLights[i] != null)
                {
                    _fieldLights[i].enabled = _lightStates[i];
                }
            }
        }
    }
}
