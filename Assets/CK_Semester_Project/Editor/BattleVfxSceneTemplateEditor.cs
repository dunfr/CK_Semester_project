using System;
using System.Collections.Generic;
using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CK.SemesterProject.EditorTools
{
    [CustomEditor(typeof(BattleVfxSceneTemplate))]
    public sealed class BattleVfxSceneTemplateEditor : UnityEditor.Editor
    {
        private BattleVfxSavedEffectLibrary.Entry[] _entries;
        private TextField _saveName;
        private DropdownField _overwriteTarget;
        private Button _saveAs;
        private Button _overwrite;
        private Label _status;
        private GameObject _selectedPrefab;

        private void OnEnable()
        {
            EditorApplication.projectChanged += RefreshTargets;
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshTargets;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement { name = "effectPlacementSaveInspector" };
            root.Add(new HelpBox("기본은 새 저장본을 만듭니다. 기존 저장본을 바꾸려면 아래에서 선택하고 덮어쓰기를 누르세요.", HelpBoxMessageType.Info));
            GameObject savedPrefab = GetLinkedPrefab();
            _saveName = new TextField("새 저장 이름")
            {
                name = "newEffectSaveName",
                value = savedPrefab != null ? BattleVfxScenePlacementSaver.GetSavedDisplayName(savedPrefab) : GetEffectRoot().name
            };
            root.Add(_saveName);
            _saveAs = new Button(SaveAs)
            {
                text = "다른 이름으로 저장 (기본)",
                name = "saveEffectAsNew",
                tooltip = "새 이펙트 복사본을 만듭니다. 같은 이름이 있으면 번호를 붙입니다."
            };
            root.Add(_saveAs);
            _overwriteTarget = new DropdownField("덮어쓸 저장본") { name = "effectOverwriteTarget" };
            _overwriteTarget.RegisterValueChangedCallback(change =>
            {
                int index = _overwriteTarget.index;
                if (index >= 0 && index < _entries.Length)
                {
                    _selectedPrefab = _entries[index].Prefab;
                    _overwriteTarget.tooltip = _entries[index].AssetPath;
                }
                UpdateEnabledState();
            });
            root.Add(_overwriteTarget);
            _overwrite = new Button(ConfirmOverwrite)
            {
                text = "선택한 저장본 덮어쓰기…",
                name = "overwriteSavedEffect",
                tooltip = "확인 후 선택한 저장본의 이름과 GUID를 유지하며 갱신합니다."
            };
            root.Add(_overwrite);
            _status = new Label { name = "effectSaveStatus" };
            root.Add(_status);
            _selectedPrefab = savedPrefab;
            RefreshTargets();
            return root;
        }

        private void SaveAs()
        {
            try
            {
                GameObject prefab = BattleVfxComponentSaver.SaveAs((BattleVfxSceneTemplate)target, _saveName.value);
                string name = BattleVfxScenePlacementSaver.GetSavedDisplayName(prefab);
                _selectedPrefab = prefab;
                _status.text = "새 저장본 생성 완료: " + name;
                RefreshTargets();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                EditorUtility.DisplayDialog("이펙트 저장", exception.Message, "확인");
            }
        }

        private void ConfirmOverwrite()
        {
            if (_selectedPrefab == null)
            {
                return;
            }
            GameObject selectedPrefab = _selectedPrefab;
            string name = BattleVfxScenePlacementSaver.GetSavedDisplayName(selectedPrefab);
            bool confirmed = EditorUtility.DisplayDialog("저장본 덮어쓰기 확인",
                "‘" + name + "’ 저장본을 현재 이펙트의 배치와 자식 설정으로 바꿀까요?", "덮어쓰기", "취소");
            if (!confirmed)
            {
                return;
            }
            try
            {
                BattleVfxComponentSaver.Overwrite((BattleVfxSceneTemplate)target, selectedPrefab);
                _status.text = "덮어쓰기 완료: " + name;
                RefreshTargets();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                EditorUtility.DisplayDialog("이펙트 저장", exception.Message, "확인");
            }
        }

        private void RefreshTargets()
        {
            if (_overwriteTarget == null)
            {
                return;
            }
            _entries = BattleVfxSavedEffectLibrary.GetEntries();
            var names = new List<string>();
            foreach (BattleVfxSavedEffectLibrary.Entry entry in _entries)
            {
                string label = entry.DisplayName;
                string unique = label;
                int number = 2;
                while (names.Contains(unique))
                {
                    unique = label + " (" + number++ + ")";
                }
                names.Add(unique);
            }
            _overwriteTarget.choices = names;
            int index = Array.FindIndex(_entries, entry => entry.Prefab == _selectedPrefab);
            if (index < 0 && _entries.Length > 0)
            {
                GameObject linkedPrefab = GetLinkedPrefab();
                index = Array.FindIndex(_entries, entry => entry.Prefab == linkedPrefab);
            }
            _selectedPrefab = index >= 0 ? _entries[index].Prefab : null;
            _overwriteTarget.SetValueWithoutNotify(index >= 0 ? names[index] : string.Empty);
            _overwriteTarget.tooltip = index >= 0 ? _entries[index].AssetPath : "덮어쓸 저장본을 선택하세요.";
            UpdateEnabledState();
        }

        private void UpdateEnabledState()
        {
            bool canSave = !EditorApplication.isPlayingOrWillChangePlaymode;
            _saveName?.SetEnabled(canSave);
            _saveAs?.SetEnabled(canSave);
            _overwriteTarget?.SetEnabled(canSave && _entries.Length > 0);
            _overwrite?.SetEnabled(canSave && _selectedPrefab != null);
        }

        private GameObject GetEffectRoot()
        {
            var component = (BattleVfxSceneTemplate)target;
            GameObject root = PrefabUtility.IsAnyPrefabInstanceRoot(component.gameObject) ? component.gameObject
                : PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject);
            return root != null ? root : component.gameObject;
        }

        private GameObject GetLinkedPrefab()
        {
            GameObject root = GetEffectRoot();
            BattleVfxSceneTemplate component = root.GetComponent<BattleVfxSceneTemplate>();
            GameObject savedPrefab = component != null ? component.SavedPrefab : ((BattleVfxSceneTemplate)target).SavedPrefab;
            return savedPrefab != null ? savedPrefab : PrefabUtility.GetCorrespondingObjectFromSource(root);
        }

        private void PlayModeChanged(PlayModeStateChange state)
        {
            UpdateEnabledState();
        }
    }
}
