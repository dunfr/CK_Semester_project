using System;
using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.EditorTools
{
    public static class BattleVfxScenePlacementSaver
    {
        private const string SavedFolder = "Assets/CK_Semester_Project/Prototype/Prefabs/BattleVfxTest/Saved";
        private const string SourcePrefix = "CK.BattleVfxSource:";

        [Serializable]
        private sealed class SourceMetadata
        {
            public string OriginalPrefabGuid;
            public string DisplayName;
            public bool HasPlayback;
            public BattleVfxEffectPlacement Placement;
            public float BasePlaybackSpeed = 1f;
        }

        public static string GetSourceGuid(GameObject effect)
        {
            if (effect == null)
            {
                return string.Empty;
            }
            GameObject asset = EditorUtility.IsPersistent(effect) ? effect
                : PrefabUtility.GetCorrespondingObjectFromSource(effect);
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            SourceMetadata source = ReadMetadata(path);
            if (source != null && Guid.TryParseExact(source.OriginalPrefabGuid, "N", out _))
            {
                return source.OriginalPrefabGuid;
            }
            return AssetDatabase.AssetPathToGUID(path);
        }

        public static bool TryGetSavedPlayback(GameObject prefab, out BattleVfxEffectPlacement placement,
            out float basePlaybackSpeed)
        {
            placement = default;
            basePlaybackSpeed = 1f;
            string path = AssetDatabase.GetAssetPath(prefab);
            SourceMetadata source = ReadMetadata(path);
            if (source == null || !source.HasPlayback)
            {
                return false;
            }
            placement = source.Placement;
            basePlaybackSpeed = source.BasePlaybackSpeed > 0f ? source.BasePlaybackSpeed : 1f;
            return true;
        }

        public static string GetSavedDisplayName(GameObject prefab)
        {
            if (prefab == null)
            {
                return string.Empty;
            }
            string path = AssetDatabase.GetAssetPath(prefab);
            SourceMetadata source = ReadMetadata(path);
            if (source == null)
            {
                return prefab.name;
            }
            if (!string.IsNullOrWhiteSpace(source?.DisplayName))
            {
                return source.DisplayName;
            }
            if (source != null && Guid.TryParseExact(source.OriginalPrefabGuid, "N", out _)
                && source.OriginalPrefabGuid != AssetDatabase.AssetPathToGUID(path))
            {
                string originalPath = AssetDatabase.GUIDToAssetPath(source.OriginalPrefabGuid);
                GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(originalPath);
                if (original != null)
                {
                    return original.name;
                }
            }
            return prefab.name;
        }

        public static GameObject SaveAs(GameObject sceneEffect, Transform target, string displayName)
        {
            Validate(sceneEffect, target);
            string uniqueName = GetUniqueDisplayName(displayName);
            return SaveSnapshot(sceneEffect, target, GetSavePath(), uniqueName);
        }

        public static GameObject Overwrite(GameObject sceneEffect, Transform target, GameObject savedPrefab)
        {
            Validate(sceneEffect, target);
            if (!BattleVfxSavedEffectLibrary.IsSavedPrefab(savedPrefab))
            {
                throw new ArgumentException("저장 목록에서 생성한 이펙트만 덮어쓸 수 있습니다. 원본 프리팹은 선택할 수 없습니다.");
            }
            return SaveSnapshot(sceneEffect, target,
                AssetDatabase.GetAssetPath(savedPrefab), GetSavedDisplayName(savedPrefab));
        }

        private static GameObject SaveSnapshot(GameObject sceneEffect, Transform target, string path, string displayName)
        {
            Transform source = sceneEffect.transform;
            GetPlayback(sceneEffect, out BattleVfxEffectPlacement playback, out float basePlaybackSpeed);
            Transform socket = FindBladeSocketParent(source);
            playback.AttachmentAnchor = socket != null ? BattleVfxEffectAnchor.BladeSocket : BattleVfxEffectAnchor.Target;
            Transform anchor = socket != null ? socket : target;
            playback.Position = socket != null ? socket.InverseTransformPoint(source.position)
                : Quaternion.Inverse(anchor.rotation) * (source.position - anchor.position);
            playback.Rotation = (Quaternion.Inverse(anchor.rotation) * source.rotation).eulerAngles;
            playback.Scale = 1f;
            Vector3 worldScale = source.lossyScale;
            string sourceGuid = GetSourceGuid(sceneEffect);
            if (!AssetDatabase.IsValidFolder(SavedFolder))
            {
                AssetDatabase.CreateFolder("Assets/CK_Semester_Project/Prototype/Prefabs/BattleVfxTest", "Saved");
            }

            GameObject snapshot = UnityEngine.Object.Instantiate(sceneEffect);
            GameObject prefab;
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(snapshot))
                {
                    PrefabUtility.UnpackPrefabInstance(snapshot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
                snapshot.name = displayName;
                snapshot.hideFlags = HideFlags.None;
                snapshot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                snapshot.transform.localScale = worldScale;
                foreach (BattleVfxSceneTemplate template in snapshot.GetComponentsInChildren<BattleVfxSceneTemplate>(true))
                {
                    UnityEngine.Object.DestroyImmediate(template);
                }
                foreach (TutorialSlashVfxPlacement automatic in snapshot.GetComponentsInChildren<TutorialSlashVfxPlacement>(true))
                {
                    automatic.enabled = false;
                }
                foreach (TutorialChargeVfxPlacement automatic in snapshot.GetComponentsInChildren<TutorialChargeVfxPlacement>(true))
                {
                    automatic.enabled = false;
                }
                snapshot.SetActive(true);
                prefab = PrefabUtility.SaveAsPrefabAsset(snapshot, path, out bool success);
                if (!success || prefab == null)
                {
                    throw new InvalidOperationException("이펙트 프리팹을 저장하지 못했습니다: " + path);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
            AssetImporter importer = AssetImporter.GetAtPath(path);
            string metadata = SourcePrefix + JsonUtility.ToJson(new SourceMetadata
            {
                OriginalPrefabGuid = sourceGuid,
                DisplayName = displayName,
                HasPlayback = true,
                Placement = playback,
                BasePlaybackSpeed = basePlaybackSpeed
            });
            string userData = string.Join("\n", importer.userData.Split('\n')
                .Where(line => !line.StartsWith(SourcePrefix, StringComparison.Ordinal) && !string.IsNullOrEmpty(line))
                .Concat(new[] { metadata }));
            if (importer.userData != userData)
            {
                importer.userData = userData;
                importer.SaveAndReimport();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            BattleVfxSceneTemplate sceneTemplate = sceneEffect.GetComponent<BattleVfxSceneTemplate>();
            if (sceneTemplate == null)
            {
                sceneTemplate = Undo.AddComponent<BattleVfxSceneTemplate>(sceneEffect);
            }
            Undo.RecordObject(sceneTemplate, "이펙트 저장 연결 유지");
            sceneTemplate.SetSavedPrefab(prefab);
            PrefabUtility.RecordPrefabInstancePropertyModifications(sceneTemplate);
            return prefab;
        }

        private static string GetSavePath()
        {
            string path;
            do
            {
                path = SavedFolder + "/Vfx_" + Guid.NewGuid().ToString("N") + ".prefab";
            }
            while (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)));
            return path;
        }

        public static Transform FindBladeSocketParent(Transform effect)
        {
            for (Transform parent = effect != null ? effect.parent : null; parent != null; parent = parent.parent)
            {
                if (parent.name == "BladeSocket")
                {
                    return parent;
                }
                WeaponEffectSocket binding = parent.GetComponent<WeaponEffectSocket>();
                if (binding != null && binding.BladeSocket != null && effect != binding.BladeSocket
                    && effect.IsChildOf(binding.BladeSocket))
                {
                    return binding.BladeSocket;
                }
            }
            return null;
        }

        private static string GetUniqueDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("새로 저장할 이펙트 이름을 입력하세요.");
            }
            string requestedName = displayName.Trim();
            if (!AssetDatabase.IsValidFolder(SavedFolder))
            {
                return requestedName;
            }
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SavedFolder }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (BattleVfxSavedEffectLibrary.IsSavedPrefab(prefab))
                {
                    names.Add(GetSavedDisplayName(prefab));
                }
            }
            string uniqueName = requestedName;
            int suffix = 2;
            while (names.Contains(uniqueName))
            {
                uniqueName = requestedName + " (" + suffix + ")";
                suffix++;
            }
            return uniqueName;
        }

        private static void Validate(GameObject sceneEffect, Transform target)
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Play를 종료한 뒤 씬에 배치한 이펙트를 저장하세요.");
            }
            if (sceneEffect == null || EditorUtility.IsPersistent(sceneEffect)
                || !sceneEffect.scene.IsValid() || !sceneEffect.scene.isLoaded)
            {
                throw new ArgumentException("Project 에셋이 아닌, 씬에 배치한 이펙트 루트를 지정하세요.");
            }
            if (target == null || EditorUtility.IsPersistent(target) || target.gameObject.scene != sceneEffect.scene
                || target == sceneEffect.transform || target.IsChildOf(sceneEffect.transform))
            {
                throw new ArgumentException("같은 씬의 기준 더미와 별도의 이펙트 루트를 지정하세요.");
            }
            Transform source = sceneEffect.transform;
            Matrix4x4 matrix = source.localToWorldMatrix;
            Matrix4x4 reconstructed = Matrix4x4.TRS(source.position, source.rotation, source.lossyScale);
            for (int i = 0; i < 12; i++)
            {
                if (Mathf.Abs(matrix[i] - reconstructed[i]) > .001f * Mathf.Max(1f, Mathf.Abs(matrix[i])))
                {
                    throw new ArgumentException("비균일 크기의 부모로 인해 이펙트가 기울어져 있습니다. 이펙트 루트를 Hierarchy 최상위로 옮긴 뒤 저장하세요.");
                }
            }
            ValidateReferences(sceneEffect);
        }

        private static void GetPlayback(GameObject sceneEffect, out BattleVfxEffectPlacement playback,
            out float basePlaybackSpeed)
        {
            BattleVfxSceneTemplate marker = sceneEffect.GetComponent<BattleVfxSceneTemplate>();
            if (marker != null && TryGetSavedPlayback(marker.SavedPrefab, out playback, out basePlaybackSpeed))
            {
                return;
            }
            GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(sceneEffect);
            if (TryGetSavedPlayback(sourcePrefab, out playback, out basePlaybackSpeed))
            {
                return;
            }
            playback = new BattleVfxEffectPlacement { Scale = 1f };
            basePlaybackSpeed = 1f;
        }

        private static SourceMetadata ReadMetadata(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            AssetImporter importer = AssetImporter.GetAtPath(path);
            string metadata = importer != null ? importer.userData.Split('\n')
                .FirstOrDefault(line => line.StartsWith(SourcePrefix, StringComparison.Ordinal)) : null;
            if (metadata == null)
            {
                return null;
            }
            try
            {
                return JsonUtility.FromJson<SourceMetadata>(metadata.Substring(SourcePrefix.Length));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static void ValidateReferences(GameObject sceneEffect)
        {
            foreach (Component component in sceneEffect.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    throw new ArgumentException("이펙트에 Missing Script가 있습니다. Inspector에서 정리한 뒤 저장하세요.");
                }
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference
                        || property.propertyPath == "m_PrefabInstance"
                        || property.propertyPath == "m_CorrespondingSourceObject"
                        || property.propertyPath == "m_PrefabAsset"
                        || (component == sceneEffect.transform && property.propertyPath == "m_Father"))
                    {
                        continue;
                    }
                    UnityEngine.Object reference = property.objectReferenceValue;
                    if (reference == null || EditorUtility.IsPersistent(reference))
                    {
                        continue;
                    }
                    Transform referencedTransform = reference is GameObject gameObject ? gameObject.transform
                        : reference is Component referencedComponent ? referencedComponent.transform : null;
                    if (referencedTransform != null && (referencedTransform == sceneEffect.transform
                        || referencedTransform.IsChildOf(sceneEffect.transform)))
                    {
                        continue;
                    }
                    throw new ArgumentException(component.name + "의 " + property.displayName
                        + "이 이펙트 밖의 씬 오브젝트를 참조합니다. 자식 오브젝트 또는 Project 에셋으로 바꾼 뒤 저장하세요.");
                }
            }
        }
    }
}
