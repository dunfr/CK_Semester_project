namespace CK.SemesterProject.Data.Editor
{
    /// <summary>
    /// DT별 변환기입니다. 매개변수 없는 생성자를 가진 구현 클래스를 추가하면 자동 등록됩니다.
    /// 모든 데이터 검증을 마친 후 에셋을 갱신하고 기존 GUID를 유지해야 합니다.
    /// </summary>
    public interface IDataTableImporter
    {
        /// <summary>확장자를 제외한 파일명입니다. 예: Character_DT, Skill_DT.</summary>
        string TableName { get; }
        void Import(string sourcePath);
    }
}
