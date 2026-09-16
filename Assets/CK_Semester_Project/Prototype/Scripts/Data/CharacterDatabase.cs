using System;
using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.Data
{
    /// <summary>엑셀에서 가져온 캐릭터 원본 데이터입니다. 플레이 중 상태는 별도로 보관합니다.</summary>
    public sealed class CharacterDatabase : ScriptableObject
    {
        [SerializeField] private CharacterData[] _characters = Array.Empty<CharacterData>();
        private Dictionary<string, CharacterData> _byId;

        public IReadOnlyList<CharacterData> Characters => Array.AsReadOnly(_characters);
        public int Count => _characters.Length;

        public bool TryGet(string id, out CharacterData data)
        {
            data = null;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (_byId == null)
            {
                _byId = new Dictionary<string, CharacterData>(StringComparer.Ordinal);
                foreach (CharacterData character in _characters)
                {
                    _byId.Add(character.Id, character);
                }
            }

            return _byId.TryGetValue(id, out data);
        }

        private void OnEnable() => _byId = null;
        private void OnValidate() => _byId = null;

#if UNITY_EDITOR
        public void SetImportedData(CharacterData[] characters)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterData character in characters)
            {
                if (character == null || string.IsNullOrWhiteSpace(character.Id) || !ids.Add(character.Id))
                {
                    throw new ArgumentException("캐릭터 ID가 비어 있거나 중복되었습니다.", nameof(characters));
                }
            }

            _characters = (CharacterData[])characters.Clone();
            _byId = null;
        }
#endif
    }
}
