namespace CK.SemesterProject.SkillSequence
{
    /// <summary>이펙트 생성 위치의 기준점. 몬스터 크기·위치가 달라도 같은 부위에 붙도록 의미 단위로 고른다.</summary>
    public enum SkillVfxAnchorPoint
    {
        CasterRoot,
        CasterBone,
        WeaponSocket,
        TargetCenter,
        TargetFeet,
        TargetGroupCenter,
        StageCenter
    }
}
