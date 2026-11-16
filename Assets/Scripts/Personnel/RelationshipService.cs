using System;
using System.Collections.Generic;
using SoccerClub.Core;
using SoccerClub.Data;

namespace SoccerClub.Personnel
{
    /// <summary>
    /// 選手同士の関係。異性は 友達→恋人→夫婦、同性は 友達→親友。毎月1日に親密度を更新し、段階を進める。
    /// EventClubId のクラブ（プレイヤーのクラブ）だけは、友達になるのをメイン画面の友達イベントに任せる。
    /// プレイヤーは操作せず見守る。乱数は使わず、id と月から値を決める。
    /// </summary>
    public sealed class RelationshipService
    {
        public const int MaxAffinity = 100;
        public const int FriendThreshold = 30;

        /// <summary>友達から戻る値。境目で毎月行き来しないよう少し下げる。</summary>
        public const int FriendLossThreshold = 25;

        public const int LoverThreshold = 60;

        /// <summary>恋人がこの値を下回ると別れて友達に戻る。</summary>
        public const int BreakUpThreshold = 40;

        public const int MarriageThreshold = 90;

        /// <summary>同性の友達がこの値以上で親友になる（恋人と同じ値）。</summary>
        public const int BestFriendThreshold = 60;

        /// <summary>親友がこの値を下回ると友達に戻る（別れと同じ値）。</summary>
        public const int BestFriendLossThreshold = 40;

        /// <summary>夫婦になるまでに、恋人でいる最短の月数（1シーズン）。</summary>
        public const int MarriageMonths = 12;

        /// <summary>恋人・夫婦になれる年齢。</summary>
        public const int AdultAge = 18;

        /// <summary>夫婦は離れて暮らしても、この値より下がらない（離婚は初版では無し）。</summary>
        public const int MarriedFloor = 60;

        /// <summary>別のクラブ（無所属・引退を含む）になったときの毎月の減少。</summary>
        public const int SeparatedDecay = 2;

        /// <summary>同じクラブの毎月の増減：相性³×CompatibilityGain − GainOffset ＋ 年齢・ポジション・ぶれ。</summary>
        public const double CompatibilityGain = 10d;

        public const double GainOffset = 7d;

        public const int NoiseRange = 3;

        /// <summary>配偶者がいるクラブへの加入は、年俸をこの割合だけ割り引いて受ける。</summary>
        public const double SpouseSalaryDiscount = 0.2d;

        /// <summary>配偶者と別のクラブになる移籍は、移籍する確率にこの値を掛ける（断りやすくなる）。</summary>
        public const double SpouseLeaveFactor = 0.5d;

        readonly List<Relationship> _list = new List<Relationship>();
        readonly Dictionary<(string, string), Relationship> _byPair = new Dictionary<(string, string), Relationship>();

        /// <summary>
        /// このクラブのトップチーム同士は、条件を満たしても段階が自動では進まない（友達・恋人・夫婦・親友）。
        /// メイン画面の関係イベント（PromoteByEvent）で進む。後退（別れ・解消）は自動のまま。null なら全クラブ自動。
        /// </summary>
        public string EventClubId { get; set; }

        /// <summary>段階が変わったとき。2つ目の引数は変わる前の段階。通知・UI用。</summary>
        public event Action<Relationship, RelationStage> StageChanged;

        public IReadOnlyList<Relationship> All => _list;

        /// <summary>2人の関係。無ければ null。</summary>
        public Relationship Find(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b) return null;
            Relationship.Order(a, b, out var first, out var second);
            _byPair.TryGetValue((first, second), out var found);
            return found;
        }

        /// <summary>2人の関係の段階。関係が無ければ None。</summary>
        public RelationStage StageBetween(string a, string b)
        {
            var rel = Find(a, b);
            return rel != null ? rel.Stage : RelationStage.None;
        }

