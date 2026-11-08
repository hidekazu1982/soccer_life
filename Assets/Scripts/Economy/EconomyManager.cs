using System;
using System.Collections.Generic;
using System.Numerics;
using SoccerClub.Data;

namespace SoccerClub.Economy
{
    public readonly struct LedgerEntry
    {
        public readonly DateTime Date;
        public readonly string Reason;
        public readonly BigInteger Amount;

        public LedgerEntry(DateTime date, string reason, BigInteger amount)
        {
            Date = date;
            Reason = reason;
            Amount = amount;
        }
    }

    public readonly struct HomeSettlement
    {
        public readonly BigInteger Gate;
        public readonly BigInteger AdvertisingSpent;
        public readonly BigInteger OperatingSpent;
        public readonly BigInteger OperatingRequired;
        public readonly int Attendance;
        public readonly int Capacity;

        public HomeSettlement(
            BigInteger gate,
            BigInteger advertisingSpent,
            BigInteger operatingSpent,
            BigInteger operatingRequired,
            int attendance,
            int capacity)
        {
            Gate = gate;
            AdvertisingSpent = advertisingSpent;
            OperatingSpent = operatingSpent;
            OperatingRequired = operatingRequired;
            Attendance = attendance;
            Capacity = capacity;
        }
    }

    public readonly struct MatchBonusPaid
    {
        public readonly BigInteger Appearance;
        public readonly BigInteger Win;

        public MatchBonusPaid(BigInteger appearance, BigInteger win)
        {
            Appearance = appearance;
            Win = win;
        }
    }

    public interface IEconomy
    {
        bool Apply(Club club, BigInteger delta, string reason);
        HomeSettlement SettleHomeMatch(Club club);
        void ApplyMonthlyIncome(Club club);
        void PayMonthlyWages(Club club);
        bool TryPayDevelopment(Club club);
        bool TrySpend(Club club, BigInteger cost, string reason);
        bool TrySpendReinforcement(Club club, BigInteger cost, string reason);
        bool IsBankrupt(Club club);
    }

    public sealed class EconomyManager : IEconomy
    {
        public event Action<Club, LedgerEntry> OnLedger;
        public event Action<Club> OnBankrupt;

        public bool Apply(Club club, BigInteger delta, string reason)
        {
            club.Coins += delta;
            OnLedger?.Invoke(club, new LedgerEntry(DateTime.MinValue, reason, delta));
            if (IsBankrupt(club))
            {
                OnBankrupt?.Invoke(club);
                return false;
            }

            return true;
        }

        public bool IsBankrupt(Club club) => club.Coins < 0;

        /// <summary>
        /// ホームの入場料を総資金へ入れ、広報部のレベルに応じた広告費をその試合で払う。
        /// 運営費は必要額を総資金から自動で引く。足りないときは残額だけ払い、不足は呼び出し側が扱う。
        /// </summary>
        public HomeSettlement SettleHomeMatch(Club club)
        {
            var advertising = ClubMath.MatchAdvertising(club);
            if (advertising > club.Coins) advertising = club.Coins > 0 ? club.Coins : BigInteger.Zero;
            var required = ClubMath.OperatingRequired(club);
            int attendance = ClubMath.Attendance(club, advertising);
            int capacity = ClubMath.Capacity(club);
            var gate = ClubMath.GateIncome(club, advertising);
            club.Operating = BigInteger.Zero;
            if (advertising > 0)
                Apply(club, -advertising, "advertising");
            if (gate > 0) Apply(club, gate, "gate");

            var paid = required < 0 ? BigInteger.Zero : required;
            if (club.Coins <= 0) paid = BigInteger.Zero;
            else if (club.Coins < paid) paid = club.Coins;
            if (paid > 0) Apply(club, -paid, "operating");
            return new HomeSettlement(gate, advertising, paid, required, attendance, capacity);
        }

