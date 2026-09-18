using System.IO;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.Data.Editor
{
    public sealed class CharacterDataImporter : IDataTableImporter
    {
        public string TableName => "Character_DT";

        void IDataTableImporter.Import(string sourcePath) => Import(sourcePath);

        public const string SourcePath = "Assets/CK_Semester_Project/Prototype/Data/CSV/Character_DT.xlsx";
        public const string OutputPath = "Assets/CK_Semester_Project/Prototype/Data/Resources/Character_DT.asset";

        public static void ImportFromMenu()
        {
            CharacterDatabase database = Import();
            EditorGUIUtility.PingObject(database);
            Debug.Log($"Character_DT: {database.Count}개 캐릭터 가져오기 완료", database);
        }

        /// <summary>검증이 모두 성공한 경우에만 기존 에셋을 갱신하여 참조와 GUID를 유지합니다.</summary>
        public static CharacterDatabase Import() => Import(SourcePath);

        public static CharacterDatabase Import(string sourcePath)
        {
            CharacterData[] characters = CharacterExcelParser.Parse(sourcePath);
            string folder = Path.GetDirectoryName(OutputPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
            }

            CharacterDatabase database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(OutputPath);
            if (database == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(OutputPath) != null)
                    throw new IOException("다른 에셋이 출력 경로를 사용 중입니다: " + OutputPath);
                database = ScriptableObject.CreateInstance<CharacterDatabase>();
                database.SetImportedData(characters);
                AssetDatabase.CreateAsset(database, OutputPath);
            }
            else
            {
                Undo.RecordObject(database, "Import Character DT");
                database.SetImportedData(characters);
                EditorUtility.SetDirty(database);
            }

            AssetDatabase.SaveAssetIfDirty(database);
            return database;
        }
    }
}