        /// <summary>配偶者が club のトップチームにいれば、その選手。いなければ null。</summary>
        public Player SpouseIn(Club club, Player player)
        {
            if (club == null || club.Squad == null || player == null) return null;
            string spouseId = PartnerOf(player.Id, out var stage);
            if (stage != RelationStage.Married || string.IsNullOrEmpty(spouseId)) return null;
            for (int i = 0; i < club.Squad.Count; i++)
            {
                var member = club.Squad[i];
                if (member != null && member.Id == spouseId) return member;
            }

            return null;
        }

        /// <summary>この選手の関係（None は除く）。親密度の高い順。</summary>
        public List<Relationship> For(string playerId)
        {
            var result = new List<Relationship>();
            if (string.IsNullOrEmpty(playerId)) return result;
            for (int i = 0; i < _list.Count; i++)
            {
                var rel = _list[i];
                if (rel.Stage != RelationStage.None && rel.Involves(playerId)) result.Add(rel);
            }

            result.Sort((x, y) => y.Affinity.CompareTo(x.Affinity));
            return result;
        }

        /// <summary>恋人・夫婦（どちらか1人だけの相手）か。親友は何人いてもよいので含めない。</summary>
        public static bool IsPartner(RelationStage stage)
        {
            return stage == RelationStage.Lover || stage == RelationStage.Married;
        }

        /// <summary>
        /// club のトップチームにいる恋人・夫婦の組を dest に入れる（両方の id から相手の id を引ける）。
        /// メイン画面で一緒に歩かせるのに使う。
        /// </summary>
        public void CopyCouples(Club club, Dictionary<string, string> dest)
        {
            if (dest == null) return;
            dest.Clear();
            if (club == null || club.Squad == null) return;
            var members = new HashSet<string>();
            for (int i = 0; i < club.Squad.Count; i++)
            {
                var p = club.Squad[i];
                if (p != null && !string.IsNullOrEmpty(p.Id)) members.Add(p.Id);
            }

            for (int i = 0; i < _list.Count; i++)
            {
                var rel = _list[i];
                if (!IsPartner(rel.Stage)) continue;
                if (!members.Contains(rel.PlayerIdA) || !members.Contains(rel.PlayerIdB)) continue;
                dest[rel.PlayerIdA] = rel.PlayerIdB;
                dest[rel.PlayerIdB] = rel.PlayerIdA;
            }
        }

        /// <summary>関係イベントを待っている組と、イベントで進む先の段階。</summary>
        public struct EventCandidate
        {
            public Relationship Rel;
            public RelationStage Next;
        }

        /// <summary>
        /// club のトップチーム同士で、段階が進む条件を満たしてイベントを待っている組。
        /// 友達（None→30以上）、親友（同性の友達→60以上）、恋人（異性の友達→60以上・18歳以上・お互い相手なし）、
        /// 夫婦（恋人→90以上・恋人になって12か月以上）。
        /// </summary>
        public List<EventCandidate> EventCandidates(Club club, DateTime today)
        {
            var result = new List<EventCandidate>();
            if (club == null || club.Squad == null) return result;
            var members = new Dictionary<string, Player>();
            for (int i = 0; i < club.Squad.Count; i++)
            {
                var p = club.Squad[i];
                if (p != null && !string.IsNullOrEmpty(p.Id)) members[p.Id] = p;
            }

            var partnered = new HashSet<string>();
            for (int i = 0; i < _list.Count; i++)
            {
                var rel = _list[i];
                if (!IsPartner(rel.Stage)) continue;
                partnered.Add(rel.PlayerIdA);
                partnered.Add(rel.PlayerIdB);
            }

            for (int i = 0; i < _list.Count; i++)
            {
                var rel = _list[i];
                if (!members.TryGetValue(rel.PlayerIdA, out var a) || !members.TryGetValue(rel.PlayerIdB, out var b)) continue;
                var next = NextStage(rel, a, b, partnered, today);
                if (next != RelationStage.None) result.Add(new EventCandidate { Rel = rel, Next = next });
            }

            return result;
        }

