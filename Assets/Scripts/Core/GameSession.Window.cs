using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using SoccerClub.Competition;
using SoccerClub.Data;
using SoccerClub.Match;
using SoccerClub.Personnel;
using SoccerClub.Time;
using SoccerClub.World;

namespace SoccerClub.Core
{
    public enum ClubStoryKind
    {
        WinterBreak,
        NewYear,
        Budget,
        Transfer,
        Signing,
        Stadium,
        Camp,
        Ticket,
        Renewal,
        StaffChange
    }

    /// <summary>休日と移籍期間に出す、クリックで進むイベント。</summary>
    public sealed class ClubStory
    {
        public ClubStoryKind Kind;
        public string Title;
        public string Body;
        public bool Budget;
        public int BudgetPercent = 50;
        public bool FinalStep => Step + 1 >= StepCount;

        internal int Step;
        internal int StepCount = 1;
        internal int ApplyStep = -1;
        internal bool Applied;
        internal bool Leaves;
        internal bool Signs;
        internal bool Negotiates;
        internal string WindowKey;
        internal string PlayerId;
        internal BigInteger Fee;
        internal BigInteger Retain;
        internal BigInteger Salary;
        internal int RetainPercent;
        internal Player Incoming;
        internal string[] Titles = new string[4];
        internal string[] Pages = new string[4];
    }

    public sealed partial class GameSession
    {
        const double LowAppearanceRate = 0.25d;
        const int StaffDismissDays = 180;
        const int StaffRetireDays = 365 * 3;
        static readonly TeamStaffRole[] StaffOfferRoles =
        {
            TeamStaffRole.Manager,
            TeamStaffRole.HeadCoach,
            TeamStaffRole.Coach,
            TeamStaffRole.PhysicalCoach,
            TeamStaffRole.Trainer,
            TeamStaffRole.Doctor
        };
        int _debugSignSerial;

        public ClubStory CurrentStory => _story;

        string _windowKey;
        DateTime _windowStart;
        DateTime _windowEnd;
        string _budgetWindowKey;
        string _announcedWinterKey;
        int _greetedYear;
        string _ticketKey;
        string _campKey;
        string _renewalKey;
        int _staffOfferMonth;
        int _staffOfferDay;
        int _staffOfferRole;
        int _staffOfferLevel;
        string _staffOfferName;
        bool _staffOfferDone;
        bool _stadiumProposal;
        int _stadiumAskedLevel = -1;
        readonly List<ScheduledDeal> _deals = new List<ScheduledDeal>();
        ClubStory _story;

        sealed class ScheduledDeal
        {
            public DateTime Date;
            public bool Incoming;
            public string PlayerId;
            public Player Target;
        }

        /// <summary>日曜と、ウィンターブレイクの全日。</summary>
        public bool IsHoliday(DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Sunday) return true;
            return WinterBreak.Contains(CurrentSeasonStart, date);
        }

        /// <summary>ウィンターブレイクか、リーグ終了翌日から7月31日まで。</summary>
        public bool IsClosedPeriod(DateTime date)
        {
            if (WinterBreak.Contains(CurrentSeasonStart, date)) return true;
            DateTime start;
            DateTime end;
            return TryOffSeason(date, out start, out end);
        }

        /// <summary>リーグ前半。ウィンターブレイクの開始より前。</summary>
        public bool IsFirstHalf(DateTime date)
        {
            var season = CurrentSeasonStart;
            var day = date.Date;
            return day >= season.Date && day < WinterBreak.Start(season);
        }

        /// <summary>休日看板を出す日。シーズン中の日曜と、閉幕期間の全日。</summary>
        public bool ShowsHolidayBoard(DateTime date)
        {
            if (IsClosedPeriod(date)) return true;
            return date.DayOfWeek == DayOfWeek.Sunday;
        }

        /// <summary>その日の朝に出すイベントがあれば開く。開いたら true。</summary>
        public bool TryOpenMorningEvent()
        {
            if (_story != null) return true;
            if (GameOver || PlayerClub == null) return false;
            var today = Time.CurrentDate.Date;
            string renewKey;
            if (TryRenewalKey(today, out renewKey))
            {
                string body = ApplySeasonRenewal();
                _renewalKey = renewKey;
                _story = MakeRenewal(body);
                return true;
            }

            EnsurePersonnelWindow(today);

            string winterKey;
            if (IsWinterStart(today, out winterKey) && _announcedWinterKey != winterKey)
            {
                _announcedWinterKey = winterKey;
                _story = MakeWinter(winterKey);
                return true;
            }

            if (today.Month == 1 && today.Day == 1 && _greetedYear != today.Year)
            {
                _greetedYear = today.Year;
                _story = MakeNewYear(today.Year);
                return true;
            }

            if (IsCampStart(today))
            {
                string campKey = "c:" + SeasonOpening(today).Ticks.ToString(CultureInfo.InvariantCulture);
                if (_campKey != campKey)
                {
                    _campKey = campKey;
                    _story = MakeCamp();
                    return true;
                }
            }

            if (IsTicketDay(today))
            {
                DecideTicketPrices();
                string ticketKey = "t:" + SeasonOpening(today).Ticks.ToString(CultureInfo.InvariantCulture);
                if (_ticketKey != ticketKey)
                {
                    _ticketKey = ticketKey;
                    _story = MakeTicket();
                    return true;
                }
            }

            DateTime start;
            DateTime end;
            string key;
            if (TrySpan(today, out start, out end, out key) && _budgetWindowKey != key)
            {
                _story = MakeBudget(key);
                return true;
            }

            if (_stadiumProposal)
            {
                _stadiumProposal = false;
                _story = MakeStadium();
                return true;
            }

            if (TryOpenStaffChange(today))
                return true;

            return TryOpenDeal(today);
        }

        /// <summary>表示中のイベントを1段進める。まだ続きがあれば true。</summary>
        public bool ContinueStory()
        {
            if (_story == null) return false;
            if (_story.Budget)
            {
                AllocateReinforcement(_story.BudgetPercent);
                _budgetWindowKey = _story.WindowKey;
                _story = null;
                return false;
            }

            if (_story.Step + 1 < _story.StepCount)
            {
                _story.Step++;
                if (_story.Step == _story.ApplyStep && !_story.Applied)
                    ApplyStory();
                ShowStoryStep();
                return true;
            }

            _story = null;
            return false;
        }

        /// <summary>交渉に入り、所属枠と強化費があり、成約する見込み。</summary>
        public bool WillSigningSucceed()
        {
            if (_story == null || _story.Kind != ClubStoryKind.Signing) return false;
            if (!_story.Negotiates || !_story.Signs || _story.Incoming == null) return false;
            if (PlayerClub == null) return false;
            if (SquadCount(PlayerClub) >= ClubMath.SquadLimit) return false;
            var cost = _story.Fee + _story.Salary;
            return PlayerClub.Reinforcement >= cost;
        }

        /// <summary>表示中の補強候補が、すでに所属へ入っている。</summary>
        public bool SigningJoined()
        {
            if (_story == null || _story.Incoming == null || string.IsNullOrEmpty(_story.Incoming.Id)) return false;
            if (PlayerClub == null || PlayerClub.Squad == null) return false;
            string id = _story.Incoming.Id;
            for (int i = 0; i < PlayerClub.Squad.Count; i++)
            {
                var player = PlayerClub.Squad[i];
                if (player != null && player.Id == id) return true;
            }

            return false;
        }

