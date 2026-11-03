using UnityEngine;

namespace CK.SemesterProject.Rendering
{
    // 여러 구역이나 보스 연출에서 다시 쓰는 캐릭터 조명 값 묶음.
    [CreateAssetMenu(menuName = "CK/Rendering/Character Light Profile", fileName = "CharacterLightProfile")]
    public sealed class CharacterLightProfile : ScriptableObject
    {
        [SerializeField]
        private CharacterLightSettings _settings = CharacterLightSettings.Default;

        public CharacterLightSettings Settings => _settings;

        private void Reset()
        {
            _settings = CharacterLightSettings.Default;
        }
    }
}
