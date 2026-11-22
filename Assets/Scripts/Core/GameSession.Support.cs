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
    /// サポートキャラ（Figma main の support と support_event）。施設は事業部、フロント人事は GM が持ってくる。
    /// ・メイン画面で事業部をタップすると、間隔とキャンセルを無視して、いま上げられる施設を勧める（RequestFacilitySupport）。
    /// ・施設のレベルアップは週に 1 回まで（前回施設を勧めた日から 7 日後以降。勧めた日のうちは出し続ける）。
    ///   条件を満たす施設のうち、費用が最も安いものを勧める。施設を勧めない日はフロント人事を探す。
    /// ・フロント人事も週に 1 回まで（前回人事を勧めた日から 7 日後以降。勧めた日のうちは出し続ける）。
    /// ・お金の判定は高速シミュレーターと同じ（資金が総年俸を下回らない範囲でだけ勧める）。
    /// ・スタジアムは、ホームの入場者が収容の 80% を超えているときだけ勧める。
    /// ・フロントは空き枠を先に勧める。空きがなければ、在籍 1 年以上の人より高レベルの候補との交代を勧める。
    /// ・キャンセルした内容は 1 か月出さない。提案は内容が違っても 1 日 1 回まで。
    /// ・一度出した提案は、プレイヤーが「レベルアップ／契約する」か「キャンセル」を選ぶまで日をまたいでも出し続ける。
    ///   施設のレベルが変わった、候補が市場からいなくなった、などで実行できなくなったときだけ取り下げる。
    ///   週 1 回の間隔は、選んだ日から数える。
    /// </summary>
    public sealed partial class GameSession
    {
        const int SupportTenureDays = 365;

        /// <summary>施設のレベルアップを勧める間隔（日）。</summary>
        const int SupportFacilityIntervalDays = 7;

        /// <summary>フロント人事を勧める間隔（日）。</summary>
        const int SupportFrontIntervalDays = 7;

        static readonly FacilityKind[] SupportFacilityOrder =
        {
            FacilityKind.TrainingGround,
            FacilityKind.TrainingFacility,
            FacilityKind.Clubhouse,
            FacilityKind.Stadium
        };

        readonly Dictionary<string, long> _supportSnooze = new Dictionary<string, long>();
        long _supportDoneTicks;
        long _supportFacilityTicks;
        long _supportFrontTicks;

        // 毎フレーム呼ばれるので、日付と資金が変わったときだけ探し直す。
        SupportSuggestion _supportCache;
        long _supportCacheDay = -1;
        BigInteger _supportCacheCoins;
        bool _supportDirty = true;

        /// <summary>選ぶまで出し続ける提案。</summary>
        SupportSuggestion _supportPending;
        /// <summary>ロード直後はキーだけ持っておき、最初の参照で提案に戻す。</summary>
        string _supportPendingKey;

        /// <summary>今日のサポートの提案。今日すでに対応済み、または勧める内容がなければ null。</summary>
        public SupportSuggestion CurrentSupport
        {
            get
            {
                var club = PlayerClub;
                if (club == null || GameOver) return null;
                long today = Time.CurrentDate.Date.Ticks;
                if (_supportDoneTicks == today) return null;

                // 選ぶまで出し続ける。実行できなくなっていたら取り下げて探し直す。
                if (_supportPending == null && !string.IsNullOrEmpty(_supportPendingKey))
                {
                    _supportPending = RestoreSupport(_supportPendingKey);
                    _supportPendingKey = null;
                }

                if (_supportPending != null)
                {
                    if (SupportStillValid(_supportPending)) return _supportPending;
                    _supportPending = null;
                    _supportDirty = true;
                }

                if (!_supportDirty && _supportCacheDay == today && _supportCacheCoins == club.Coins)
                    return _supportCache;

                _supportDirty = false;
                _supportCacheDay = today;
                _supportCacheCoins = club.Coins;
                _supportCache = FindSupport(today);
                _supportPending = _supportCache;
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
            long today = Time.CurrentDate.Date.Ticks;
            // 週 1 回の間隔は、選んだ日から数える（何日も出し続けた翌日にすぐ次が出ないように）。
            if (_supportPending != null)
            {
                if (_supportPending.Kind == SupportKind.Facility) _supportFacilityTicks = today;
                else _supportFrontTicks = today;
            }

            _supportDoneTicks = today;
            _supportDirty = true;
            _supportCache = null;
            _supportPending = null;
            _supportPendingKey = null;
        }

        /// <summary>出し続けている提案が、まだ実行できる形か（お金の判定は「レベルアップ／契約する」のときに行う）。</summary>
        bool SupportStillValid(SupportSuggestion suggestion)
        {
            var club = PlayerClub;
            if (suggestion == null || club == null) return false;
            if (suggestion.Kind == SupportKind.Facility)
            {
                var facility = club.Facilities != null ? club.Facilities.Get(suggestion.Facility) : null;
                return facility != null && facility.Level == suggestion.CurrentLevel;
            }

            var front = club.Front;
            return front != null
                   && suggestion.Candidate != null
                   && _frontMarket.Contains(suggestion.Candidate)
                   && front.GetAt(suggestion.Slot) == suggestion.Current;
        }

        /// <summary>セーブしたキー（F:種類:目標Lv / S:枠:候補Id）から提案を作り直す。作れなければ null。</summary>
        SupportSuggestion RestoreSupport(string key)
        {
            var club = PlayerClub;
            if (club == null || string.IsNullOrEmpty(key)) return null;
            var parts = key.Split(':');
            if (parts.Length != 3) return null;

            if (parts[0] == "F")
            {
                if (!int.TryParse(parts[1], out int kindValue) || !int.TryParse(parts[2], out int target)) return null;
                var kind = (FacilityKind)kindValue;
                var facility = club.Facilities != null ? club.Facilities.Get(kind) : null;
                if (facility == null || facility.Level + 1 != target) return null;
                return new SupportSuggestion
                {
                    Kind = SupportKind.Facility,
                    Facility = kind,
                    CurrentLevel = facility.Level,
                    TargetLevel = target,
                    Cost = facility.NextUpgradeCost(),
                    Key = key
                };
            }

            if (parts[0] == "S")
            {
                var front = club.Front;
                if (front == null || !int.TryParse(parts[1], out int slot)) return null;
                if (slot < 0 || slot >= FrontOffice.SlotCount) return null;
                StaffMember candidate = null;
                for (int i = 0; i < _frontMarket.Count; i++)
                {
                    var c = _frontMarket[i];
                    if (c != null && c.Id.ToString() == parts[2]) { candidate = c; break; }
                }

                if (candidate == null) return null;
                return new SupportSuggestion
                {
                    Kind = SupportKind.Front,
                    Slot = slot,
                    Role = FrontOffice.RoleAt(slot),
                    Candidate = candidate,
                    Current = front.GetAt(slot),
                    Cost = FrontOfficeHiring.HireCost(candidate),
                    Key = key
                };
            }

            return null;
        }

        SupportSuggestion FindSupport(long today)
        {
            PruneSupportSnooze(today);
            return FindFacilitySupport(today) ?? FindWeeklyFrontSupport(today);
        }

        /// <summary>前回の人事の提案から 7 日たっていれば、空き枠、次に交代の順で探す。</summary>
        SupportSuggestion FindWeeklyFrontSupport(long today)
        {
            // 提案した日はその日のうちは出し続け、次は 7 日後から。
            if (_supportFrontTicks > 0
                && today != _supportFrontTicks
                && today < new DateTime(_supportFrontTicks).AddDays(SupportFrontIntervalDays).Ticks)
                return null;

            var best = FindFrontSupport(today, true) ?? FindFrontSupport(today, false);
            if (best != null) _supportFrontTicks = today;
            return best;
        }

        /// <summary>
        /// 前回の施設の提案から 7 日たっていれば、条件を満たす施設のうち費用が最も安いものを勧める。
        /// 費用が同じなら 練習場、トレーニング室、クラブハウス、スタジアムの順。
        /// </summary>
        SupportSuggestion FindFacilitySupport(long today)
        {
            // 提案した日はその日のうちは出し続け、次は 7 日後から。
            if (_supportFacilityTicks > 0
                && today != _supportFacilityTicks
                && today < new DateTime(_supportFacilityTicks).AddDays(SupportFacilityIntervalDays).Ticks)
                return null;

            var best = BestFacilitySupport(today, true);
            if (best != null) _supportFacilityTicks = today;
            return best;
        }

        /// <summary>
        /// 事業部に相談したとき（メイン画面で事業部をタップ）。週 1 回の間隔とキャンセルは無視し、
        /// スタジアムも入場者に関係なく候補に入れる。お金の判定はいつもと同じ。勧められる施設がなければ null。
        /// 出し続けている施設の提案があれば、それをそのまま返す。
        /// </summary>
        public SupportSuggestion RequestFacilitySupport()
        {
            var club = PlayerClub;
            if (club == null || club.Facilities == null || GameOver) return null;
            if (_supportPending == null && !string.IsNullOrEmpty(_supportPendingKey))
            {
                _supportPending = RestoreSupport(_supportPendingKey);
                _supportPendingKey = null;
            }

            if (_supportPending != null && _supportPending.Kind == SupportKind.Facility && SupportStillValid(_supportPending))
                return _supportPending;

            var found = BestFacilitySupport(Time.CurrentDate.Date.Ticks, false);
            if (found == null) return null;
            // 出していたフロントの提案は取り下げる（7 日たてばまた探す）。
            _supportPending = found;
            _supportPendingKey = null;
            return found;
        }

        /// <summary>
        /// 条件を満たす施設のうち費用が最も安いもの。automatic が true のときだけ、
        /// スタジアムの入場者の条件とキャンセル（1 か月）を見る。
        /// </summary>
        SupportSuggestion BestFacilitySupport(long today, bool automatic)
        {
            SupportSuggestion best = null;
            for (int i = 0; i < SupportFacilityOrder.Length; i++)
            {
                var kind = SupportFacilityOrder[i];
                if (automatic && kind == FacilityKind.Stadium && !HomeAttendanceExceedsCapacity()) continue;
                if (!SimulationCanUpgrade(kind)) continue;
                var facility = PlayerClub.Facilities.Get(kind);
                var cost = facility.NextUpgradeCost();
                string key = "F:" + (int)kind + ":" + (facility.Level + 1);
                if (automatic && IsSnoozed(key, today)) continue;
                if (best != null && cost >= best.Cost) continue;
                best = new SupportSuggestion
                {
                    Kind = SupportKind.Facility,
                    Facility = kind,
                    CurrentLevel = facility.Level,
                    TargetLevel = facility.Level + 1,
                    Cost = cost,
                    Key = key
                };
            }

            return best;
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
            file.SupportFacilityTicks = _supportFacilityTicks;
            file.SupportFrontTicks = _supportFrontTicks;
            file.SupportPendingKey = _supportPending != null ? _supportPending.Key : _supportPendingKey;
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
            _supportFacilityTicks = 0;
            _supportFrontTicks = 0;
            _supportDirty = true;
            _supportCache = null;
            _supportPending = null;
            _supportPendingKey = null;
            if (file == null) return;
            _supportPendingKey = file.SupportPendingKey;
            _supportDoneTicks = file.SupportDoneTicks;
            _supportFacilityTicks = file.SupportFacilityTicks;
            _supportFrontTicks = file.SupportFrontTicks;
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