        /// <summary>条件を満たしていれば進む先の段階。進めなければ None。</summary>
        static RelationStage NextStage(Relationship rel, Player a, Player b, HashSet<string> partnered, DateTime today)
        {
            switch (rel.Stage)
            {
                case RelationStage.None:
                    return rel.Affinity >= FriendThreshold ? RelationStage.Friend : RelationStage.None;
                case RelationStage.Friend:
                    if (a.Gender == b.Gender)
                        return rel.Affinity >= BestFriendThreshold ? RelationStage.BestFriend : RelationStage.None;
                    if (rel.Affinity < LoverThreshold || a.Age < AdultAge || b.Age < AdultAge) return RelationStage.None;
                    if (partnered.Contains(a.Id) || partnered.Contains(b.Id)) return RelationStage.None;
                    return RelationStage.Lover;
                case RelationStage.Lover:
                    return rel.Affinity >= MarriageThreshold && MonthsSince(rel.Since, today) >= MarriageMonths
                        ? RelationStage.Married
                        : RelationStage.None;
                default:
                    return RelationStage.None;
            }
        }

        /// <summary>関係イベントの結果。いまも条件を満たしているときだけ段階を進める。</summary>
        public bool PromoteByEvent(Club club, string a, string b, RelationStage next, DateTime today)
        {
            var list = EventCandidates(club, today);
            for (int i = 0; i < list.Count; i++)
            {
                var rel = list[i].Rel;
                if (list[i].Next != next || !rel.Involves(a) || !rel.Involves(b) || a == b) continue;
                ChangeStage(rel, next, today);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 古いセーブで同性の恋人・夫婦になっている組を親友に直す。ロード直後に呼ぶ。
        /// </summary>
        public void ApplyGenderRules(IEnumerable<Player> players)
        {
            if (players == null) return;
            var gender = new Dictionary<string, Gender>();
            foreach (var p in players)
            {
                if (p != null && !string.IsNullOrEmpty(p.Id)) gender[p.Id] = p.Gender;
            }

            for (int i = 0; i < _list.Count; i++)
            {
                var rel = _list[i];
                if (!IsPartner(rel.Stage)) continue;
                if (!gender.TryGetValue(rel.PlayerIdA, out var ga) || !gender.TryGetValue(rel.PlayerIdB, out var gb)) continue;
                if (ga == gb) rel.Stage = RelationStage.BestFriend;
            }
        }

        /// <summary>恋人か配偶者の id。いなければ null。</summary>
        public string PartnerOf(string playerId, out RelationStage stage)
        {
            stage = RelationStage.None;
            if (string.IsNullOrEmpty(playerId)) return null;
            for (int i = 0; i < _list.Count; i++)
            {
                var rel = _list[i];
                if (!IsPartner(rel.Stage) || !rel.Involves(playerId)) continue;
                stage = rel.Stage;
                return rel.Other(playerId);
            }

            return null;
        }

        /// <summary>同じ id が別人になったとき（無所属のキャリアやり直しなど）に、その id の関係を消す。</summary>
        public void Forget(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                var rel = _list[i];
                if (!rel.Involves(playerId)) continue;
                _byPair.Remove((rel.PlayerIdA, rel.PlayerIdB));
                _list.RemoveAt(i);
            }
        }

        /// <summary>毎月1日に呼ぶ。clubs の所属選手どうしの親密度を上げ、離れた2人は下げる。</summary>
        public void MonthlyUpdate(IReadOnlyList<Club> clubs, IReadOnlyList<Player> freeAgents, DateTime date)
        {
            var people = CollectPeople(clubs, freeAgents);
            var clubOf = new Dictionary<string, int>();
            int monthIndex = date.Year * 12 + date.Month;
            var touched = new HashSet<Relationship>();
            int eventClub = -1;

            int clubCount = clubs == null ? 0 : clubs.Count;
            for (int c = 0; c < clubCount; c++)
            {
                if (clubs[c] != null && !string.IsNullOrEmpty(EventClubId) && clubs[c].Id == EventClubId) eventClub = c;
                var squad = clubs[c] != null ? clubs[c].Squad : null;
                if (squad == null) continue;
                for (int i = 0; i < squad.Count; i++)
                {
                    var p = squad[i];
                    if (p != null && !string.IsNullOrEmpty(p.Id)) clubOf[p.Id] = c;
                }

                for (int i = 0; i < squad.Count; i++)
                {
                    var a = squad[i];
                    if (a == null || string.IsNullOrEmpty(a.Id)) continue;
                    for (int j = i + 1; j < squad.Count; j++)
                    {
                        var b = squad[j];
                        if (b == null || string.IsNullOrEmpty(b.Id) || a.Id == b.Id) continue;
                        int delta = MonthlyGain(a, b, monthIndex);
                        var rel = Find(a.Id, b.Id);
                        if (rel == null)
                        {
                            if (delta <= 0) continue;
                            rel = Add(a.Id, b.Id, date);
                        }

                        rel.Affinity = Clamp(rel.Affinity + delta, rel.Stage == RelationStage.Married ? MarriedFloor : 0);
                        touched.Add(rel);
                    }
                }
            }

            for (int i = _list.Count - 1; i >= 0; i--)
            {
                var rel = _list[i];
                bool hasA = people.ContainsKey(rel.PlayerIdA);
                bool hasB = people.ContainsKey(rel.PlayerIdB);
                if (!hasA || !hasB)
                {
                    // 夫婦は片方が引退しても残す（家族の記録は優先度3で軽い記録に移す）。
                    bool keep = rel.Stage == RelationStage.Married && (hasA || hasB);
                    if (!keep)
                    {
                        RemoveAt(i);
                        continue;
                    }
                }

                if (touched.Contains(rel)) continue;
                int floor = rel.Stage == RelationStage.Married ? MarriedFloor : 0;
                rel.Affinity = Clamp(rel.Affinity - SeparatedDecay, floor);
            }

            UpdateStages(people, clubOf, eventClub, date);
            SyncMarried(people.Values);
        }

        /// <summary>夫婦の関係から Player.Married を揃える。</summary>
        public void SyncMarried(IEnumerable<Player> players)
        {
            if (players == null) return;
            var married = new HashSet<string>();
            for (int i = 0; i < _list.Count; i++)
            {
                var rel = _list[i];
                if (rel.Stage != RelationStage.Married) continue;
                married.Add(rel.PlayerIdA);
                married.Add(rel.PlayerIdB);
            }

            foreach (var player in players)
            {
                if (player != null) player.Married = married.Contains(player.Id);
            }
        }

        /// <summary>同じクラブで1か月過ごしたときの親密度の増減。</summary>
        public static int MonthlyGain(Player a, Player b, int monthIndex)
        {
            double compat = PlayerTraits.Compatibility(a.Id, b.Id);
            // 3乗で偏らせ、相性の良い組だけが伸びるようにする。
            // 25人のクラブで、友達は1人あたり4人ほど、1年目の恋人は2〜3組、長く続けると夫婦が6組前後になる。
            double baseGain = compat * compat * compat * CompatibilityGain - GainOffset;

            int ageDiff = Math.Abs(a.Age - b.Age);
            int ageBonus = ageDiff <= 3 ? 2 : (ageDiff <= 6 ? 1 : (ageDiff > 12 ? -1 : 0));
            int positionBonus = PositionUtility.GroupOf(a.PreferredPosition) == PositionUtility.GroupOf(b.PreferredPosition) ? 1 : 0;
            int noise = PlayerTraits.MonthlyNoise(a.Id, b.Id, monthIndex, NoiseRange);
            return (int)Math.Round(baseGain, MidpointRounding.AwayFromZero) + ageBonus + positionBonus + noise;
        }

        void UpdateStages(Dictionary<string, Player> people, Dictionary<string, int> clubOf, int eventClub, DateTime date)
        {
            var partnered = new HashSet<string>();
            for (int i = 0; i < _list.Count; i++)
            {
                var rel = _list[i];
                if (!IsPartner(rel.Stage)) continue;
                partnered.Add(rel.PlayerIdA);
                partnered.Add(rel.PlayerIdB);
            }

            // 後退（別れ・友達解消）を先に済ませ、空いた人が同じ月に新しい恋人を作れるようにする。
            for (int i = 0; i < _list.Count; i++)
            {
                var rel = _list[i];
                if (rel.Stage == RelationStage.Lover && rel.Affinity < BreakUpThreshold)
                {
                    partnered.Remove(rel.PlayerIdA);
                    partnered.Remove(rel.PlayerIdB);
                    ChangeStage(rel, RelationStage.Friend, date);
                }
                else if (rel.Stage == RelationStage.BestFriend && rel.Affinity < BestFriendLossThreshold)
                {
                    ChangeStage(rel, RelationStage.Friend, date);
                }
                else if (rel.Stage == RelationStage.Friend && rel.Affinity < FriendLossThreshold)
                {
                    ChangeStage(rel, RelationStage.None, date);
                }
            }

            var loverCandidates = new List<Relationship>();
            for (int i = 0; i < _list.Count; i++)
            {
                var rel = _list[i];
                // プレイヤーのクラブ同士の組は、関係イベントが起きるまで段階が進まない。
                if (WaitsForEvent(rel, clubOf, eventClub)) continue;
                if (rel.Stage == RelationStage.None && rel.Affinity >= FriendThreshold)
                    ChangeStage(rel, RelationStage.Friend, date);

                if (rel.Stage == RelationStage.Friend && rel.Affinity >= LoverThreshold)
                {
                    bool sameGender = people.TryGetValue(rel.PlayerIdA, out var pa) && people.TryGetValue(rel.PlayerIdB, out var pb) && pa.Gender == pb.Gender;
                    if (sameGender)
                    {
                        if (rel.Affinity >= BestFriendThreshold) ChangeStage(rel, RelationStage.BestFriend, date);
                    }
                    else
                    {
                        loverCandidates.Add(rel);
                    }
                }
                else if (rel.Stage == RelationStage.Lover && rel.Affinity >= MarriageThreshold && MonthsSince(rel.Since, date) >= MarriageMonths)
                    ChangeStage(rel, RelationStage.Married, date);
            }

            // 親密度の高い組から恋人にする。1人が同時に2人と恋人にならないようにする。
            loverCandidates.Sort((x, y) => y.Affinity.CompareTo(x.Affinity));
            for (int i = 0; i < loverCandidates.Count; i++)
            {
                var rel = loverCandidates[i];
                if (partnered.Contains(rel.PlayerIdA) || partnered.Contains(rel.PlayerIdB)) continue;
                if (!people.TryGetValue(rel.PlayerIdA, out var a) || !people.TryGetValue(rel.PlayerIdB, out var b)) continue;
                if (a.Gender == b.Gender || a.Age < AdultAge || b.Age < AdultAge) continue;
                if (!clubOf.TryGetValue(a.Id, out var clubA) || !clubOf.TryGetValue(b.Id, out var clubB) || clubA != clubB) continue;
                ChangeStage(rel, RelationStage.Lover, date);
                partnered.Add(rel.PlayerIdA);
                partnered.Add(rel.PlayerIdB);
            }

            for (int i = _list.Count - 1; i >= 0; i--)
            {
                var rel = _list[i];
                if (rel.Stage == RelationStage.None && rel.Affinity <= 0) RemoveAt(i);
            }
        }

        static bool WaitsForEvent(Relationship rel, Dictionary<string, int> clubOf, int eventClub)
        {
            if (eventClub < 0) return false;
            return clubOf.TryGetValue(rel.PlayerIdA, out var a) && clubOf.TryGetValue(rel.PlayerIdB, out var b) && a == eventClub && b == eventClub;
        }

        void ChangeStage(Relationship rel, RelationStage next, DateTime date)
        {
            if (rel.Stage == next) return;
            var previous = rel.Stage;
            rel.Stage = next;
            rel.Since = date;
            StageChanged?.Invoke(rel, previous);
        }

        Relationship Add(string a, string b, DateTime date)
        {
            Relationship.Order(a, b, out var first, out var second);
            var rel = new Relationship
            {
                PlayerIdA = first,
                PlayerIdB = second,
                Stage = RelationStage.None,
                Affinity = 0,
                Since = date
            };
            _list.Add(rel);
            _byPair[(first, second)] = rel;
            return rel;
        }

        void RemoveAt(int index)
        {
            var rel = _list[index];
            _byPair.Remove((rel.PlayerIdA, rel.PlayerIdB));
            _list.RemoveAt(index);
        }

        static Dictionary<string, Player> CollectPeople(IReadOnlyList<Club> clubs, IReadOnlyList<Player> freeAgents)
        {
            var people = new Dictionary<string, Player>();
            int clubCount = clubs == null ? 0 : clubs.Count;
            for (int c = 0; c < clubCount; c++)
            {
                var club = clubs[c];
                if (club == null) continue;
                AddAll(people, club.Squad);
                AddAll(people, club.Academy);
            }

            AddAll(people, freeAgents);
            return people;
        }

        static void AddAll(Dictionary<string, Player> people, IReadOnlyList<Player> roster)
        {
            if (roster == null) return;
            for (int i = 0; i < roster.Count; i++)
            {
                var p = roster[i];
                if (p != null && !string.IsNullOrEmpty(p.Id)) people[p.Id] = p;
            }
        }

        static int MonthsSince(DateTime since, DateTime now)
        {
            return (now.Year - since.Year) * 12 + now.Month - since.Month;
        }

        static int Clamp(int value, int floor)
        {
            if (value < floor) return floor;
            if (value > MaxAffinity) return MaxAffinity;
            return value;
        }

        // ---- セーブ ----

        public RelationshipSave[] Write()
        {
            var result = new RelationshipSave[_list.Count];
            for (int i = 0; i < _list.Count; i++)
            {
                var rel = _list[i];
                result[i] = new RelationshipSave
                {
                    PlayerIdA = rel.PlayerIdA,
                    PlayerIdB = rel.PlayerIdB,
                    Stage = (int)rel.Stage,
                    Affinity = rel.Affinity,
                    SinceTicks = rel.Since.Ticks
                };
            }

            return result;
        }

        public void Read(RelationshipSave[] saves)
        {
            _list.Clear();
            _byPair.Clear();
            if (saves == null) return;
            for (int i = 0; i < saves.Length; i++)
            {
                var save = saves[i];
                if (save == null || string.IsNullOrEmpty(save.PlayerIdA) || string.IsNullOrEmpty(save.PlayerIdB)) continue;
                if (save.PlayerIdA == save.PlayerIdB) continue;
                Relationship.Order(save.PlayerIdA, save.PlayerIdB, out var first, out var second);
                if (_byPair.ContainsKey((first, second))) continue;
                int stage = save.Stage;
                if (stage < (int)RelationStage.None || stage > (int)RelationStage.BestFriend) stage = (int)RelationStage.None;
                var rel = new Relationship
                {
                    PlayerIdA = first,
                    PlayerIdB = second,
                    Stage = (RelationStage)stage,
                    Affinity = Clamp(save.Affinity, 0),
                    Since = save.SinceTicks > 0 ? new DateTime(save.SinceTicks) : default
                };
                _list.Add(rel);
                _byPair[(first, second)] = rel;
            }
        }
    }
}
