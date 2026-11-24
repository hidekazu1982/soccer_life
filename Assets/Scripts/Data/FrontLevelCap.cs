using System;

namespace SoccerClub.Data
{
    /// <summary>
    /// リーグLVによる強化の上限（プレイヤーが強くなりすぎないようにする）。
    /// ・フロント（全部署）とチームスタッフは「リーグLV＋10」まで。これを超えてレベルアップできず、採用候補にも出ない。
    /// ・練習場・トレーニング室・クラブハウスの上限は Facility.LevelCap（リーグLV＋10）。スタジアム（上限Lv200）は対象外。
    /// ・上限を超えている人や施設（古いセーブ・初期値）はそのまま残し、上がらないようにだけする。
    /// </summary>
    public static class FrontLevelCap
    {
        /// <summary>フロントとチームスタッフは「リーグLV＋この値」まで。0 で無制限。シミュレーターから変えられる。</summary>
        public const int DefaultLeagueLevelAllowance = 10;
        public static int LeagueLevelAllowance = DefaultLeagueLevelAllowance;

        /// <summary>採用候補に出るレベルの幅（上限から何Lv下まで）。</summary>
        public const int MarketSpread = 5;

        /// <summary>いまのリーグLVでのフロントとチームスタッフの上限。</summary>
        public static int Max()
        {
            if (LeagueLevelAllowance <= 0) return ClubMath.MaxStaffLevel;
            int cap = Math.Max(1, Facility.CurrentLeagueLevel) + LeagueLevelAllowance;
            return Math.Min(cap, ClubMath.MaxStaffLevel);
        }

        /// <summary>このフロント・チームスタッフが次のレベルへ上がれるか。</summary>
        public static bool CanRaise(StaffMember staff)
        {
            return staff != null && staff.Level < Max() && staff.Level < ClubMath.MaxStaffLevel;
        }

        /// <summary>フロントとチームスタッフの採用候補の上限。クラブハウスの上限とリーグLVの上限の低いほう。</summary>
        public static int MarketCap(Club club)
        {
            int house = club != null && club.Facilities != null ? club.Facilities.MarketLevelCap() : 1;
            return Math.Max(1, Math.Min(house, Max()));
        }

        /// <summary>採用候補の下限。上限の 5 下まで。</summary>
        public static int MarketFloor(Club club)
        {
            return Math.Max(1, MarketCap(club) - MarketSpread);
        }

        /// <summary>このレベルのフロント・チームスタッフを採用できるか。</summary>
        public static bool MarketAllows(Club club, int level)
        {
            if (club == null || club.Facilities == null) return false;
            return level >= MarketFloor(club) && level <= MarketCap(club);
        }

        /// <summary>施設の上限。成長施設はリーグLV＋10、スタジアムはLv200。</summary>
        public static int FacilityCap(Club club, Facility facility)
        {
            return facility != null ? facility.LevelCap : 0;
        }

        /// <summary>施設をもう 1 つ上げられるか。</summary>
        public static bool CanUpgradeFacility(Club club, Facility facility)
        {
            return facility != null && facility.Level < FacilityCap(club, facility);
        }
    }
}