        /// <summary>移籍オファーの選手が、スタメンかベンチに入っている。</summary>
        public bool IsMatchSheetPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            return SheetIds(PlayerClub).Contains(playerId);
        }

        /// <summary>話し合いの結果、選手が移籍する見込み。</summary>
        public bool WillTransferLeave()
        {
            if (_story == null || _story.Kind != ClubStoryKind.Transfer || !_story.Leaves) return false;
            if (FindSquad(_story.PlayerId) == null) return false;
            return SquadCount(PlayerClub) > ClubMath.MinimumSquad;
        }

        /// <summary>移籍金を受け取って、選手がチームを離れた。</summary>
        public bool TransferDeparted()
        {
            if (_story == null || _story.Kind != ClubStoryKind.Transfer || !_story.Applied || !_story.Leaves) return false;
            int page = _story.ApplyStep;
            if (page < 0 || _story.Pages == null || page >= _story.Pages.Length) return false;
            var text = _story.Pages[page];
            return text != null && text.IndexOf("移籍が決まりました", StringComparison.Ordinal) >= 0;
        }

        /// <summary>表示中のイベントの確定処理を、まだなら一度だけ行う。</summary>
        public void ApplyOpenStory()
        {
            if (_story == null || _story.Applied || _story.ApplyStep < 0) return;
            _story.Step = _story.ApplyStep;
            ApplyStory();
            ShowStoryStep();
        }

        /// <summary>表示中のイベントを確定して閉じる。</summary>
        public void CloseStory()
        {
            ApplyOpenStory();
            _story = null;
        }

        public void ClosePersonnelWindowIfLeft(DateTime closed, DateTime opened)
        {
            if (InPersonnelWindow(closed) && !InPersonnelWindow(opened))
                ResetWindowAppearances();
        }

        void EnsurePersonnelWindow(DateTime today)
        {
            DateTime start;
            DateTime end;
            string key;
            if (!TrySpan(today, out start, out end, out key)) return;
            if (_windowKey == key) return;
            _windowKey = key;
            _windowStart = start;
            _windowEnd = end;
            _deals.Clear();
            PreparePlayerDeals(today);
            RunRivalTransfers();
        }

        bool InPersonnelWindow(DateTime date)
        {
            DateTime start;
            DateTime end;
            string key;
            return TrySpan(date.Date, out start, out end, out key);
        }

        bool TrySpan(DateTime date, out DateTime start, out DateTime end, out string key)
        {
            var day = date.Date;
            var season = CurrentSeasonStart;
            if (WinterBreak.Contains(season, day))
            {
                start = WinterBreak.Start(season);
                end = WinterBreak.End(season);
                key = "w:" + start.Ticks.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (TryOffSeason(day, out start, out end))
            {
                key = "o:" + start.Ticks.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            start = default;
            end = default;
            key = null;
            return false;
        }

        bool TryOffSeason(DateTime day, out DateTime start, out DateTime end)
        {
            start = default;
            end = default;
            DateTime last;
            if (!LeagueIsComplete(out last)) return false;
            var next = new DateTime(last.Year, 8, 1);
            if (last.Month >= 8) next = next.AddYears(1);
            start = last.Date.AddDays(1);
            end = next.AddDays(-15);
            if (end < start) return false;
            return day >= start && day <= end;
        }

        /// <summary>次の8月1日。その日以降なら翌年。</summary>
        static DateTime SeasonOpening(DateTime day)
        {
            var date = day.Date;
            if (date.Month < 8) return new DateTime(date.Year, 8, 1);
            return new DateTime(date.Year + 1, 8, 1);
        }

        bool IsCampStart(DateTime day)
        {
            return day.Date == SeasonOpening(day).AddDays(-14);
        }

        bool IsTicketDay(DateTime day)
        {
            return day.Date == SeasonOpening(day).AddDays(-7);
        }

        /// <summary>シーズン前のチケット価格。事業部の見積もりに、リーグLVの倍率をかける。</summary>
        void DecideTicketPrices()
        {
            double strength = LeagueStrengthFactor;
            for (int i = 0; i < Clubs.Count; i++)
            {
                var club = Clubs[i];
                if (club == null) continue;
                club.TicketPrice = ClubMath.RoundInt(ClubMath.QuoteTicket(club) * strength);
            }
        }

        void AllocateReinforcement(int percent)
        {
            if (PlayerClub == null) return;
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            percent = percent / 10 * 10;
            var amount = PlayerClub.Coins * percent / 100;
            if (amount < 0) amount = BigInteger.Zero;
            if (amount > PlayerClub.Coins) amount = PlayerClub.Coins;
            if (amount > 0)
            {
                PlayerClub.Coins -= amount;
                PlayerClub.Reinforcement += amount;
            }

            NotifyChanged();
        }

        void NoteStadiumDemand(Fixture fixture)
        {
            if (fixture == null || PlayerClub == null || fixture.Home != PlayerClub) return;
            if (fixture.StadiumCapacity <= 0) return;
            if ((long)fixture.Attendance * 100L <= (long)fixture.StadiumCapacity * 80L) return;
            var stadium = PlayerClub.Facilities != null ? PlayerClub.Facilities.Stadium : null;
            if (stadium == null || !stadium.CanUpgrade()) return;
            if (_stadiumAskedLevel == stadium.Level) return;
            _stadiumAskedLevel = stadium.Level;
            _stadiumProposal = true;
        }

        bool IsWinterStart(DateTime today, out string key)
        {
            var start = WinterBreak.Start(CurrentSeasonStart);
            key = "w:" + start.Ticks.ToString(CultureInfo.InvariantCulture);
            return today.Date == start;
        }

        void PreparePlayerDeals(DateTime today)
        {
            var club = PlayerClub;
            int count = SquadCount(club);
            var onSheet = SheetIds(club);
            double average = SquadAverage(club);

            var outgoing = new List<Player>();
            if (count > ClubMath.MinimumSquad && club.Squad != null)
            {
                for (int i = 0; i < club.Squad.Count; i++)
                {
                    var player = club.Squad[i];
                    if (player == null || string.IsNullOrEmpty(player.Id)) continue;
                    bool low = IsLowAppearance(club, player);
                    bool high = average > 0d && player.Overall >= average;
                    if (!low && !high) continue;
                    outgoing.Add(player);
                }

                var noise = new Dictionary<string, int>();
                for (int i = 0; i < outgoing.Count; i++)
                    noise[outgoing[i].Id] = _random.Next();
                outgoing.Sort((a, b) => CompareOutgoing(club, onSheet, noise, a, b));
                int cap = count - ClubMath.MinimumSquad;
                if (outgoing.Count > cap)
                    outgoing.RemoveRange(cap, outgoing.Count - cap);
            }

            var transferDays = DaysInWindow(today);
            Shuffle(transferDays);
            int sales = Math.Min(outgoing.Count, transferDays.Count);
            for (int i = 0; i < sales; i++)
            {
                _deals.Add(new ScheduledDeal
                {
                    Date = transferDays[i],
                    PlayerId = outgoing[i].Id
                });
            }

            var signDays = DaysInWindow(today);
            var candidates = BuildSigningCandidates();
            if (SquadCount(club) >= ClubMath.SquadLimit)
                candidates.Clear();
            else
                candidates.RemoveAll(player => !SigningCanOpen(player));
            Shuffle(signDays);
            int signs = Math.Min(candidates.Count, signDays.Count);
            for (int i = 0; i < signs; i++)
            {
                _deals.Add(new ScheduledDeal
                {
                    Date = signDays[i],
                    Incoming = true,
                    Target = candidates[i],
                    PlayerId = candidates[i] != null ? candidates[i].Id : null
                });
            }
        }

        List<Player> BuildSigningCandidates()
        {
            var list = Scouting.BuildShortlist(
                PlayerClub,
                _freeAgents,
                new List<StaffMember>(),
                new HashSet<string>(),
                new HashSet<string>());
            var players = list != null ? new List<Player>(list.Players) : new List<Player>();
            players.RemoveAll(player => player == null);
            players.Sort((a, b) => GeneralManagerAi.CompareWanted(PlayerClub, a, b));
            return players;
        }

        void RunRivalTransfers()
        {
            var stamp = Time.CurrentDate.Ticks.ToString(CultureInfo.InvariantCulture);
            var staff = new List<StaffMember>();
            var order = new List<Club>();
            for (int i = 0; i < Clubs.Count; i++)
            {
                var club = Clubs[i];
                if (club == null || club.IsPlayerOwned || club.Facilities == null) continue;
                order.Add(club);
                staff.AddRange(WorldFactory.CreateTeamStaffMarket(
                    _random,
                    "staff-" + stamp + "-" + i,
                    club.Facilities.MarketLevelFloor(),
                    club.Facilities.MarketLevelCap()));
            }

            order.Sort((a, b) => ScoutLevel(b).CompareTo(ScoutLevel(a)));
            var takenPlayers = ScheduledPlayerIds();
            var takenStaff = new HashSet<string>();
            var lists = new Dictionary<Club, TransferShortlist>();
            for (int i = 0; i < order.Count; i++)
            {
                var club = order[i];
                lists[club] = Scouting.BuildShortlist(club, _freeAgents, staff, takenPlayers, takenStaff);
            }

            for (int i = 0; i < order.Count; i++)
            {
                var club = order[i];
                GeneralManager.ReleaseContracts(club);
                TransferShortlist list;
                lists.TryGetValue(club, out list);
                GeneralManager.SignListed(club, list, Time.CurrentDate);
            }

            DetachSignedFreeAgents();
        }

        HashSet<string> ScheduledPlayerIds()
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < _deals.Count; i++)
            {
                var deal = _deals[i];
                if (deal != null && deal.Incoming && !string.IsNullOrEmpty(deal.PlayerId))
                    ids.Add(deal.PlayerId);
            }

            return ids;
        }

        void DetachSignedFreeAgents()
        {
            var clubbed = new HashSet<Player>();
            for (int c = 0; c < Clubs.Count; c++)
            {
                var squad = Clubs[c] != null ? Clubs[c].Squad : null;
                if (squad == null) continue;
                for (int i = 0; i < squad.Count; i++)
                {
                    if (squad[i] != null) clubbed.Add(squad[i]);
                }
            }

            for (int i = 0; i < _freeAgents.Count; i++)
            {
                var player = _freeAgents[i];
                if (player != null && clubbed.Contains(player))
                    _freeAgents[i] = WorldFactory.CreateFreeAgent(_random, _freeAgentSerial++, 18);
            }
        }

        Player LiveFreeAgent(Player stored)
        {
            if (stored == null || string.IsNullOrEmpty(stored.Id)) return stored;
            for (int i = 0; i < _freeAgents.Count; i++)
            {
                var player = _freeAgents[i];
                if (player != null && player.Id == stored.Id)
                    return player;
            }

            return stored;
        }

        List<DateTime> DaysInWindow(DateTime today)
        {
            var days = new List<DateTime>();
            var from = today.Date > _windowStart.Date ? today.Date : _windowStart.Date;
            for (var day = from; day <= _windowEnd.Date; day = day.AddDays(1))
            {
                if (IsYearEndHoliday(day)) continue;
                days.Add(day);
            }

            return days;
        }

        /// <summary>12/28から1/3は移籍も補強も出さない。</summary>
        static bool IsYearEndHoliday(DateTime day)
        {
            if (day.Month == 12 && day.Day >= 28) return true;
            return day.Month == 1 && day.Day <= 3;
        }

        bool TryOpenDeal(DateTime today)
        {
            PostponeYearEndDeals(today);
            if (IsYearEndHoliday(today)) return false;

            for (int guard = 0; guard < 12; guard++)
            {
                var deal = DueDeal(today);
                if (deal == null) return false;
                if (deal.Incoming)
                {
                    var target = LiveFreeAgent(deal.Target);
                    if (target == null || !SigningCanOpen(target))
                    {
                        _deals.Remove(deal);
                        continue;
                    }

                    _deals.Remove(deal);
                    _story = MakeSigning(target);
                    if (_story == null) continue;
                    return true;
                }

                if (SquadCount(PlayerClub) <= ClubMath.MinimumSquad)
                {
                    _deals.Remove(deal);
                    continue;
                }

                _deals.Remove(deal);

                var player = FindSquad(deal.PlayerId);
                if (player == null) continue;
                _story = MakeTransfer(player);
                return true;
            }

            return false;
        }

        void PostponeYearEndDeals(DateTime today)
        {
            for (int i = _deals.Count - 1; i >= 0; i--)
            {
                var deal = _deals[i];
                if (deal == null || !IsYearEndHoliday(deal.Date)) continue;
                if (deal.Date.Date > today.Date) continue;
                if (IsYearEndHoliday(today))
                {
                    if (!MoveLater(deal, today))
                        _deals.RemoveAt(i);
                }
                else
                {
                    deal.Date = today;
                }
            }
        }

        ScheduledDeal DueDeal(DateTime today)
        {
            ScheduledDeal signing = null;
            for (int i = 0; i < _deals.Count; i++)
            {
                var deal = _deals[i];
                if (deal == null || deal.Date.Date != today) continue;
                if (!deal.Incoming) return deal;
                if (signing == null) signing = deal;
            }

            return signing;
        }

        bool MoveLater(ScheduledDeal deal, DateTime today)
        {
            var used = new HashSet<long>();
            for (int i = 0; i < _deals.Count; i++)
            {
                var other = _deals[i];
                if (other == null || other == deal || other.Incoming != deal.Incoming) continue;
                used.Add(other.Date.Date.Ticks);
            }

            var options = new List<DateTime>();
            for (var day = today.AddDays(1); day <= _windowEnd.Date; day = day.AddDays(1))
            {
                if (IsYearEndHoliday(day)) continue;
                if (used.Contains(day.Ticks)) continue;
                options.Add(day);
            }

            if (options.Count == 0) return false;
            deal.Date = options[_random.Next(options.Count)];
            return true;
        }

        ClubStory MakeWinter(string key)
        {
            var season = CurrentSeasonStart;
            var start = WinterBreak.Start(season);
            var end = WinterBreak.End(season);
            var story = new ClubStory
            {
                Kind = ClubStoryKind.WinterBreak,
                WindowKey = key
            };
            story.Titles[0] = "ウィンターブレイク";
            story.Pages[0] = "ウィンターブレイクが始まりました。\n"
                + start.ToString("MM/dd", CultureInfo.InvariantCulture)
                + " から "
                + end.ToString("MM/dd", CultureInfo.InvariantCulture)
                + " まで試合はありません。この期間は休日です。";
            ShowStoryStep(story);
            return story;
        }

        ClubStory MakeNewYear(int year)
        {
            var story = new ClubStory { Kind = ClubStoryKind.NewYear };
            story.Titles[0] = "新しい年";
            story.Pages[0] = year.ToString(CultureInfo.InvariantCulture) + "年になりました。\nあけましておめでとうございます。";
            ShowStoryStep(story);
            return story;
        }

        bool TryRenewalKey(DateTime today, out string key)
        {
            key = null;
            DateTime start;
            DateTime end;
            if (!TryOffSeason(today, out start, out end)) return false;
            key = "o:" + start.Ticks.ToString(CultureInfo.InvariantCulture);
            return _renewalKey != key;
        }

        ClubStory MakeRenewal(string body)
        {
            var story = new ClubStory { Kind = ClubStoryKind.Renewal };
            story.Titles[0] = "契約更新";
            story.Pages[0] = body ?? "";
            ShowStoryStep(story);
            return story;
        }

        string ApplySeasonRenewal()
        {
            int raised = 0;
            int held = 0;
            int keptVeterans = 0;
            var expired = new List<string>();
            for (int i = 0; i < Clubs.Count; i++)
            {
                var club = Clubs[i];
                if (club == null) continue;
                bool report = club.IsPlayerOwned;
                RaiseFrontOffice(club, report, ref raised, ref held);
                int kept = ExpireIdleVeterans(club, report ? expired : null);
                if (report) keptVeterans = kept;
            }

            return RenewalBody(raised, held, expired, keptVeterans);
        }

        void RaiseFrontOffice(Club club, bool report, ref int raised, ref int held)
        {
            var front = club.Front;
            if (front == null) return;
            RaiseFrontOne(club, front.Secretary, report, ref raised, ref held);
            RaiseFrontOne(club, front.GeneralManager, report, ref raised, ref held);
            RaiseFrontSlots(club, front.Strengthening, report, ref raised, ref held);
            RaiseFrontSlots(club, front.Sales, report, ref raised, ref held);
            RaiseFrontSlots(club, front.PublicRelations, report, ref raised, ref held);
            RaiseFrontSlots(club, front.Business, report, ref raised, ref held);
            RaiseFrontSlots(club, front.Academy, report, ref raised, ref held);
        }

        void RaiseFrontSlots(Club club, StaffMember[] slots, bool report, ref int raised, ref int held)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
                RaiseFrontOne(club, slots[i], report, ref raised, ref held);
        }

        void RaiseFrontOne(Club club, StaffMember staff, bool report, ref int raised, ref int held)
        {
            if (staff == null || staff.Age >= ClubMath.RetirementAge) return;
            var currentPay = ClubMath.StaffAnnual(staff);
            int next = staff.Level + 1;
            if (next <= ClubMath.MaxStaffLevel)
            {
                var nextPay = ClubMath.StaffAnnual(ClubMath.StaffBase(staff), next);
                if (nextPay > 0 && Economy.TrySpend(club, nextPay, "front-renew-" + staff.Id))
                {
                    staff.Level = next;
                    if (report) raised++;
                    return;
                }
            }

            if (currentPay > 0 && Economy.TrySpend(club, currentPay, "front-renew-" + staff.Id))
            {
                if (report) held++;
                return;
            }

            if (report) held++;
        }

        int ExpireIdleVeterans(Club club, List<string> names)
        {
            if (club.Squad == null || club.MatchesSinceWindow <= 0) return 0;
            var idle = new List<Player>();
            for (int i = 0; i < club.Squad.Count; i++)
            {
                var player = club.Squad[i];
                if (player == null || player.Appearances > 0) continue;
                if (player.Age < 32 && player.Phase != GrowthPhase.Veteran) continue;
                idle.Add(player);
            }

            idle.Sort((a, b) => a.Overall.CompareTo(b.Overall));
            int room = club.Squad.Count - ClubMath.MinimumSquad;
            int removed = 0;
            for (int i = 0; i < idle.Count && room > 0; i++)
            {
                club.Squad.Remove(idle[i]);
                room--;
                removed++;
                if (names != null)
                    names.Add(idle[i].Name ?? "選手");
            }

            return idle.Count - removed;
        }

        static string RenewalBody(int raised, int held, List<string> expired, int keptVeterans)
        {
            var lines = new List<string>();
            lines.Add("オフシーズンに入り、契約を更新しました。");
            if (raised > 0 && held == 0)
                lines.Add("フロントは昇給し、契約を更新しました。");
            else if (raised > 0)
                lines.Add("フロントは契約を更新しました。資金の範囲で昇給しています。");
            else
                lines.Add("フロントは契約を更新しました。");
            lines.Add("チームスタッフの交代は、月に1人の候補で判断します。");

            if (expired != null && expired.Count > 0)
                lines.Add(JoinNotes(expired, 4, "") + "は出場がなく、契約満了です。移籍金はありません。");
            else if (keptVeterans > 0)
                lines.Add("出場がないベテランがいますが、所属が15人のため残しました。");
            else
                lines.Add("契約満了の選手はいません。");
            return string.Join("\n", lines.ToArray());
        }

        static string JoinNotes(List<string> notes, int limit, string overflow)
        {
            if (notes == null || notes.Count == 0) return "";
            int show = notes.Count < limit ? notes.Count : limit;
            var text = "";
            for (int i = 0; i < show; i++)
            {
                if (i > 0) text += "、";
                text += notes[i];
            }

            int rest = notes.Count - show;
            if (rest > 0)
                text += "、ほか" + rest.ToString(CultureInfo.InvariantCulture) + "人" + (overflow ?? "");
            return text;
        }

        int DaysEmployed(StaffMember staff)
        {
            if (staff == null) return 0;
            var joined = staff.JoinedTicks > 0 ? new DateTime(staff.JoinedTicks).Date : SeasonStart;
            int days = (int)(Time.CurrentDate.Date - joined).TotalDays;
            return days < 0 ? 0 : days;
        }

        bool TryOpenStaffChange(DateTime today)
        {
            EnsureStaffOffer(today);
            if (_staffOfferDone || today.Day != _staffOfferDay) return false;
            _staffOfferDone = true;
            var story = MakeStaffChange(today);
            if (story == null) return false;
            _story = story;
            return true;
        }

        void EnsureStaffOffer(DateTime today)
        {
            int month = today.Year * 100 + today.Month;
            if (_staffOfferMonth == month) return;
            var club = PlayerClub;
            _staffOfferMonth = month;
            _staffOfferDone = false;
            _staffOfferName = null;
            _staffOfferLevel = 1;
            _staffOfferRole = (int)TeamStaffRole.Manager;
            _staffOfferDay = today.Day;
            if (club == null) return;
            int pick = _random.Next(StaffOfferRoles.Length);
            _staffOfferRole = (int)StaffOfferRoles[pick];
            int cap = club.Facilities != null ? club.Facilities.MarketLevelCap() : 1;
            int high = cap + 2;
            if (high < 1) high = 1;
            if (high > ClubMath.MaxStaffLevel) high = ClubMath.MaxStaffLevel;
            _staffOfferLevel = 1 + _random.Next(high);
            int last = DateTime.DaysInMonth(today.Year, today.Month);
            int span = last - today.Day + 1;
            _staffOfferDay = today.Day + (span <= 1 ? 0 : _random.Next(span));
            _staffOfferName = NameCatalog.ForId("offer-" + club.Id + "-" + month.ToString(CultureInfo.InvariantCulture));
        }

        ClubStory MakeStaffChange(DateTime today)
        {
            var club = PlayerClub;
            var team = club != null ? club.Team : null;
            if (club == null || team == null) return null;
            if (_staffOfferRole < 0 || _staffOfferRole > (int)TeamStaffRole.Doctor) return null;
            var role = (TeamStaffRole)_staffOfferRole;

            StaffMember current = null;
            int slot = 0;
            bool retire = false;
            int bestLevel = int.MaxValue;
            int bestDays = -1;
            ConsiderStaff(club, team.Manager, TeamStaffRole.Manager, role, 0, ref current, ref slot, ref retire, ref bestLevel, ref bestDays);
            ConsiderStaff(club, team.HeadCoach, TeamStaffRole.HeadCoach, role, 0, ref current, ref slot, ref retire, ref bestLevel, ref bestDays);
            ConsiderStaff(club, team.PhysicalCoach, TeamStaffRole.PhysicalCoach, role, 0, ref current, ref slot, ref retire, ref bestLevel, ref bestDays);
            ConsiderStaff(club, team.Doctor, TeamStaffRole.Doctor, role, 0, ref current, ref slot, ref retire, ref bestLevel, ref bestDays);
            ConsiderStaffSlots(club, team.Coaches, TeamStaffRole.Coach, role, ref current, ref slot, ref retire, ref bestLevel, ref bestDays);
            ConsiderStaffSlots(club, team.Trainers, TeamStaffRole.Trainer, role, ref current, ref slot, ref retire, ref bestLevel, ref bestDays);
            if (current == null) return null;

            var hire = CreateOfferStaff(club, role, today);
            var annual = ClubMath.StaffAnnual(hire);
            var cost = annual * 2;
            bool paid = cost > 0 && Economy.TrySpendReinforcement(club, cost, "staff-change-" + hire.Id);
            if (paid)
                PlaceStaff(team, role, slot, hire);

            string oldName = string.IsNullOrEmpty(current.Name) ? "スタッフ" : current.Name;
            string newName = string.IsNullOrEmpty(hire.Name) ? "スタッフ" : hire.Name;
            int rank = LeagueRank(club);
            var lines = new List<string>();
            lines.Add(StaffRoleName(role) + "の" + oldName + "は在籍" + bestDays.ToString(CultureInfo.InvariantCulture) + "日です。");
            lines.Add(rank <= 0
                ? "リーグの順位はまだありません。"
                : "リーグの順位は" + rank.ToString(CultureInfo.InvariantCulture) + "位です。");
            lines.Add(retire
                ? "在籍が3年に達したため、勇退です。"
                : "順位が10位以下で、在籍が180日以上のため、解雇です。");
            lines.Add("候補は" + newName + "（Lv" + hire.Level.ToString(CultureInfo.InvariantCulture) + "）です。");
            lines.Add("契約金と年俸は各" + StoryMoney(annual) + "、合計" + StoryMoney(cost) + "を強化費から払います。");
            if (paid)
            {
                lines.Add(oldName + "の契約は満了しました。この契約満了に資金の増減はありません。");
                lines.Add(newName + "と契約しました。");
            }
            else
            {
                lines.Add("強化費が足りないため、契約できませんでした。" + oldName + "はそのままです。");
            }

            var story = new ClubStory { Kind = ClubStoryKind.StaffChange };
            story.Titles[0] = "スタッフ交代";
            story.Pages[0] = string.Join("\n", lines.ToArray());
            ShowStoryStep(story);
            return story;
        }

        void ConsiderStaffSlots(
            Club club, StaffMember[] slots, TeamStaffRole slotRole, TeamStaffRole offerRole,
            ref StaffMember best, ref int bestIndex, ref bool bestRetire, ref int bestLevel, ref int bestDays)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
                ConsiderStaff(club, slots[i], slotRole, offerRole, i, ref best, ref bestIndex, ref bestRetire, ref bestLevel, ref bestDays);
        }

        void ConsiderStaff(
            Club club, StaffMember staff, TeamStaffRole slotRole, TeamStaffRole offerRole, int index,
            ref StaffMember best, ref int bestIndex, ref bool bestRetire, ref int bestLevel, ref int bestDays)
        {
            if (slotRole != offerRole || staff == null) return;
            if (_staffOfferLevel <= staff.Level) return;
            int days = DaysEmployed(staff);
            bool retire = days >= StaffRetireDays;
            bool dismiss = !retire && LeagueRank(club) >= 10 && days >= StaffDismissDays;
            if (!retire && !dismiss) return;
            if (best != null && (staff.Level > bestLevel || (staff.Level == bestLevel && days <= bestDays)))
                return;
            best = staff;
            bestIndex = index;
            bestRetire = retire;
            bestLevel = staff.Level;
            bestDays = days;
        }

        StaffMember CreateOfferStaff(Club club, TeamStaffRole role, DateTime today)
        {
            int level = _staffOfferLevel;
            if (level < 1) level = 1;
            if (level > ClubMath.MaxStaffLevel) level = ClubMath.MaxStaffLevel;
            string id = "chg-" + club.Id + "-" + _staffOfferMonth.ToString(CultureInfo.InvariantCulture) + "-" + ((int)role).ToString(CultureInfo.InvariantCulture);
            var staff = new StaffMember
            {
                Id = id,
                Name = string.IsNullOrEmpty(_staffOfferName) ? NameCatalog.ForId(id) : _staffOfferName,
                Age = 26 + _random.Next(39),
                Level = level,
                IsFrontOffice = false,
                TeamRole = role,
                JoinedTicks = today.Ticks,
                ScoutedGroup = (PositionGroup)_random.Next(4)
            };
            if (role == TeamStaffRole.Manager && FormationCatalog.All.Length > 0)
            {
                staff.FavoriteFormation = FormationCatalog.All[_random.Next(FormationCatalog.All.Length)].Type;
                staff.FavoriteStyle = (PlayingStyle)_random.Next(5);
                staff.LineupPriority = (LineupPriority)_random.Next(2);
            }

            return staff;
        }

        static void PlaceStaff(TeamStaff team, TeamStaffRole role, int index, StaffMember hire)
        {
            if (team == null || hire == null) return;
            switch (role)
            {
                case TeamStaffRole.Manager:
                    team.Manager = hire;
                    break;
                case TeamStaffRole.HeadCoach:
                    team.HeadCoach = hire;
                    break;
                case TeamStaffRole.PhysicalCoach:
                    team.PhysicalCoach = hire;
                    break;
                case TeamStaffRole.Doctor:
                    team.Doctor = hire;
                    break;
                case TeamStaffRole.Coach:
                    if (team.Coaches != null && index >= 0 && index < team.Coaches.Length)
                        team.Coaches[index] = hire;
                    break;
                case TeamStaffRole.Trainer:
                    if (team.Trainers != null && index >= 0 && index < team.Trainers.Length)
                        team.Trainers[index] = hire;
                    break;
            }
        }

        static string StaffRoleName(TeamStaffRole role)
        {
            switch (role)
            {
                case TeamStaffRole.Manager: return "監督";
                case TeamStaffRole.HeadCoach: return "ヘッドコーチ";
                case TeamStaffRole.Coach: return "コーチ";
                case TeamStaffRole.PhysicalCoach: return "フィジカルコーチ";
                case TeamStaffRole.Trainer: return "トレーナー";
                case TeamStaffRole.Doctor: return "ドクター";
                default: return "スタッフ";
            }
        }

        ClubStory MakeBudget(string key)
        {
            var story = new ClubStory
            {
                Kind = ClubStoryKind.Budget,
                Budget = true,
                WindowKey = key
            };
            story.Titles[0] = "強化費の予算";
            story.Pages[0] = "この期間の補強に使う強化費を、資産の10%単位で決めてください。\n移籍金と年俸は、強化費から支払います。";
            story.BudgetPercent = 50;
            ShowStoryStep(story);
            return story;
        }

        ClubStory MakeCamp()
        {
            var story = new ClubStory { Kind = ClubStoryKind.Camp };
            story.Titles[0] = "キャンプ";
            story.Pages[0] = "開幕に向けたキャンプが始まりました。\n2週間前からの1週間、チームはキャンプです。";
            ShowStoryStep(story);
            return story;
        }

        ClubStory MakeTicket()
        {
            int yen = PlayerClub != null ? PlayerClub.TicketPrice : 0;
            var story = new ClubStory { Kind = ClubStoryKind.Ticket };
            story.Titles[0] = "チケット価格";
            story.Pages[0] = "事業部が今季のチケット価格を "
                + MoneyText.Format(yen)
                + "に決めました。";
            ShowStoryStep(story);
            return story;
        }

        ClubStory MakeStadium()
        {
            var stadium = PlayerClub != null && PlayerClub.Facilities != null ? PlayerClub.Facilities.Stadium : null;
            var cost = stadium != null ? stadium.NextUpgradeCost() : BigInteger.Zero;
            string secretary = "秘書";
            var front = PlayerClub != null ? PlayerClub.Front : null;
            if (front != null && front.Secretary != null && !string.IsNullOrEmpty(front.Secretary.Name))
                secretary = front.Secretary.Name;
            var story = new ClubStory { Kind = ClubStoryKind.Stadium };
            story.Titles[0] = "スタジアムの拡張";
            story.Pages[0] = "ホームの入場者が収容の80%を超えました。\n"
                + secretary + "が、スタジアムのレベルアップを提案しています。\n費用 "
                + StoryMoney(cost) + "。";
            ShowStoryStep(story);
            return story;
        }

        ClubStory MakeTransfer(Player player)
        {
            var club = PlayerClub;
            bool low = IsLowAppearance(club, player);
            bool veteran = player.Phase == GrowthPhase.Veteran || player.Age >= 32;
            bool prime = !veteran && player.Phase == GrowthPhase.Prime;
            bool approve = veteran && low;
            int percent = approve ? 0 : (prime && low ? 30 : 10);
            string reason;
            if (approve)
                reason = "出場機会の少ないベテランです。GMはオファーを承認します。";
            else if (veteran)
                reason = "ベテランですが、出場があります。GMは留意します。";
            else if (prime)
                reason = "中堅です。出場数にかかわらず、GMは留意します。";
            else
                reason = "若手です。出場数にかかわらず、GMは留意します。";

            var fee = ClubMath.TransferFee(player);
            var salary = ClubMath.PlayerAnnual(player);
            var retain = percent <= 0
                ? BigInteger.Zero
                : ClubMath.RoundCoins(ClubMath.ToDouble(salary) * percent / 100d);
            double leave = 1d - ClubMath.NegotiationChance(GmLevel(club));
            bool leaves = _random.NextDouble() < leave;
            var story = new ClubStory
            {
                Kind = ClubStoryKind.Transfer,
                StepCount = 4,
                ApplyStep = 3,
                Leaves = leaves,
                PlayerId = player.Id,
                Fee = fee,
                Retain = retain,
                Salary = salary,
                RetainPercent = percent
            };
            story.Titles[0] = "オファーが届きました";
            story.Titles[1] = "GMの方針";
            story.Titles[2] = "話し合い";
            story.Titles[3] = "移籍の結果";
            string purse = IsClosedPeriod(Time.CurrentDate) ? "資産" : "強化費";
            story.Pages[0] = Line(player) + "に、他クラブから移籍オファーが届きました。\n移籍金  "
                + StoryMoney(fee) + "\n移籍金は" + purse + "に入ります。";
            int stay = ClubMath.RoundInt(ClubMath.NegotiationChance(GmLevel(club)) * 100d);
            int move = 100 - stay;
            story.Pages[1] = GmName(club) + "の方針です。\n" + reason
                + (approve
                    ? "\n移籍の見込みは " + move.ToString(CultureInfo.InvariantCulture) + "% です。"
                    : "\n残留の見込みは " + stay.ToString(CultureInfo.InvariantCulture) + "% です。")
                + (!approve && percent > 0 ? "\n残留した場合は、年俸の" + percent + "%を支払います。" : "");
            story.Pages[2] = GmName(club) + "が" + (player.Name ?? "選手") + "と話し合いを行っています。";
            story.Pages[3] = "話し合いの結果を待っています。";
            ShowStoryStep(story);
            return story;
        }

        /// <summary>デバッグ用。仮の選手で補強交渉を開く。</summary>
        public bool DebugBeginSigning()
        {
            if (_story != null || PlayerClub == null) return false;
            var player = WorldFactory.CreatePlayer(
                _random,
                "debug-sign-" + (++_debugSignSerial).ToString(CultureInfo.InvariantCulture),
                22,
                GrowthPhase.Prime,
                1d,
                Position.CF);
            player.Name = "仮選手";
            player.Age = 22;
            var fee = new BigInteger(1000000);
            var salary = new BigInteger(500000);
            var story = new ClubStory
            {
                Kind = ClubStoryKind.Signing,
                StepCount = 4,
                ApplyStep = 3,
                Negotiates = true,
                Signs = _random.NextDouble() < ClubMath.NegotiationChance(GmLevel(PlayerClub)),
                Incoming = player,
                Fee = fee,
                Salary = salary
            };
            story.Titles[0] = "補強候補";
            story.Titles[1] = "GMの方針";
            story.Titles[2] = "交渉";
            story.Titles[3] = "交渉の結果";
            story.Pages[0] = "強化部が補強候補を紹介しました。\n" + Line(player)
                + "\n移籍金  " + StoryMoney(fee) + "    年俸  " + StoryMoney(salary);
            int signRate = ClubMath.RoundInt(ClubMath.NegotiationChance(GmLevel(PlayerClub)) * 100d);
            story.Pages[1] = GmName(PlayerClub) + "は獲得交渉を行います。\n獲得の見込みは "
                + signRate.ToString(CultureInfo.InvariantCulture) + "% です。";
            story.Pages[2] = (player.Name ?? "選手") + "と交渉しています。";
            story.Pages[3] = "交渉の結果を待っています。";
            ShowStoryStep(story);
            _story = story;
            return true;
        }

        /// <summary>デバッグ用。所属選手を1人選び、移籍オファーを開く。</summary>
        public bool DebugBeginTransfer()
        {
            if (_story != null || PlayerClub == null || PlayerClub.Squad == null) return false;
            var candidates = new List<Player>();
            for (int i = 0; i < PlayerClub.Squad.Count; i++)
            {
                var player = PlayerClub.Squad[i];
                if (player != null && !string.IsNullOrEmpty(player.Id))
                    candidates.Add(player);
            }

            if (candidates.Count == 0) return false;
            _story = MakeTransfer(candidates[_random.Next(candidates.Count)]);
            return _story != null;
        }

        bool SigningCanOpen(Player player)
        {
            if (player == null || PlayerClub == null) return false;
            if (SquadCount(PlayerClub) >= ClubMath.SquadLimit) return false;
            var cost = ClubMath.TransferFee(player) + ClubMath.PlayerAnnual(player);
            return PlayerClub.Reinforcement >= cost;
        }

        ClubStory MakeSigning(Player player)
        {
            if (!SigningCanOpen(player)) return null;
            var fee = ClubMath.TransferFee(player);
            var salary = ClubMath.PlayerAnnual(player);
            var story = new ClubStory
            {
                Kind = ClubStoryKind.Signing,
                StepCount = 4,
                ApplyStep = 3,
                Negotiates = true,
                Signs = _random.NextDouble() < ClubMath.NegotiationChance(GmLevel(PlayerClub)),
                Incoming = player,
                Fee = fee,
                Salary = salary
            };
            story.Titles[0] = "補強候補";
            story.Titles[1] = "GMの方針";
            story.Titles[2] = "交渉";
            story.Titles[3] = "交渉の結果";
            story.Pages[0] = "強化部が補強候補を紹介しました。\n" + Line(player)
                + "\n移籍金  " + StoryMoney(fee) + "    年俸  " + StoryMoney(salary);
            int signRate = ClubMath.RoundInt(ClubMath.NegotiationChance(GmLevel(PlayerClub)) * 100d);
            story.Pages[1] = GmName(PlayerClub) + "は獲得交渉を行います。\n獲得の見込みは "
                + signRate.ToString(CultureInfo.InvariantCulture) + "% です。";
            story.Pages[2] = (player.Name ?? "選手") + "と交渉しています。";
            story.Pages[3] = "交渉の結果を待っています。";
            ShowStoryStep(story);
            return story;
        }

        void ApplyStory()
        {
            if (_story == null || _story.Applied) return;
            _story.Applied = true;
            if (_story.Kind == ClubStoryKind.Signing)
                ApplySigning();
            else if (_story.Kind == ClubStoryKind.Transfer)
                ApplyOutgoing();
        }

        void ApplyOutgoing()
        {
            var player = FindSquad(_story.PlayerId);
            if (player == null)
            {
                _story.Pages[3] = "対象の選手は、すでにチームにいません。";
                return;
            }

            if (SquadCount(PlayerClub) <= ClubMath.MinimumSquad)
            {
                _story.Pages[3] = "所属が15人のため、放出できませんでした。";
                return;
            }

            if (_story.Leaves)
            {
                PlayerClub.Squad.Remove(player);
                bool closed = IsClosedPeriod(Time.CurrentDate);
                if (closed)
                    Economy.Apply(PlayerClub, _story.Fee, "fee-" + player.Id);
                else
                    Economy.CreditReinforcement(PlayerClub, _story.Fee, "fee-" + player.Id);
                string purse = closed ? "資産" : "強化費";
                _story.Pages[3] = (player.Name ?? "選手") + "の移籍が決まりました。\n移籍金  "
                    + StoryMoney(_story.Fee) + " が" + purse + "に入りました。";
                return;
            }

            PayRetain(player.Id, _story.Retain);
            string paid = _story.Retain > 0
                ? "\n年俸の" + _story.RetainPercent + "%にあたる " + StoryMoney(_story.Retain) + " を支払いました。"
                : "";
            _story.Pages[3] = (player.Name ?? "選手") + "は残留することになりました。" + paid;
        }

        void ApplySigning()
        {
            var player = LiveFreeAgent(_story.Incoming);
            _story.Incoming = player;
            if (player == null)
            {
                _story.Pages[_story.ApplyStep] = "候補がいません。";
                return;
            }

            int page = _story.ApplyStep;
            if (SquadCount(PlayerClub) >= ClubMath.SquadLimit)
            {
                _story.Pages[page] = "所属が25人に達したため、加入できませんでした。";
                return;
            }

            if (!_story.Signs)
            {
                _story.Pages[page] = "交渉は成立しませんでした。";
                return;
            }

            var cost = _story.Fee + _story.Salary;
            if (!Economy.TrySpendReinforcement(PlayerClub, cost, "sign-" + player.Id))
            {
                _story.Pages[page] = "強化費が足りず、交渉は成立しませんでした。";
                return;
            }

            player.ContractSalary = _story.Salary;
            PlayerClub.Squad.Add(player);
            DetachSignedFreeAgents();
            _story.Pages[page] = (player.Name ?? "選手") + "の加入が決まりました。\n移籍金と年俸、合計 "
                + StoryMoney(cost) + " を強化費から支払いました。";
        }

        void PayRetain(string playerId, BigInteger cost)
        {
            if (cost <= 0 || PlayerClub == null) return;
            var fromBudget = PlayerClub.Reinforcement > cost ? cost : PlayerClub.Reinforcement;
            if (fromBudget < 0) fromBudget = BigInteger.Zero;
            if (fromBudget > 0)
                Economy.TrySpendReinforcement(PlayerClub, fromBudget, "retain-" + playerId);
            var rest = cost - fromBudget;
            if (rest > 0)
                Economy.Apply(PlayerClub, -rest, "retain-" + playerId);
        }

        void ShowStoryStep()
        {
            ShowStoryStep(_story);
        }

        static void ShowStoryStep(ClubStory story)
        {
            if (story == null) return;
            int step = story.Step;
            if (step < 0) step = 0;
            if (step > 3) step = 3;
            story.Title = story.Titles[step] ?? "";
            story.Body = story.Pages[step] ?? "";
        }

        void ResetWindowAppearances()
        {
            for (int c = 0; c < Clubs.Count; c++)
            {
                var club = Clubs[c];
                if (club == null) continue;
                club.MatchesSinceWindow = 0;
                if (club.Squad == null) continue;
                for (int i = 0; i < club.Squad.Count; i++)
                {
                    if (club.Squad[i] != null) club.Squad[i].Appearances = 0;
                }
            }
        }

        static bool IsLowAppearance(Club club, Player player)
        {
            return club != null
                && player != null
                && club.MatchesSinceWindow > 0
                && (double)player.Appearances / club.MatchesSinceWindow < LowAppearanceRate;
        }

        int CompareOutgoing(Club club, HashSet<string> onSheet, Dictionary<string, int> noise, Player a, Player b)
        {
            int rank = RankOutgoing(club, onSheet, a).CompareTo(RankOutgoing(club, onSheet, b));
            if (rank != 0) return rank;
            int noiseA;
            int noiseB;
            noise.TryGetValue(a.Id, out noiseA);
            noise.TryGetValue(b.Id, out noiseB);
            return noiseA.CompareTo(noiseB);
        }

        int RankOutgoing(Club club, HashSet<string> onSheet, Player player)
        {
            bool low = IsLowAppearance(club, player);
            bool sheet = onSheet != null && onSheet.Contains(player.Id);
            if (low && !sheet && player.Phase == GrowthPhase.Veteran) return 0;
            if (low && !sheet && player.Phase == GrowthPhase.Prime) return 1;
            if (low && !sheet && player.Phase == GrowthPhase.Youth) return 2;
            if (low) return 3;
            return 4;
        }

        HashSet<string> SheetIds(Club club)
        {
            var ids = new HashSet<string>();
            if (club == null) return ids;
            var sheet = new ManagerLineupAi().BuildSheet(club);
            if (sheet == null) return ids;
            if (sheet.StartingXi != null)
            {
                for (int i = 0; i < sheet.StartingXi.Length; i++)
                {
                    var slot = sheet.StartingXi[i];
                    if (slot != null && slot.Player != null && !string.IsNullOrEmpty(slot.Player.Id))
                        ids.Add(slot.Player.Id);
                }
            }

            if (sheet.Bench == null) return ids;
            for (int i = 0; i < sheet.Bench.Length; i++)
            {
                var player = sheet.Bench[i];
                if (player != null && !string.IsNullOrEmpty(player.Id))
                    ids.Add(player.Id);
            }

            return ids;
        }

        static double SquadAverage(Club club)
        {
            if (club == null || club.Squad == null || club.Squad.Count == 0) return 0d;
            double total = 0d;
            int count = 0;
            for (int i = 0; i < club.Squad.Count; i++)
            {
                var player = club.Squad[i];
                if (player == null) continue;
                total += player.Overall;
                count++;
            }

            return count == 0 ? 0d : total / count;
        }

        static int SquadCount(Club club)
        {
            if (club == null || club.Squad == null) return 0;
            int count = 0;
            for (int i = 0; i < club.Squad.Count; i++)
            {
                if (club.Squad[i] != null) count++;
            }

            return count;
        }

        Player FindSquad(string id)
        {
            if (PlayerClub == null || PlayerClub.Squad == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < PlayerClub.Squad.Count; i++)
            {
                var player = PlayerClub.Squad[i];
                if (player != null && player.Id == id) return player;
            }

            return null;
        }

        static string Line(Player player)
        {
            if (player == null) return "選手";
            return (player.Name ?? "選手")
                + "（" + player.Age + "歳・" + PhaseName(player.Phase)
                + "・総合" + player.DisplayOverall.ToString(CultureInfo.InvariantCulture) + "）";
        }

        static string PhaseName(GrowthPhase phase)
        {
            switch (phase)
            {
                case GrowthPhase.Youth: return "若手";
                case GrowthPhase.Prime: return "中堅";
                default: return "ベテラン";
            }
        }

        static int ScoutLevel(Club club)
        {
            if (club == null || club.Front == null) return 0;
            return club.Front.StrengtheningLevel();
        }

        static string GmName(Club club)
        {
            var gm = club != null && club.Front != null ? club.Front.GeneralManager : null;
            if (gm != null && !string.IsNullOrEmpty(gm.Name)) return gm.Name;
            return "GM";
        }

        static int GmLevel(Club club)
        {
            var gm = club != null && club.Front != null ? club.Front.GeneralManager : null;
            return gm != null ? gm.Level : 0;
        }

        static string StoryMoney(BigInteger amount)
        {
            return MoneyText.Format(amount);
        }

        void Shuffle<T>(IList<T> list)
        {
            if (list == null) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                T swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }

        void WritePersonnel(GameSaveFile file)
        {
            if (file == null) return;
            file.PersonnelWindowKey = _windowKey;
            file.WindowStartTicks = _windowStart.Ticks;
            file.WindowEndTicks = _windowEnd.Ticks;
            file.BudgetWindowKey = _budgetWindowKey;
            file.AnnouncedWinterKey = _announcedWinterKey;
            file.GreetedYear = _greetedYear;
            file.TicketKey = _ticketKey;
            file.CampKey = _campKey;
            file.RenewalKey = _renewalKey;
            file.StaffOfferMonth = _staffOfferMonth;
            file.StaffOfferDay = _staffOfferDay;
            file.StaffOfferRole = _staffOfferRole;
            file.StaffOfferLevel = _staffOfferLevel;
            file.StaffOfferName = _staffOfferName;
            file.StaffOfferDone = _staffOfferDone;
            file.StadiumProposal = _stadiumProposal;
            file.StadiumAskedLevel = _stadiumAskedLevel;
            file.Deals = WriteDeals();
            file.Story = WriteStory(_story);
            file.FreeAgents = WritePlayers(_freeAgents);
            file.FreeAgentSerial = _freeAgentSerial;
        }

        void ReadPersonnel(GameSaveFile file)
        {
            _deals.Clear();
            _story = null;
            if (file == null) return;
            _windowKey = file.PersonnelWindowKey;
            _windowStart = file.WindowStartTicks > 0 ? new DateTime(file.WindowStartTicks) : default;
            _windowEnd = file.WindowEndTicks > 0 ? new DateTime(file.WindowEndTicks) : default;
            _budgetWindowKey = file.BudgetWindowKey;
            _announcedWinterKey = file.AnnouncedWinterKey;
            _greetedYear = file.GreetedYear;
            _ticketKey = file.TicketKey;
            _campKey = file.CampKey;
            _renewalKey = file.RenewalKey;
            _staffOfferMonth = file.StaffOfferMonth;
            _staffOfferDay = file.StaffOfferDay;
            _staffOfferRole = file.StaffOfferRole;
            _staffOfferLevel = file.StaffOfferLevel;
            _staffOfferName = file.StaffOfferName;
            _staffOfferDone = file.StaffOfferDone;
            _stadiumProposal = file.StadiumProposal;
            _stadiumAskedLevel = file.StadiumAskedLevel;
            int count = file.Deals == null ? 0 : file.Deals.Length;
            for (int i = 0; i < count; i++)
            {
                var save = file.Deals[i];
                if (save == null || save.DateTicks <= 0) continue;
                var deal = new ScheduledDeal
                {
                    Date = new DateTime(save.DateTicks),
                    Incoming = save.Incoming,
                    PlayerId = save.PlayerId,
                    Target = ReadOnePlayer(save.Player)
                };
                _deals.Add(deal);
            }

            _story = ReadStory(file.Story);
            _freeAgents.Clear();
            var agents = ReadPlayers(file.FreeAgents);
            if (agents != null && agents.Count > 0)
            {
                _freeAgents.AddRange(agents);
                _freeAgentSerial = file.FreeAgentSerial > _freeAgents.Count ? file.FreeAgentSerial : _freeAgents.Count;
            }
            else
            {
                EnsureFreeAgents();
            }

            RetireVeteranFreeAgents();
        }

        void RetireVeteranFreeAgents()
        {
            for (int i = 0; i < _freeAgents.Count; i++)
            {
                var player = _freeAgents[i];
                if (player != null && player.Age > ClubMath.FreeAgentMaxAge)
                    ResetFreeAgentCareer(player);
            }
        }

        WindowDealSave[] WriteDeals()
        {
            var result = new WindowDealSave[_deals.Count];
            for (int i = 0; i < _deals.Count; i++)
            {
                var deal = _deals[i];
                result[i] = new WindowDealSave
                {
                    DateTicks = deal.Date.Ticks,
                    Incoming = deal.Incoming,
                    PlayerId = deal.PlayerId,
                    Player = WriteOnePlayer(deal.Target)
                };
            }

            return result;
        }

        static ClubStorySave WriteStory(ClubStory story)
        {
            if (story == null) return null;
            return new ClubStorySave
            {
                Kind = (int)story.Kind,
                Step = story.Step,
                StepCount = story.StepCount,
                ApplyStep = story.ApplyStep,
                Applied = story.Applied,
                Budget = story.Budget,
                BudgetPercent = story.BudgetPercent,
                Leaves = story.Leaves,
                Signs = story.Signs,
                Negotiates = story.Negotiates,
                WindowKey = story.WindowKey,
                PlayerId = story.PlayerId,
                Fee = Money(story.Fee),
                Retain = Money(story.Retain),
                Salary = Money(story.Salary),
                Title0 = At(story.Titles, 0),
                Title1 = At(story.Titles, 1),
                Title2 = At(story.Titles, 2),
                Title3 = At(story.Titles, 3),
                Page0 = At(story.Pages, 0),
                Page1 = At(story.Pages, 1),
                Page2 = At(story.Pages, 2),
                Page3 = At(story.Pages, 3),
                Incoming = WriteOnePlayer(story.Incoming)
            };
        }

        ClubStory ReadStory(ClubStorySave save)
        {
            if (save == null || save.StepCount <= 0) return null;
            var story = new ClubStory
            {
                Kind = (ClubStoryKind)save.Kind,
                Step = save.Step,
                StepCount = save.StepCount,
                ApplyStep = save.ApplyStep,
                Applied = save.Applied,
                Budget = save.Budget,
                BudgetPercent = save.BudgetPercent < 0 ? 50 : save.BudgetPercent,
                Leaves = save.Leaves,
                Signs = save.Signs,
                Negotiates = save.Negotiates,
                WindowKey = save.WindowKey,
                PlayerId = save.PlayerId,
                Fee = ReadMoney(save.Fee),
                Retain = ReadMoney(save.Retain),
                Salary = ReadMoney(save.Salary),
                Incoming = ReadOnePlayer(save.Incoming)
            };
            story.Titles[0] = save.Title0;
            story.Titles[1] = save.Title1;
            story.Titles[2] = save.Title2;
            story.Titles[3] = save.Title3;
            story.Pages[0] = save.Page0;
            story.Pages[1] = save.Page1;
            story.Pages[2] = save.Page2;
            story.Pages[3] = save.Page3;
            story.RetainPercent = PercentOf(story.Salary, story.Retain);
            ShowStoryStep(story);
            return story;
        }

        static int PercentOf(BigInteger salary, BigInteger retain)
        {
            if (salary <= 0 || retain <= 0) return 0;
            double ratio = ClubMath.ToDouble(retain) / ClubMath.ToDouble(salary);
            if (ratio >= 0.2d) return 30;
            return 10;
        }

        static string At(string[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length) return null;
            return values[index];
        }

        static PlayerSave WriteOnePlayer(Player player)
        {
            if (player == null) return null;
            var list = new List<Player> { player };
            var saves = WritePlayers(list);
            return saves != null && saves.Length > 0 ? saves[0] : null;
        }

        static Player ReadOnePlayer(PlayerSave save)
        {
            if (save == null) return null;
            var players = ReadPlayers(new[] { save });
            return players.Count > 0 ? players[0] : null;
        }
    }
}
