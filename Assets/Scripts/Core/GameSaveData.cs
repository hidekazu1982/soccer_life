using System;

namespace SoccerClub.Core
{
    [Serializable]
    public sealed class GameSaveFile
    {
        public const int CurrentVersion = 1;

        public int Version;
        public string RandomState;
        public long DateTicks;
        public bool GameOver;
        public int LeagueLevel;
        public int DroughtSeasons;
        public int TitleStreak;
        public string PlayerClubId;
        public ClubSave[] Clubs;
        public StaffSave[] FrontMarket;
        public FixtureSave[] LeagueFixtures;
        public FixtureSave[] CupFixtures;
        public CupTieSave[] CupTies;
        public bool HasRound2Seeds;
        public string[] Round2SeedIds;
        public long Round2DateTicks;
        public string[] Round1WinnerIds;
        public LaterRoundSave[] LaterRounds;
        public PendingCupTieSave[] PendingCupTies;
        public MonthLedgerSave MonthLedger;
        public MonthLedgerSave PendingMonthReport;
        public SponsorOfferSave PendingSponsor;
        public SponsorCutSave PendingSponsorCut;
        public string PersonnelWindowKey;
        public long WindowStartTicks;
        public long WindowEndTicks;
        public string BudgetWindowKey;
        public string AnnouncedWinterKey;
        public int GreetedYear;
        public string TicketKey;
        public string CampKey;
        public string RenewalKey;
        public WindowDealSave[] Deals;
        public ClubStorySave Story;
        public PlayerSave[] FreeAgents;
        public int FreeAgentSerial;
        public int StaffOfferMonth;
        public int StaffOfferDay;
        public int StaffOfferRole;
        public int StaffOfferLevel;
        public string StaffOfferName;
        public bool StaffOfferDone;
        // サポートキャラ。今日対応済みの日付と、キャンセルで出さない内容（期限つき）。
        public long SupportDoneTicks;
        public string[] SupportSnoozeKeys;
        public long[] SupportSnoozeUntil;
        // 選手同士の関係（友達・恋人・夫婦）。古いセーブでは null。
        public RelationshipSave[] Relationships;
    }

    [Serializable]
    public sealed class RelationshipSave
    {
        public string PlayerIdA;
        public string PlayerIdB;
        public int Stage;
        public int Affinity;
        public long SinceTicks;
    }

    [Serializable]
    public sealed class WindowDealSave
    {
        public long DateTicks;
        public bool Incoming;
        public string PlayerId;
        public PlayerSave Player;
    }

    [Serializable]
    public sealed class ClubStorySave
    {
        public int Kind;
        public int Step;
        public int StepCount;
        public int ApplyStep;
        public bool Applied;
        public bool Budget;
        public int BudgetPercent;
        public bool Leaves;
        public bool Signs;
        public bool Negotiates;
        public string WindowKey;
        public string PlayerId;
        public string Fee;
        public string Retain;
        public string Salary;
        public string Title0;
        public string Title1;
        public string Title2;
        public string Title3;
        public string Page0;
        public string Page1;
        public string Page2;
        public string Page3;
        public PlayerSave Incoming;
    }

    [Serializable]
    public sealed class MonthLedgerSave
    {
        public int Year;
        public int Month;
        public string Sponsor;
        public string Goods;
        public string Gate;
        public string Broadcast;
        public string Prize;
        public string Operating;
        public string Advertising;
        public string Development;
        public string Facilities;
        public string FrontWages;
        public string SquadWages;
        public string Reinforcement;
        public string Coins;
    }

