using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CK.SemesterProject.Data.Editor
{
    /// <summary>DT 엑셀 변경을 모아 에셋 임포트가 끝난 다음 변환합니다.</summary>
    [InitializeOnLoad]
    public sealed class DataTableAutoImporter : AssetPostprocessor
    {
        private static readonly HashSet<string> Pending = new(StringComparer.OrdinalIgnoreCase);
        private static bool _scheduled;
        private static bool _startupScanPending;

        static DataTableAutoImporter()
        {
            // Play 진입의 도메인 재로드마다 전체 자산 목록을 다시 검색하지 않는다.
            EditorApplication.delayCall += QueueStartupTables;
        }

        private static void QueueStartupTables()
        {
            string sessionKey = "CK.DataTableStartupScan:" + Application.dataPath;
            if (SessionState.GetBool(sessionKey, false))
            {
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += QueueStartupTables;
                return;
            }
            _startupScanPending = true;
            QueueAll();
            if (Pending.Count == 0)
            {
                SessionState.SetBool(sessionKey, true);
                _startupScanPending = false;
            }
        }

        private static bool IsTable(string path)
            => path.StartsWith("Assets/", StringComparison.Ordinal)
                && !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal)
                && path.EndsWith("_DT.xlsx", StringComparison.OrdinalIgnoreCase);

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            foreach (string path in imported.Concat(moved))
            {
                if (IsTable(path))
                {
                    Pending.Add(path);
                }
                else if (path.Contains("/Scripts/Data/Editor/") && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    // 변환기 변경은 재검사하되 일반 Play 재로드는 건너뛴다.
                    SessionState.SetBool("CK.DataTableStartupScan:" + Application.dataPath, false);
                    EditorApplication.delayCall -= QueueStartupTables;
                    EditorApplication.delayCall += QueueStartupTables;
                }
            }

            foreach (string path in deleted.Concat(movedFrom))
                if (IsTable(path))
                    Debug.LogWarning($"DT 원본이 삭제 또는 이동되었습니다: {path}. 기존 데이터 에셋은 보존됩니다.");

            Schedule();
        }

        public static void QueueAll()
        {
            foreach (string path in AssetDatabase.GetAllAssetPaths())
                if (IsTable(path)) Pending.Add(path);
            Schedule();
        }

        private static void Schedule()
        {
            if (_scheduled || Pending.Count == 0) return;
            _scheduled = true;
            EditorApplication.delayCall += ProcessPending;
        }

        private static void ProcessPending()
        {
            _scheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Schedule();
                return;
            }

            string[] paths = Pending.ToArray();
            Pending.Clear();
            Dictionary<string, IDataTableImporter> importers;
            try { importers = CreateRegistry(); }
            catch (Exception exception) { Debug.LogException(exception); return; }

            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;
                try { Import(path, importers); }
                catch (Exception exception)
                {
                    Debug.LogError($"DT 자동 임포트 실패: {path}\n{exception.Message}\n기존 데이터 에셋은 갱신되지 않았습니다.");
                }
            }
            if (_startupScanPending)
            {
                SessionState.SetBool("CK.DataTableStartupScan:" + Application.dataPath, true);
                _startupScanPending = false;
            }
        }

        private static Dictionary<string, IDataTableImporter> CreateRegistry()
        {
            var result = new Dictionary<string, IDataTableImporter>(StringComparer.OrdinalIgnoreCase);
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IDataTableImporter>())
            {
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters) continue;
                var importer = (IDataTableImporter)Activator.CreateInstance(type);
                if (string.IsNullOrWhiteSpace(importer.TableName) || result.ContainsKey(importer.TableName))
                    throw new InvalidOperationException($"DT 변환기 이름이 비어 있거나 중복되었습니다: {type.FullName}");
                result.Add(importer.TableName, importer);
            }
            return result;
        }

        private static void Import(string path, Dictionary<string, IDataTableImporter> importers)
        {
            string tableName = Path.GetFileNameWithoutExtension(path);
            if (!importers.TryGetValue(tableName, out IDataTableImporter importer))
                throw new InvalidOperationException($"{tableName}용 IDataTableImporter 구현이 필요합니다.");

            // 같은 이름의 서로 다른 원본이 하나의 출력 에셋을 덮어쓰지 않도록 합니다.
            if (AssetDatabase.GetAllAssetPaths().Count(candidate => IsTable(candidate)
                && string.Equals(Path.GetFileNameWithoutExtension(candidate), tableName,
                    StringComparison.OrdinalIgnoreCase)) > 1)
                throw new InvalidDataException($"{tableName}.xlsx 원본이 둘 이상 있습니다.");

            importer.Import(path);
        }

        /// <summary>잘못된 원본 때문에 과거 데이터가 빌드에 들어가는 것을 방지합니다.</summary>
        public static void ImportAllOrThrow()
        {
            Dictionary<string, IDataTableImporter> importers = CreateRegistry();
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(IsTable))
                Import(path, importers);
        }
    }

    public sealed class DataTableBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            try { DataTableAutoImporter.ImportAllOrThrow(); }
            catch (Exception exception) { throw new BuildFailedException("DT 검증 실패: " + exception.Message); }
        }
    }
}
