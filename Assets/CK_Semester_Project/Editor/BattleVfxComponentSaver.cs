using System;
using System.Linq;
using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.EditorTools
{
    public static class BattleVfxComponentSaver
    {
        private const string DummyName = "VFX Training Dummy";

        public static GameObject Save(BattleVfxSceneTemplate component)
        {
            GameObject root = GetEffectRoot(component);
            return SaveAs(component, root != null ? root.name : string.Empty);
        }

        public static GameObject SaveAs(BattleVfxSceneTemplate component, string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("새로 저장할 이펙트 이름을 입력하세요.");
            }
            return SaveInternal(component, displayName, null);
        }

        public static GameObject Overwrite(BattleVfxSceneTemplate component, GameObject savedPrefab)
        {
            if (!BattleVfxSavedEffectLibrary.IsSavedPrefab(savedPrefab))
            {
                throw new ArgumentException("저장 목록에서 생성한 이펙트만 덮어쓸 수 있습니다. 원본 프리팹은 선택할 수 없습니다.");
            }
            return SaveInternal(component, null, savedPrefab);
        }

        private static GameObject SaveInternal(BattleVfxSceneTemplate component,
            string displayName, GameObject overwritePrefab)
        {
            if (component == null)
            {
                throw new ArgumentException("씬에 놓은 이펙트에 이펙트 배치 저장 컴포넌트를 추가하세요.");
            }
            GameObject root = GetEffectRoot(component);
            if (EditorUtility.IsPersistent(root) || !root.scene.IsValid() || !root.scene.isLoaded)
            {
                throw new ArgumentException("Project 에셋이 아닌 씬에 놓은 이펙트에서 저장하세요.");
            }
            if (root.GetComponentInChildren<WeaponEffectSocket>(true) != null)
            {
                throw new ArgumentException("캐릭터나 칼날 소켓이 아닌, 그 아래 배치한 이펙트 프리팹에 저장 컴포넌트를 추가하세요.");
            }
            Transform socket = BattleVfxScenePlacementSaver.FindBladeSocketParent(root.transform);
            Transform dummy = socket != null ? socket : root.scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == DummyName);
            if (dummy == null)
            {
                throw new ArgumentException("현재 씬에 기본 테스트 더미가 없습니다. BattleVfxTest 씬에서 저장하세요.");
            }
            return overwritePrefab != null
                ? BattleVfxScenePlacementSaver.Overwrite(root, dummy, overwritePrefab)
                : BattleVfxScenePlacementSaver.SaveAs(root, dummy, displayName);
        }

        private static GameObject GetEffectRoot(BattleVfxSceneTemplate component)
        {
            if (component == null)
            {
                return null;
            }
            Transform socket = BattleVfxScenePlacementSaver.FindBladeSocketParent(component.transform);
            if (socket != null)
            {
                GameObject nestedRoot = PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject);
                if (nestedRoot != null && nestedRoot.transform.IsChildOf(socket))
                {
                    return nestedRoot;
                }
                Transform effectRoot = component.transform;
                while (effectRoot.parent != null && effectRoot.parent != socket)
                {
                    effectRoot = effectRoot.parent;
                }
                return effectRoot.gameObject;
            }
            GameObject root = PrefabUtility.IsAnyPrefabInstanceRoot(component.gameObject) ? component.gameObject
                : PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject);
            return root != null ? root : component.gameObject;
        }
    }
}
