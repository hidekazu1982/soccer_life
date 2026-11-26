using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using SoccerClub.Data;
using SoccerClub.Personnel;

namespace SoccerClub.Core
{
    /// <summary>
    /// 季節のパーティ（Parties.csv）。その日の朝に開き、Step 0 で招待する選手を選ぶ（ClubEventView）。
    /// 確定（ContinueStory）で費用を払い、効果を入れ、Step 1 のパーティの場面（MeetingRoomView）へ進む。
    /// オートと高速シミュレーターでは、開いたときの「おすすめ」（出場の少ない順）のまま確定する。
    /// WindowKey は「パーティの id|会話の番号|B の id」（会話が無ければ番号は -1）。PlayerId は会話の A。
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>会話に出た組に足す親密度。</summary>
        public const int PartyTalkBonus = 8;

        /// <summary>最後に開いたパーティ（年:id）。</summary>
        string _partyKey;

        /// <summary>パーティに招待されて移籍しにくくなっている選手と、その期限と割合。</summary>
        readonly Dictionary<string, PartyLoyalty> _partyLoyal = new Dictionary<string, PartyLoyalty>();

        struct PartyLoyalty
        {
            public DateTime Until;
            public int Percent;
        }

        /// <summary>開いているパーティ。無ければ null。</summary>
        public PartyCatalog.Party OpenParty
        {
            get
            {
                if (_story == null || _story.Kind != ClubStoryKind.Party) return null;
                return PartyCatalog.Find(PartyIdOf(_story));
            }
        }

        public static string PartyIdOf(ClubStory story)
        {
            if (story == null || string.IsNullOrEmpty(story.WindowKey)) return null;
            return story.WindowKey.Split('|')[0];
        }

        bool TryOpenParty(DateTime today)
        {
            var party = PartyCatalog.On(today);
            if (party == null || PlayerClub == null || PlayerClub.Squad == null || PlayerClub.Squad.Count == 0) return false;
            string key = today.Year.ToString(CultureInfo.InvariantCulture) + ":" + party.Id;
            if (_partyKey == key) return false;
            _partyKey = key;
            OpenPartyStory(party);
            return true;
        }

        /// <summary>
        /// デバッグ用。日付と「その年にもう開いたか」を無視して、指定したパーティを今すぐ開く。
        /// _partyKey は変えないので、本来の日のパーティもそのまま開く。費用と効果は通常どおり入る。
        /// </summary>
        public bool DebugOpenParty(string partyId)
        {
            if (_story != null || GameOver || PlayerClub == null || PlayerClub.Squad == null || PlayerClub.Squad.Count == 0) return false;
            var party = PartyCatalog.Find(partyId);
            if (party == null) return false;
            OpenPartyStory(party);
            return true;
        }

        void OpenPartyStory(PartyCatalog.Party party)
        {
            var story = new ClubStory
            {
                Kind = ClubStoryKind.Party,
                WindowKey = party.Id + "|-1|",
                StepCount = 2
            };
            story.Titles[0] = party.Title;
            story.Titles[1] = party.Title;
            story.Pages[0] = party.Title + "を開きます。招待する選手を" + party.Slots.ToString(CultureInfo.InvariantCulture)
                + "人まで選んでください。費用は1人あたり年俸の" + party.CostPercent.ToString("0.#", CultureInfo.InvariantCulture) + "%です。";
            story.Pages[1] = "";
            RecommendPartyGuests(story);
            ShowStoryStep(story);
            _story = story;
        }

        /// <summary>秘書のおすすめ。出場の少ない順に、払える範囲で枠いっぱいまで。</summary>
        public void RecommendPartyGuests(ClubStory story)
        {
            var party = story != null ? PartyCatalog.Find(PartyIdOf(story)) : null;
            if (party == null || PlayerClub == null) return;
            story.Invited.Clear();
            var list = new List<Player>();
            for (int i = 0; i < PlayerClub.Squad.Count; i++)
            {
                var p = PlayerClub.Squad[i];
                if (p != null && !string.IsNullOrEmpty(p.Id)) list.Add(p);
            }

            list.Sort((x, y) =>
            {
                int c = x.Appearances.CompareTo(y.Appearances);
                return c != 0 ? c : string.CompareOrdinal(x.Id, y.Id);
            });

            var cost = BigInteger.Zero;
            for (int i = 0; i < list.Count && story.Invited.Count < party.Slots; i++)
            {
                var add = PartyCostOf(party, list[i]);
                if (cost + add > PlayerClub.Coins) continue;
                cost += add;
                story.Invited.Add(list[i].Id);
            }
        }

