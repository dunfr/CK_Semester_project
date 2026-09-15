namespace CK.SemesterProject.Battle
{
    public enum BattleTeam
    {
        Player,
        Monster,
    }

    public enum BattleElement
    {
        None,
        Afterimage,
        Imprint,
        Oblivion,
    }

    public enum BattlePhase
    {
        NotStarted,
        AwaitingAction,
        AwaitingPresentation,
        Finished,
    }

    public enum BattleOutcome
    {
        None,
        Victory,
        Defeat,
        Draw,
    }

    public enum BattleEntryCondition
    {
        Normal,
        PlayerInitiated,
        MonsterCollision,
    }

    public enum BattleActionKind
    {
        Skill,
        Wait,
        Defend,
    }

    public enum SkillTarget
    {
        Enemy,
        Ally,
        Self,
    }

    public enum BattleActionError
    {
        None,
        InvalidPhase,
        StaleTurn,
        InvalidActor,
        InvalidAction,
        UnknownSkill,
        InvalidTarget,
        InvalidMemoryInvestment,
        UnsupportedAction
    }
}
