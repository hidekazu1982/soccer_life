using System;
using System.Collections.Generic;
using System.Numerics;
using SoccerClub.Data;
using SoccerClub.Personnel;

namespace SoccerClub.Core
{
    public enum SupportKind
    {
        Facility,
        Front
    }

    /// <summary>メイン画面のサポートキャラが持ってくる提案。施設のレベルアップか、フロントの契約。</summary>
    public sealed class SupportSuggestion
    {
        public SupportKind Kind;

        // 施設
        public FacilityKind Facility;
        public int CurrentLevel;
        public int TargetLevel;

        // フロント
        public int Slot = -1;
        public FrontRole Role;
        public StaffMember Candidate;
        /// <summary>交代される人。空き枠への契約なら null。</summary>
        public StaffMember Current;

        public BigInteger Cost;

        /// <summary>キャンセルで 1 か月出さないための識別子。施設は種類と目標レベル、フロントは枠と候補。</summary>
        public string Key;
    }

    /// <summary>
    /// サポートキャラ（Figma main の support と support_event）。
    /// ・優先順位は 練習場、トレーニング室、クラブハウス、スタジアム、フロント人事。
    /// ・お金の判定は高速シミュレーターと同じ（資金が総年俸を下回らない範囲でだけ勧める）。
    /// ・スタジアムは、ホームの入場者が収容の 80% を超えているときだけ勧める。
    /// ・フロントは空き枠を先に勧める。空きがなければ、在籍 1 年以上の人より高レベルの候補との交代を勧める。
    /// ・キャンセルした内容は 1 か月出さない。提案は内容が違っても 1 日 1 回まで。
    /// </summary>
    public sealed partial class GameSession
    {
        const int SupportTenureDays = 365;

        static readonly FacilityKind[] SupportFacilityOrder =
        {
            FacilityKind.TrainingGround,
            FacilityKind.TrainingFacility,
            FacilityKind.Clubhouse,
            FacilityKind.Stadium
        };

        readonly Dictionary<string, long> _supportSnooze = new Dictionary<string, long>();
        long _supportDoneTicks;

        // 毎フレーム呼ばれるので、日付と資金が変わったときだけ探し直す。
        SupportSuggestion _supportCache;
        long _supportCacheDay = -1;
        BigInteger _supportCacheCoins;
        bool _supportDirty = true;

        /// <summary>今日のサポートの提案。今日すでに対応済み、または勧める内容がなければ null。</summary>
        public SupportSuggestion CurrentSupport
        {
            get
            {
                var club = PlayerClub;
                if (club == null || GameOver) return null;
                long today = Time.CurrentDate.Date.Ticks;
                if (_supportDoneTicks == today) return null;
                if (!_supportDirty && _supportCacheDay == today && _supportCacheCoins == club.Coins)
                    return _supportCache;

                _supportDirty = false;
                _supportCacheDay = today;
                _supportCacheCoins = club.Coins;
                _supportCache = FindSupport(today);
                return _supportCache;
            }
        }

        /// <summary>「レベルアップ」「契約する」。実行できたら true。どちらでも今日のサポートは終わり。</summary>
        public bool AcceptSupport(SupportSuggestion suggestion)
        {
            if (suggestion == null || PlayerClub == null) return false;
            MarkSupportDone();
            bool done;
            if (suggestion.Kind == SupportKind.Facility)
            {
                var facility = PlayerClub.Facilities != null ? PlayerClub.Facilities.Get(suggestion.Facility) : null;
                // 提案を開いている間にレベルが変わっていたら、別の内容なのでやめる。
                done = facility != null
                       && facility.Level == suggestion.CurrentLevel
                       && OwnerApprovesFacilityUpgrade(suggestion.Facility);
            }
            else
            {
                var front = PlayerClub.Front;
                done = front != null
                       && suggestion.Candidate != null
                       && _frontMarket.Contains(suggestion.Candidate)
                       && front.GetAt(suggestion.Slot) == suggestion.Current
                       && OwnerContractsFront(suggestion.Candidate, suggestion.Slot, out _);
            }

            NotifyChanged();
            return done;
        }

        /// <summary>「キャンセル」。同じ内容は 1 か月出さない。</summary>
        public void SnoozeSupport(SupportSuggestion suggestion)
        {
            if (suggestion != null && !string.IsNullOrEmpty(suggestion.Key))
                _supportSnooze[suggestion.Key] = Time.CurrentDate.Date.AddMonths(1).Ticks;
            MarkSupportDone();
            NotifyChanged();
        }

        void MarkSupportDone()
        {
            _supportDoneTicks = Time.CurrentDate.Date.Ticks;
            _supportDirty = true;
            _supportCache = null;
        }