        /// <summary>招待を入れ替える。枠がいっぱいなら追加しない。追加できなかったら false。</summary>
        public bool TogglePartyGuest(ClubStory story, string playerId)
        {
            var party = story != null ? PartyCatalog.Find(PartyIdOf(story)) : null;
            if (party == null || string.IsNullOrEmpty(playerId) || story.Step != 0) return false;
            if (story.Invited.Remove(playerId)) return true;
            if (story.Invited.Count >= party.Slots) return false;
            story.Invited.Add(playerId);
            return true;
        }

        public BigInteger PartyCost(ClubStory story)
        {
            var party = story != null ? PartyCatalog.Find(PartyIdOf(story)) : null;
            if (party == null || PlayerClub == null) return BigInteger.Zero;
            var total = BigInteger.Zero;
            for (int i = 0; i < story.Invited.Count; i++)
            {
                var p = SquadPlayer(story.Invited[i]);
                if (p != null) total += PartyCostOf(party, p);
            }

            return total;
        }

        /// <summary>この選手を招待したときの費用（年俸の CostPercent%）。招待画面の詳細に出す。</summary>
        public BigInteger PartyGuestCost(ClubStory story, Player player)
        {
            var party = story != null ? PartyCatalog.Find(PartyIdOf(story)) : null;
            return party == null || player == null ? BigInteger.Zero : PartyCostOf(party, player);
        }

        public bool CanAffordParty(ClubStory story)
        {
            return PlayerClub != null && PartyCost(story) <= PlayerClub.Coins;
        }

        static BigInteger PartyCostOf(PartyCatalog.Party party, Player player)
        {
            var annual = ClubMath.PlayerAnnual(player);
            return ClubMath.RoundCoins(ClubMath.ToDouble(annual) * party.CostPercent / 100d);
        }

        Player SquadPlayer(string id)
        {
            if (PlayerClub == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < PlayerClub.Squad.Count; i++)
            {
                var p = PlayerClub.Squad[i];
                if (p != null && p.Id == id) return p;
            }

            return null;
        }

