using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CK.SemesterProject.EditorTools
{
    public static class BattleVfxSavedEffectLibrary
    {
        private const string SavedFolder = "Assets/CK_Semester_Project/Prototype/Prefabs/BattleVfxTest/Saved";

        public sealed class Entry
        {
            public GameObject Prefab;
            public string AssetPath;
            public string DisplayName;
            public BattleVfxEffectPlacement Placement;
            public float BasePlaybackSpeed = 1f;
        }

        public static Entry[] GetEntries()
        {
            if (!AssetDatabase.IsValidFolder(SavedFolder))
            {
                return Array.Empty<Entry>();
            }
            var entries = new List<Entry>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SavedFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!IsSavedPrefab(prefab, path))
                {
                    continue;
                }
                var entry = new Entry
                {
                    Prefab = prefab,
                    AssetPath = path,
                    DisplayName = BattleVfxScenePlacementSaver.GetSavedDisplayName(prefab),
                    Placement = new BattleVfxEffectPlacement { Scale = 1f }
                };
                if (BattleVfxScenePlacementSaver.TryGetSavedPlayback(prefab, out BattleVfxEffectPlacement savedPlacement,
                    out float savedBaseSpeed))
                {
                    entry.Placement = savedPlacement;
                    entry.BasePlaybackSpeed = savedBaseSpeed;
                }
                entries.Add(entry);
            }
            return entries.OrderBy(entry => entry.DisplayName, StringComparer.Ordinal)
                .ThenBy(entry => entry.AssetPath, StringComparer.Ordinal).ToArray();
        }

        public static bool IsSavedPrefab(GameObject prefab)
        {
            return IsSavedPrefab(prefab, AssetDatabase.GetAssetPath(prefab));
        }

        public static void Delete(GameObject prefab)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Play를 종료한 뒤 저장 이펙트를 삭제하세요.");
            }
            string path = AssetDatabase.GetAssetPath(prefab);
            if (!IsSavedPrefab(prefab, path))
            {
                throw new ArgumentException("저장 목록에서 생성한 이펙트만 삭제할 수 있습니다. 원본 프리팹은 삭제하지 않습니다.");
            }
            // 에셋을 휴지통으로 옮긴 뒤에는 Unity 참조가 null이 되므로 씬의 저장 표시를 먼저 수집한다.
            BattleVfxSceneTemplate[] markers = Resources.FindObjectsOfTypeAll<BattleVfxSceneTemplate>()
                .Where(marker => !EditorUtility.IsPersistent(marker) && marker.gameObject.scene.IsValid()
                    && marker.gameObject.scene.isLoaded && marker.SavedPrefab == prefab).ToArray();
            if (!AssetDatabase.MoveAssetToTrash(path))
            {
                throw new InvalidOperationException("저장 이펙트를 휴지통으로 옮기지 못했습니다: " + path);
            }
            foreach (BattleVfxSceneTemplate marker in markers)
            {
                if (marker == null)
                {
                    continue;
                }
                marker.SetSavedPrefab(null);
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
                EditorUtility.SetDirty(marker);
                if (!EditorSceneManager.IsPreviewScene(marker.gameObject.scene))
                {
                    EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
                }
            }
        }

        private static bool IsSavedPrefab(GameObject prefab, string path)
        {
            if (prefab == null || !EditorUtility.IsPersistent(prefab) || string.IsNullOrEmpty(path)
                || !path.StartsWith(SavedFolder + "/", StringComparison.Ordinal))
            {
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != prefab
                || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
            {
                return false;
            }
            if (BattleVfxScenePlacementSaver.TryGetSavedPlayback(prefab, out _, out _))
            {
                return true;
            }
            // 이전 버전에서 저장한 프리팹도 목록과 삭제 기능을 계속 사용할 수 있다.
            string name = Path.GetFileName(path);
            bool hasSlot = name.EndsWith("_Charge.prefab", StringComparison.Ordinal)
                || Enumerable.Range(1, 3).Any(stage => name.EndsWith("_Stage" + stage + "_Hit.prefab", StringComparison.Ordinal));
            return name.Length > 37 && name.StartsWith("Vfx_", StringComparison.Ordinal) && name[36] == '_'
                && Guid.TryParseExact(name.Substring(4, 32), "N", out _) && hasSlot;
        }
    }
}
