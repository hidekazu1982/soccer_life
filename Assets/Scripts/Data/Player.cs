using System;
using System.Collections.Generic;
using System.Numerics;
using SoccerClub.Core;

namespace SoccerClub.Data
{
    [Serializable]
    public sealed class Player
    {
        public string Id;
        public string Name;

        /// <summary>性別。id から 5:5 で決まる（PlayerTraits.GenderForId）。</summary>
        public Gender Gender;

        public int Age;
        public GrowthPhase Phase;
        public Rarity Rarity;
        public PlayingStyle Style;
        public Position PreferredPosition;

        /// <summary>基礎能力。最大値は実質無制限。</summary>
        public double Offense;
        public double Defense;
        public double StaminaMax;
        public double StaminaCurrent;

        /// <summary>0〜100。練習と試合で減り、日曜とトレーナーが戻す。</summary>
        public int Condition = 80;

        /// <summary>この値以下は練習を休み、画面外で1日休養する。</summary>
        public const int ForcedRestCondition = 19;

        /// <summary>リーグLV1の強制休養。リーグが上がると ForcedRestPerLeague ずつ増える。</summary>
        public const int ForcedRestRecovery = 30;

        public const int ForcedRestPerLeague = 2;

        /// <summary>リーグLV1の練習疲労。リーグが上がると 1 ずつ増える。</summary>
        public const int TrainingConditionCost = 2;

        public const int TrainingFatiguePerLeague = 1;

        /// <summary>リーグLV1の日曜の回復。リーグが上がると 1 ずつ増える。</summary>
        public const int SundayConditionRecovery = 8;

        public const int SundayRecoveryPerLeague = 1;

        /// <summary>リーグLV1の、試合のない休日の回復。</summary>
        public const int DayOffConditionRecovery = 4;

        public const int DayOffRecoveryPerLeague = 1;

        /// <summary>リーグLV1の出場疲労。リーグが上がると 2 ずつ増える。</summary>
        public const int MatchPlayedConditionCost = 6;

        public const int MatchFatiguePerLeague = 2;

        /// <summary>リーグLV1のベンチ疲労。リーグが上がると 1 ずつ増える。</summary>
        public const int MatchBenchConditionCost = 2;

        public const int BenchFatiguePerLeague = 1;

        /// <summary>調子アイコンと同じ 20 点刻み。80 以上が 100%、以後 10% ずつ下がり 60% まで。</summary>
        public int ConditionGainPercent()
        {
            if (Condition >= 80) return 100;
            if (Condition >= 60) return 90;
            if (Condition >= 40) return 80;
            if (Condition >= 20) return 70;
            return 60;
        }

        /// <summary>残りの怪我日数。0 なら出場できる。</summary>
        public int InjuryDays;

        /// <summary>直前の移籍処理以降に出場した試合数。</summary>
        public int Appearances;

        /// <summary>スタメンまたは途中出場した日。未出場は default。</summary>
        public DateTime LastPlayedDate;

        /// <summary>直前の試合で得点した日。演出用で、能力には使わない。</summary>
        public DateTime LastGoalDate;

        /// <summary>今シーズン、試合でこの選手に付いた得点。</summary>
        public int SeasonGoals;

        /// <summary>今シーズンに出場した試合の評価点の合計。</summary>
        public double SeasonRatingSum;

        /// <summary>今シーズンに評価を付けた試合数。</summary>
        public int SeasonRatedMatches;

        /// <summary>夫婦の関係があるとき true。RelationshipService が毎月揃える。試合やお金には影響しない。</summary>
        public bool Married;

        public int Children;

        public Suitability[] Suitabilities = new Suitability[13];

        public double Overall => Offense + Defense + StaminaMax;

        /// <summary>画面表示用。内部値を四捨五入して整数にする。</summary>
        public int DisplayOffense => ToDisplay(Offense);

        public int DisplayDefense => ToDisplay(Defense);

        public int DisplayStaminaMax => ToDisplay(StaminaMax);

        public int DisplayStaminaCurrent => ToDisplay(StaminaCurrent);

        public int DisplayOverall => ToDisplay(Overall);

        public static int ToDisplay(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            if (value >= int.MaxValue) return int.MaxValue;
            if (value <= int.MinValue) return int.MinValue;
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>契約年俸。8月1日の更新時だけ見直す。0 は未契約（無所属など）で、実力の年俸を使う。</summary>
        public BigInteger ContractSalary;

        /// <summary>実際に払う年俸。契約があれば契約年俸。</summary>
        public BigInteger AnnualSalary => ClubMath.ContractAnnual(this);

        public BigInteger MonthlySalary => AnnualSalary / 12;

        public Suitability SuitabilityAt(Position position) => Suitabilities[(int)position];

        public void SetSuitability(Position position, Suitability suitability)
        {
            Suitabilities[(int)position] = suitability;
        }

        /// <summary>配置ポジションの適性 × スタミナ残量を乗算した実効攻撃力。</summary>
        public double EffectiveOffense(Position slot)
        {
            return Offense * PositionUtility.Multiplier(SuitabilityAt(slot)) * StaminaRatio();
        }

        public double EffectiveDefense(Position slot)
        {
            return Defense * PositionUtility.Multiplier(SuitabilityAt(slot)) * StaminaRatio();
        }

        public double StaminaRatio()
        {
            if (StaminaMax <= 0d) return 0d;
            return Math.Max(0d, StaminaCurrent / StaminaMax);
        }

        public void ResetMatchStamina() => StaminaCurrent = StaminaMax;

        public void ApplyDailyGrowth(double offenseDelta, double defenseDelta, double staminaDelta)
        {
            Offense = Math.Max(1d, Offense + offenseDelta);
            Defense = Math.Max(1d, Defense + defenseDelta);
            StaminaMax = Math.Max(1d, StaminaMax + staminaDelta);
            StaminaCurrent = Math.Min(StaminaMax, Math.Max(0d, StaminaCurrent + staminaDelta));
        }

        public void AddCondition(int delta)
        {
            int next = Condition + delta;
            if (next < 0) next = 0;
            if (next > 100) next = 100;
            Condition = next;
        }

        /// <summary>適性が最も高いポジション。同点のときは得意ポジションを残す。</summary>
        public Position BestPosition()
        {
            var best = PreferredPosition;
            if (Suitabilities == null) return best;
            int bestScore = (int)SuitabilityAt(best);
            int count = Math.Min(13, Suitabilities.Length);
            for (int i = 0; i < count; i++)
            {
                int score = (int)Suitabilities[i];
                if (score <= bestScore) continue;
                bestScore = score;
                best = (Position)i;
            }

            return best;
        }
    }

    public sealed class PlayerComparer : IComparer<Player>
    {
        public int Compare(Player x, Player y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return 1;
            if (y == null) return -1;
            return y.Overall.CompareTo(x.Overall);
        }
    }
}