    [Serializable]
    public sealed class ClubSave
    {
        public string Id;
        public string Name;
        public int EmblemIndex;
        public bool NumberTextIsWhite;
        public string Coins;
        public string Reinforcement;
        public string Advertising;
        public int TicketPrice;
        public string Operating;
        public int CoreSupporters;
        public int LightSupporters;
        public bool DevelopmentPaid;
        public int MatchesSinceWindow;
        public int PreviousLeagueRank;
        public int LeaguePoints;
        public int GoalsFor;
        public int GoalsAgainst;
        public int Wins;
        public int Draws;
        public int Losses;
        public int LosingStreak;
        public int UnpaidMonths;
        public SponsorSave[] Sponsors;
        public PlayerSave[] Squad;
        public PlayerSave[] Academy;
        public StaffSave Secretary;
        public StaffSave GeneralManager;
        public StaffSave[] Strengthening;
        public StaffSave[] Sales;
        public StaffSave[] PublicRelations;
        public StaffSave[] Business;
        public StaffSave[] AcademyStaff;
        public StaffSave Manager;
        public StaffSave HeadCoach;
        public StaffSave[] Coaches;
        public StaffSave PhysicalCoach;
        public StaffSave[] Trainers;
        public StaffSave Doctor;
        public FacilitySave Stadium;
        public FacilitySave Transport;
        public FacilitySave TrainingGround;
        public FacilitySave TrainingFacility;
        public FacilitySave Clubhouse;
    }

    [Serializable]
    public sealed class SponsorSave
    {
        public string Name;
        public string Monthly;
        public int MonthsLeft;
        public int CutRank;
        public int CutLosses;
    }

    [Serializable]
    public sealed class SponsorCutLineSave
    {
        public string Name;
        public int CutRank;
        public int CutLosses;
    }

    [Serializable]
    public sealed class SponsorCutSave
    {
        public SponsorCutLineSave[] Lines;
    }

    [Serializable]
    public sealed class SponsorOfferSave
    {
        public string SponsorName;
        public string StaffName;
        public string Monthly;
        public string Upfront;
        public int Months;
        public int ActiveContracts;
        public int CutRank;
        public int CutLosses;
    }

    [Serializable]
    public sealed class PlayerSave
    {
        public string Id;
        public string Name;
        public int Age;
        public int Phase;
        public int Rarity;
        public int Style;
        public int PreferredPosition;
        public string Offense;
        public string Defense;
        public string StaminaMax;
        public string StaminaCurrent;
        public int Condition;
        public int InjuryDays;
        public int Appearances;
        public int SeasonGoals;
        public string SeasonRatingSum;
        public int SeasonRatedMatches;
        public long LastPlayedTicks;
        public long LastGoalTicks;
        public bool Married;
        public int Children;
        public int[] Suitabilities;
        public string ContractSalary;
        // 0 = 未設定（古いセーブ。id から決める）、1 = 男性、2 = 女性。
        public int Gender;
    }

    [Serializable]
    public sealed class StaffSave
    {
        public bool Has;
        public string Id;
        public string Name;
        public int Age;
        public int Level;
        public bool IsFrontOffice;
        public int FrontRole;
        public int TeamRole;
        public int Policy;
        public int FavoriteFormation;
        public int FavoriteStyle;
        public int LineupPriority;
        public int ScoutedGroup;
        public bool Married;
        public int Children;
        public long JoinedTicks;
    }

    [Serializable]
    public sealed class FacilitySave
    {
        public int Level;
        public string BaseCost;
    }

    [Serializable]
    public sealed class FixtureSave
    {
        public string Id;
        public long DateTicks;
        public string HomeId;
        public string AwayId;
        public bool IsLeague;
        public bool IsCup;
        public int CupRound;
        public int Leg;
        public bool HasResult;
        public int HomeGoals;
        public int AwayGoals;
        public bool DecidedByPenalty;
        public bool ConsequencesApplied;
        public string GateIncome;
        public string AdvertisingSpent;
        public string OperatingPaid;
        public string OperatingRequired;
        public string HomeAppearancePaid;
        public string HomeWinPaid;
        public string AwayAppearancePaid;
        public string AwayWinPaid;
        public int Attendance;
        public int StadiumCapacity;
    }

    [Serializable]
    public sealed class CupTieSave
    {
        public string Id;
        public string FirstHomeId;
        public string FirstAwayId;
        public int FirstHomeGoals;
        public int FirstAwayGoals;
        public int SecondHomeGoals;
        public int SecondAwayGoals;
        public bool Complete;
    }

    [Serializable]
    public sealed class LaterRoundSave
    {
        public int Round;
        public string[] ClubIds;
    }

    [Serializable]
    public sealed class PendingCupTieSave
    {
        public string TieId;
        public int Round;
        public long FirstLegTicks;
        public string HomeId;
        public string HomeFeeder;
        public string AwayId;
        public string AwayFeeder;
    }
}