        SupportSuggestion FindSupport(long today)
        {
            PruneSupportSnooze(today);
            for (int i = 0; i < SupportFacilityOrder.Length; i++)
            {
                var kind = SupportFacilityOrder[i];
                if (kind == FacilityKind.Stadium && !HomeAttendanceExceedsCapacity()) continue;
                if (!SimulationCanUpgrade(kind)) continue;
                var facility = PlayerClub.Facilities.Get(kind);
                var suggestion = new SupportSuggestion
                {
                    Kind = SupportKind.Facility,
                    Facility = kind,
                    CurrentLevel = facility.Level,
                    TargetLevel = facility.Level + 1,
                    Cost = facility.NextUpgradeCost(),
                    Key = "F:" + (int)kind + ":" + (facility.Level + 1)
                };
                if (IsSnoozed(suggestion.Key, today)) continue;
                return suggestion;
            }

            return FindFrontSupport(today, true) ?? FindFrontSupport(today, false);
        }

        /// <summary>
        /// emptySlots が true なら空き枠に入れる候補（レベルが最も高い人）、
        /// false なら在籍 1 年以上の人との交代（レベル差が最も大きい組み合わせ）を探す。
        /// </summary>
        SupportSuggestion FindFrontSupport(long today, bool emptySlots)
        {
            var club = PlayerClub;
            var front = club.Front;
            if (front == null || _frontMarket.Count == 0) return null;
            var payroll = ClubMath.RenewalAnnual(club);
            if (club.Coins < payroll) return null;

            SupportSuggestion best = null;
            int bestGain = 0;
            for (int slot = 0; slot < FrontOffice.SlotCount; slot++)
            {
                var current = front.GetAt(slot);
                if (emptySlots != (current == null)) continue;
                if (current != null && DaysEmployed(current) < SupportTenureDays) continue;
                var role = FrontOffice.RoleAt(slot);
                int currentLevel = current != null ? current.Level : 0;
                var currentWage = current != null ? ClubMath.StaffAnnual(current) : BigInteger.Zero;
                for (int i = 0; i < _frontMarket.Count; i++)
                {
                    var candidate = _frontMarket[i];
                    if (candidate == null || candidate.FrontRole != role) continue;
                    if (candidate.Level <= currentLevel) continue;
                    if (!ClubMath.StaffMarketAllows(club, candidate.Level)) continue;
                    var cost = FrontOfficeHiring.HireCost(candidate);
                    var payrollAfter = payroll - currentWage + ClubMath.StaffAnnual(candidate);
                    if (club.Coins - cost < payrollAfter) continue;
                    string key = "S:" + slot + ":" + candidate.Id;
                    if (IsSnoozed(key, today)) continue;
                    int gain = candidate.Level - currentLevel;
                    if (best != null && gain <= bestGain) continue;
                    best = new SupportSuggestion
                    {
                        Kind = SupportKind.Front,
                        Slot = slot,
                        Role = role,
                        Candidate = candidate,
                        Current = current,
                        Cost = cost,
                        Key = key
                    };
                    bestGain = gain;
                }

                // 空き枠は上の枠（秘書、GM、強化部…）から順に埋める。
                if (emptySlots && best != null) return best;
            }

            return best;
        }

        bool IsSnoozed(string key, long today)
        {
            return _supportSnooze.TryGetValue(key, out var until) && today < until;
        }

        void PruneSupportSnooze(long today)
        {
            if (_supportSnooze.Count == 0) return;
            List<string> expired = null;
            foreach (var pair in _supportSnooze)
            {
                if (today < pair.Value) continue;
                if (expired == null) expired = new List<string>();
                expired.Add(pair.Key);
            }

            if (expired == null) return;
            for (int i = 0; i < expired.Count; i++)
                _supportSnooze.Remove(expired[i]);
        }

        void WriteSupport(GameSaveFile file)
        {
            if (file == null) return;
            file.SupportDoneTicks = _supportDoneTicks;
            file.SupportSnoozeKeys = new string[_supportSnooze.Count];
            file.SupportSnoozeUntil = new long[_supportSnooze.Count];
            int index = 0;
            foreach (var pair in _supportSnooze)
            {
                file.SupportSnoozeKeys[index] = pair.Key;
                file.SupportSnoozeUntil[index] = pair.Value;
                index++;
            }
        }

        void ReadSupport(GameSaveFile file)
        {
            _supportSnooze.Clear();
            _supportDoneTicks = 0;
            _supportDirty = true;
            _supportCache = null;
            if (file == null) return;
            _supportDoneTicks = file.SupportDoneTicks;
            var keys = file.SupportSnoozeKeys;
            var until = file.SupportSnoozeUntil;
            int count = keys == null || until == null ? 0 : Math.Min(keys.Length, until.Length);
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(keys[i])) continue;
                _supportSnooze[keys[i]] = until[i];
            }
        }
    }
}
