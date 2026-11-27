using System;
using System.Numerics;
using SoccerClub.Data;

namespace SoccerClub.Core
{
    /// <summary>メイン画面でキャラの頭上に出る、拾えるアイコンの種類（版 1.65）。</summary>
    public enum PickupKind
    {
        Money,
        Offense,
        Defense,
        Stamina
    }

    /// <summary>
    /// メイン画面の拾えるアイコン（版 1.65）。お金はリーグの放映権料（月額）の 1 日分（最低 1 万円、万円で丸め）。
    /// OFF・DEF・STA は、その選手の練習 1 回分（基礎 10 × 練習効果）を足す。調子と乱数はかけない。
    /// 1 日に出るのは PickupsPerDay 個まで（日が進まないまま待っても増えない）。
    /// </summary>
    public sealed partial class GameSession
    {
        public const int PickupsPerDay = 3;
        static readonly BigInteger PickupMoneyMinimum = new BigInteger(10000);
        static readonly BigInteger PickupMoneyUnit = new BigInteger(10000);
        const int PickupMoneyDays = 30;

        public BigInteger PickupMoneyAmount()
        {
            var amount = ClubMath.BroadcastMonthly(LeagueStrengthFactor) / PickupMoneyDays;
            amount = amount / PickupMoneyUnit * PickupMoneyUnit;
            return amount < PickupMoneyMinimum ? PickupMoneyMinimum : amount;
        }

        /// <summary>お金を拾う。収支報告では「その他」の収入に入る。</summary>
        public bool CollectPickupMoney(out BigInteger amount)
        {
            amount = BigInteger.Zero;
            if (PlayerClub == null || GameOver) return false;
            amount = PickupMoneyAmount();
            Economy.Apply(PlayerClub, amount, "pickup");
            return true;
        }

        /// <summary>選手の OFF・DEF・STA を練習 1 回分だけ上げる。自クラブの選手でなければ false。</summary>
        public bool CollectPickupStat(string playerId, PickupKind kind, out double gain, out string playerName)
        {
            gain = 0d;
            playerName = null;
            if (PlayerClub == null || GameOver || kind == PickupKind.Money || string.IsNullOrEmpty(playerId)) return false;
            var player = FindSquadPlayer(playerId);
            if (player == null) return false;

            double offenseDefense;
            double stamina;
            ClubMath.PracticeMultiplier(PlayerClub, player, out offenseDefense, out stamina);
            double multiplier = kind == PickupKind.Stamina ? stamina : offenseDefense;
            gain = Math.Round(ClubMath.TrainingBaseAdd * Math.Max(0d, multiplier), 1, MidpointRounding.AwayFromZero);
            if (gain <= 0d) gain = 0.1d;
            if (kind == PickupKind.Offense) player.ApplyDailyGrowth(gain, 0d, 0d);
            else if (kind == PickupKind.Defense) player.ApplyDailyGrowth(0d, gain, 0d);
            else player.ApplyDailyGrowth(0d, 0d, gain);
            playerName = player.Name;
            return true;
        }

        /// <summary>自クラブの選手なら true（拾えるアイコンで能力を出せるかどうか）。</summary>
        public bool IsSquadPlayer(string id)
        {
            return FindSquadPlayer(id) != null;
        }

        Player FindSquadPlayer(string id)
        {
            if (PlayerClub == null || PlayerClub.Squad == null || string.IsNullOrEmpty(id)) return null;
            var squad = PlayerClub.Squad;
            for (int i = 0; i < squad.Count; i++)
            {
                if (squad[i] != null && squad[i].Id == id) return squad[i];
            }

            return null;
        }
    }
}