        /// <summary>
        /// 出場した選手へ出場給を払い、勝っていれば同じ選手へ勝利給も払う。どちらも年俸の 1%。
        /// 総資金の範囲で払い、負にはしない。
        /// </summary>
        public MatchBonusPaid PayMatchBonuses(Club club, IReadOnlyList<Player> appeared, bool won)
        {
            if (club == null || appeared == null) return new MatchBonusPaid(BigInteger.Zero, BigInteger.Zero);
            var seen = new HashSet<Player>();
            var appearance = BigInteger.Zero;
            var win = BigInteger.Zero;
            for (int i = 0; i < appeared.Count; i++)
            {
                var player = appeared[i];
                if (player == null || !seen.Add(player)) continue;
                var bonus = ClubMath.MatchBonus(player);
                appearance += PayUpTo(club, bonus, "appearance");
                if (won) win += PayUpTo(club, bonus, "win");
            }

            return new MatchBonusPaid(appearance, win);
        }

        BigInteger PayUpTo(Club club, BigInteger amount, string reason)
        {
            if (amount <= 0 || club.Coins <= 0) return BigInteger.Zero;
            if (amount > club.Coins) amount = club.Coins;
            Apply(club, -amount, reason);
            return amount;
        }

        public void ApplyMonthlyIncome(Club club)
        {
            var sponsor = ClubMath.SponsorIncome(club);
            var goods = ClubMath.GoodsIncome(club);
            if (sponsor > 0) Apply(club, sponsor, "sponsor");
            PaySponsorContracts(club);
            if (goods > 0) Apply(club, goods, "goods");
        }

        /// <summary>選手とチームスタッフの月給。フロントの年俸は毎月引かない。</summary>
        public void PayMonthlyWages(Club club)
        {
            if (club == null || club.Coins <= 0) return;
            PayTeamWages(club);
            var squad = club.Squad;
            if (squad == null) return;
            for (int i = 0; i < squad.Count; i++)
            {
                var player = squad[i];
                if (player == null) continue;
                PayWage(club, ClubMath.PlayerMonthly(player), false);
            }
        }

        void PayTeamWages(Club club)
        {
            var team = club.Team;
            if (team == null) return;
            PayStaffWage(club, team.Manager, false);
            PayStaffWage(club, team.HeadCoach, false);
            PayStaffWage(club, team.PhysicalCoach, false);
            PayStaffWage(club, team.Doctor, false);
            PayStaffWages(club, team.Coaches);
            PayStaffWages(club, team.Trainers);
        }

        void PayStaffWages(Club club, StaffMember[] slots)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
                PayStaffWage(club, slots[i], false);
        }

        void PayStaffWage(Club club, StaffMember staff, bool front)
        {
            if (staff == null) return;
            PayWage(club, staff.AnnualSalary / 12, front);
        }

        void PayWage(Club club, BigInteger amount, bool front)
        {
            if (amount <= 0 || club.Coins <= 0) return;
            if (amount > club.Coins) amount = club.Coins;
            Apply(club, -amount, front ? "front-wage" : "squad-wage");
        }

        void PaySponsorContracts(Club club)
        {
            var sponsors = club.Sponsors;
            if (sponsors == null) return;
            for (int i = sponsors.Count - 1; i >= 0; i--)
            {
                var contract = sponsors[i];
                if (contract == null || contract.MonthsLeft <= 0)
                {
                    sponsors.RemoveAt(i);
                    continue;
                }

                if (contract.Monthly > 0) Apply(club, contract.Monthly, "sponsor");
                contract.MonthsLeft--;
                if (contract.MonthsLeft <= 0) sponsors.RemoveAt(i);
            }
        }

        public bool TryPayDevelopment(Club club)
        {
            club.DevelopmentPaid = false;
            var cost = ClubMath.DevelopmentFee(club);
            if (cost <= 0) return false;
            if (!TrySpend(club, cost, "development")) return false;
            club.DevelopmentPaid = true;
            return true;
        }

        public bool TrySpend(Club club, BigInteger cost, string reason)
        {
            if (cost < 0 || club.Coins < cost) return false;
            return Apply(club, -cost, reason);
        }

        public bool TrySpendReinforcement(Club club, BigInteger cost, string reason)
        {
            if (cost < 0 || club.Reinforcement < cost) return false;
            club.Reinforcement -= cost;
            OnLedger?.Invoke(club, new LedgerEntry(DateTime.MinValue, reason, -cost));
            return true;
        }

        public void CreditReinforcement(Club club, BigInteger amount, string reason)
        {
            if (club == null || amount <= 0) return;
            club.Reinforcement += amount;
            OnLedger?.Invoke(club, new LedgerEntry(DateTime.MinValue, reason, amount));
        }
    }
}
