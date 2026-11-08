namespace SoccerClub.Core
{
    public enum Position
    {
        GK,
        CB,
        RSB,
        LSB,
        RWB,
        LWB,
        DM,
        AM,
        RSH,
        LSH,
        RW,
        LW,
        CF
    }

    public enum PositionGroup
    {
        GK,
        DF,
        MF,
        FW
    }

    /// <summary>ポジション適性。試合時は Off/Def にこの倍率を乗算する。</summary>
    public enum Suitability
    {
        None = 0,
        Fair = 50,
        Good = 75,
        Excellent = 100
    }

    public enum Rarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    public enum GrowthPhase
    {
        Youth,
        Prime,
        Veteran
    }

    public enum PlayingStyle
    {
        Balanced,
        Possession,
        Counter,
        HighPress,
        Direct
    }

    /// <summary>監督の得意システム。並びは選手紹介の16種。</summary>
    public enum FormationType
    {
        F541A,
        F541B,
        F541C,
        F532A,
        F532B,
        F451A,
        F451B,
        F442A,
        F442B,
        F442C,
        F433A,
        F433B,
        F352,
        F343A,
        F343B,
        F343C
    }

    public enum FrontRole
    {
        Secretary,
        GeneralManager,
        Strengthening,
        Sales,
        PublicRelations,
        Business,
        Academy
    }

    public enum TeamStaffRole
    {
        Manager,
        HeadCoach,
        Coach,
        PhysicalCoach,
        Trainer,
        Doctor
    }

    /// <summary>GM が持つチーム作りのスタイル。</summary>
    public enum ReinforcementPolicy
    {
        Youth,
        Strength,
        Manager
    }

    /// <summary>監督がスタメンとベンチを選ぶときの性格。未設定のセーブはパラメータ優先。</summary>
    public enum LineupPriority
    {
        Parameter,
        Condition
    }

    public enum BudgetKind
    {
        Reinforcement,
        Advertising,
        Operating
    }
}