        /// <summary>
        /// 招待を確定する。払えなければ、払える人数まで招待を後ろから減らす。招待が0人なら開かない（場面も出さない）。
        /// 戻り値は、パーティの場面へ進むなら true。
        /// </summary>
        bool ConfirmParty(ClubStory story)
        {
            var party = PartyCatalog.Find(PartyIdOf(story));
            if (party == null || PlayerClub == null) return false;

            var guests = new List<Player>();
            for (int i = 0; i < story.Invited.Count && guests.Count < party.Slots; i++)
            {
                var p = SquadPlayer(story.Invited[i]);
                if (p != null && !guests.Contains(p)) guests.Add(p);
            }

            var cost = BigInteger.Zero;
            for (int i = 0; i < guests.Count; i++) cost += PartyCostOf(party, guests[i]);
            while (guests.Count > 0 && cost > PlayerClub.Coins)
            {
                cost -= PartyCostOf(party, guests[guests.Count - 1]);
                guests.RemoveAt(guests.Count - 1);
            }

            if (guests.Count == 0)
            {
                story.Invited.Clear();
                return false;
            }

            if (cost > 0 && !Economy.TrySpend(PlayerClub, cost, "party"))
                return false;

            story.Invited.Clear();
            for (int i = 0; i < guests.Count; i++) story.Invited.Add(guests[i].Id);

            var today = Time.CurrentDate.Date;
            // 調子と能力。
            int statGrown = 0;
            for (int i = 0; i < guests.Count; i++)
            {
                var p = guests[i];
                double mult = p.Age <= 21 ? party.YoungMultiplier : 1d;
                int condition = (int)Math.Round(party.Condition * mult);
                p.Condition = Math.Min(100, p.Condition + condition);
                if (party.Stat >= 0)
                {
                    double gain = ExtraPracticeGain(p, party.Stat) * mult;
                    if (gain > 0d)
                    {
                        ApplyStat(p, party.Stat, gain);
                        statGrown++;
                    }
                }

                if (party.LeavePercent > 0)
                    _partyLoyal[p.Id] = new PartyLoyalty { Until = CurrentSeasonStart.AddYears(1), Percent = party.LeavePercent };
            }

            // 親密度。招待した人同士の全ペア＋組のボーナス。
            int pairs = 0;
            int partners = 0;
            var pairCasts = new List<SeasonalCatalog.Cast>();
            var coupleCasts = new List<SeasonalCatalog.Cast>();
            for (int i = 0; i < guests.Count; i++)
            {
                for (int j = i + 1; j < guests.Count; j++)
                {
                    var a = guests[i];
                    var b = guests[j];
                    var rel = _relations.Find(a.Id, b.Id);
                    var stage = rel != null ? rel.Stage : RelationStage.None;
                    double mult = a.Age <= 21 && b.Age <= 21 ? party.YoungMultiplier : 1d;
                    int amount = party.Affinity;
                    if (RelationshipService.IsPartner(stage))
                    {
                        amount += party.PartnerBonus;
                        partners++;
                        coupleCasts.Add(new SeasonalCatalog.Cast { A = a, B = b, Stage = stage });
                    }
                    else if (stage == RelationStage.Friend || stage == RelationStage.BestFriend)
                    {
                        amount += party.FriendBonus;
                        pairCasts.Add(new SeasonalCatalog.Cast { A = a, B = b, Stage = stage });
                    }

                    amount = (int)Math.Round(amount * mult);
                    if (amount <= 0) continue;
                    _relations.AddAffinity(a.Id, b.Id, amount, today);
                    pairs++;
                }
            }

            // 会話（招待した人の中から）。会話に出た組は親密度をさらに足す。
            int seed = today.Year * 977;
            for (int i = 0; i < party.Id.Length; i++) seed = unchecked(seed * 31 + party.Id[i]);
            var random = new Random(seed & int.MaxValue);
            var solos = new List<Player>(guests);
            solos.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
            SeasonalCatalog.Script script;
            SeasonalCatalog.Cast cast;
            var talkSeason = new SeasonalCatalog.Season { Id = party.Id };
            if (SeasonalCatalog.TryChoose(talkSeason, solos, pairCasts, coupleCasts, random, out script, out cast))
            {
                if (cast.B != null && random.Next(2) == 0)
                {
                    var swap = cast.A;
                    cast.A = cast.B;
                    cast.B = swap;
                }

                story.PlayerId = cast.A.Id;
                story.WindowKey = party.Id + "|" + script.Index.ToString(CultureInfo.InvariantCulture) + "|" + (cast.B != null ? cast.B.Id : "");
                if (cast.B != null) _relations.AddAffinity(cast.A.Id, cast.B.Id, PartyTalkBonus, today);
            }
            else
            {
                story.PlayerId = guests[0].Id;
                story.WindowKey = party.Id + "|-1|";
            }

            var result = guests.Count.ToString(CultureInfo.InvariantCulture) + "人の調子が上がり、"
                + pairs.ToString(CultureInfo.InvariantCulture) + "組の親密度が上がりました。";
            if (partners > 0) result += "恋人・夫婦の" + partners.ToString(CultureInfo.InvariantCulture) + "組は特に仲が深まりました。";
            if (statGrown > 0) result += PartyCatalog.StatLabel(party.Stat) + "も伸びました。";
            result += "（費用 " + MoneyText.Format(cost) + "）";
            story.Pages[1] = result;
            story.Step = 1;
            ShowStoryStep();
            NotifyChanged();
            return true;
        }

        /// <summary>パーティに招待された選手の、移籍のしやすさの倍率（1 は変わらない）。</summary>
        double PartyLeaveMultiplier(string playerId, DateTime today)
        {
            PartyLoyalty loyalty;
            if (string.IsNullOrEmpty(playerId) || !_partyLoyal.TryGetValue(playerId, out loyalty)) return 1d;
            if (today >= loyalty.Until)
            {
                _partyLoyal.Remove(playerId);
                return 1d;
            }

            return 1d - loyalty.Percent / 100d;
        }

        void WriteParty(GameSaveFile file)
        {
            file.PartyKey = _partyKey;
            var ids = new List<string>();
            var until = new List<long>();
            var percent = new List<int>();
            foreach (var pair in _partyLoyal)
            {
                ids.Add(pair.Key);
                until.Add(pair.Value.Until.Ticks);
                percent.Add(pair.Value.Percent);
            }

            file.PartyLoyalIds = ids.ToArray();
            file.PartyLoyalUntil = until.ToArray();
            file.PartyLoyalPercent = percent.ToArray();
        }

        void ReadParty(GameSaveFile file)
        {
            _partyKey = file.PartyKey;
            _partyLoyal.Clear();
            if (file.PartyLoyalIds == null || file.PartyLoyalUntil == null || file.PartyLoyalPercent == null) return;
            int count = Math.Min(file.PartyLoyalIds.Length, Math.Min(file.PartyLoyalUntil.Length, file.PartyLoyalPercent.Length));
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(file.PartyLoyalIds[i])) continue;
                _partyLoyal[file.PartyLoyalIds[i]] = new PartyLoyalty
                {
                    Until = new DateTime(file.PartyLoyalUntil[i]),
                    Percent = file.PartyLoyalPercent[i]
                };
            }
        }
    }
}
