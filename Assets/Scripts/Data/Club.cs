using System;
using System.Collections.Generic;
using System.Numerics;
using SoccerClub.Core;

namespace SoccerClub.Data
{
    [Serializable]
    public sealed class Club
    {
        public string Id;
        public string Name;
        public bool IsPlayerOwned;

        /// <summary>1〜20。Resources/emblem/emblem_XX と名鑑の順に対応する。</summary>
        public int EmblemIndex;

        /// <summary>背番号の文字色。true なら白、false なら黒。</summary>
        public bool NumberTextIsWhite;

        public BigInteger Coins;

        /// <summary>選手とチームスタッフの年俸、移籍金。総資金とは別の財布。</summary>
        public BigInteger Reinforcement;

        /// <summary>旧セーブの広告費残高。試合では使わず、読み込み時に総資金へ戻す。</summary>
        public BigInteger Advertising;

        /// <summary>事業部が決めたチケット価格。円。0 なら未決定。</summary>
        public int TicketPrice;

        /// <summary>旧セーブの運営費残高。試合では使わず、読み込み時に総資金へ戻す。</summary>
        public BigInteger Operating;

        public int CoreSupporters = ClubMath.CoreFloor;
        public int LightSupporters = ClubMath.LightFloor;

        public List<Player> Squad = new List<Player>();
        public List<Player> Academy = new List<Player>();
        public List<SponsorContract> Sponsors = new List<SponsorContract>();
        public bool DevelopmentPaid;
        public int MatchesSinceWindow;

        public FrontOffice Front = new FrontOffice();
        public TeamStaff Team = new TeamStaff();
        public ClubFacilities Facilities = new ClubFacilities();

        public int PreviousLeagueRank = 10;
        public int LeaguePoints;
        public int GoalsFor;
        public int GoalsAgainst;
        public int Wins;
        public int Draws;
        public int Losses;

        /// <summary>リーグとカップを通した連敗。勝ちと引き分けで 0 に戻る。</summary>
        public int LosingStreak;

        /// <summary>給料を払いきれなかった月が続いている数。ClubMath.UnpaidMonthsToBankrupt で破産。</summary>
        public int UnpaidMonths;

        public int GoalDifference => GoalsFor - GoalsAgainst;

        public BigInteger BudgetOf(BudgetKind kind)
        {
            switch (kind)
            {
                case BudgetKind.Reinforcement: return Reinforcement;
                case BudgetKind.Advertising: return Advertising;
                default: return Operating;
            }
        }

        public void AddBudget(BudgetKind kind, BigInteger delta)
        {
            switch (kind)
            {
                case BudgetKind.Reinforcement:
                    Reinforcement += delta;
                    break;
                case BudgetKind.Advertising:
                    Advertising += delta;
                    break;
                default:
                    Operating += delta;
                    break;
            }
        }

        public void RecordMatch(int scored, int conceded)
        {
            GoalsFor += scored;
            GoalsAgainst += conceded;
            if (scored > conceded)
            {
                Wins++;
                LeaguePoints += 3;
            }
            else if (scored == conceded)
            {
                Draws++;
                LeaguePoints += 1;
            }
            else
            {
                Losses++;
            }
        }

        static readonly bool[] UsedNumbers = new bool[100];

        /// <summary>
        /// トップチームの背番号を決める。今の番号はそのまま残し、無い人と重なった人（後ろにいる人）だけ空き番号を付ける。
        /// GK は 1、12、21… を優先し、それ以外は 2 から空いている小さい番号を付ける。
        /// </summary>
        public void EnsureShirtNumbers()
        {
            if (Squad == null) return;
            Array.Clear(UsedNumbers, 0, UsedNumbers.Length);
            bool missing = false;
            for (int i = 0; i < Squad.Count; i++)
            {
                var p = Squad[i];
                if (p == null) continue;
                int n = p.ShirtNumber;
                if (n >= 1 && n <= 99 && !UsedNumbers[n])
                    UsedNumbers[n] = true;
                else
                {
                    p.ShirtNumber = 0;
                    missing = true;
                }
            }

            if (!missing) return;
            for (int i = 0; i < Squad.Count; i++)
            {
                var p = Squad[i];
                if (p == null || p.ShirtNumber != 0) continue;
                int n = 0;
                if (PositionUtility.GroupOf(p.BestPosition()) == PositionGroup.GK)
                {
                    if (!UsedNumbers[1]) n = 1;
                    else
                    {
                        for (int k = 12; k <= 99 && n == 0; k += 9)
                            if (!UsedNumbers[k]) n = k;
                    }
                }

                for (int k = 2; k <= 99 && n == 0; k++)
                    if (!UsedNumbers[k]) n = k;
                if (n == 0) continue;
                UsedNumbers[n] = true;
                p.ShirtNumber = n;
            }
        }

        public void ResetSeasonStats()
        {
            LeaguePoints = 0;
            GoalsFor = 0;
            GoalsAgainst = 0;
            Wins = 0;
            Draws = 0;
            Losses = 0;
            LosingStreak = 0;
        }
    }
}
