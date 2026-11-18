using System;
using System.Collections.Generic;
using SoccerClub.Core;

namespace SoccerClub.Data
{
    /// <summary>
    /// 季節のイベント。その日の朝に、季節の背景の上で選手1〜2人が話す。お金と能力は変えない。
    /// Resources/Master/Seasons.csv に日付・見出し・背景、SeasonTalk.csv に会話（1行＝1つの会話）を書く。
    /// 会話の cast は solo（1人）/ pair（友達か親友の2人）/ couple（恋人か夫婦の2人）。
    /// 台詞は line1 = A、line2 = B（solo は A）、line3 = A（空欄なら2つで終わる）。
    /// </summary>
    public static class SeasonalCatalog
    {
        public sealed class Season
        {
            public string Id;
            public int Month;
            public int Day;
            public string Banner;
            public string Title;

            /// <summary>Resources からの背景のパス。読めなければ会議室の背景を使う。</summary>
            public string Background;
        }

        public sealed class Script
        {
            public int Index;
            public string SeasonId;
            public string Cast;
            public string Condition;
            public int Weight;
            public string Line1;
            public string Line2;
            public string Line3;

            public bool Solo => Cast == "solo";
            public int LineCount => string.IsNullOrEmpty(Line3) ? 2 : 3;
        }

        /// <summary>会話に出る2人（solo は B が null）と、2人の関係。</summary>
        public struct Cast
        {
            public Player A;
            public Player B;
            public RelationStage Stage;
        }

        static readonly List<Season> _seasons = new List<Season>();
        static readonly List<Script> _scripts = new List<Script>();
        static bool _loaded;

        public static Season Find(string id)
        {
            Ensure();
            for (int i = 0; i < _seasons.Count; i++)
            {
                if (_seasons[i].Id == id) return _seasons[i];
            }

            return null;
        }

        public static Script ScriptAt(int index)
        {
            Ensure();
            return index >= 0 && index < _scripts.Count ? _scripts[index] : null;
        }

        /// <summary>その日の季節のイベント。無ければ null。</summary>
        public static Season On(DateTime date)
        {
            Ensure();
            for (int i = 0; i < _seasons.Count; i++)
            {
                var s = _seasons[i];
                if (s.Month == date.Month && s.Day == date.Day) return s;
            }

            return null;
        }

        /// <summary>
        /// 季節の会話を1つ選び、出る選手を決める。solos は所属選手、pairs は友達・親友の組、couples は恋人・夫婦の組。
        /// 出られる組が無い会話は選ばない。どれも無ければ false。
        /// </summary>
        public static bool TryChoose(Season season, List<Player> solos, List<Cast> pairs, List<Cast> couples, Random random, out Script script, out Cast cast)
        {
            Ensure();
            script = null;
            cast = default;
            if (season == null) return false;

            var usable = new List<Script>();
            var options = new List<List<Cast>>();
            int total = 0;
            for (int i = 0; i < _scripts.Count; i++)
            {
                var s = _scripts[i];
                if (s.SeasonId != season.Id) continue;
                var list = Candidates(s, solos, pairs, couples);
                if (list.Count == 0) continue;
                usable.Add(s);
                options.Add(list);
                total += s.Weight;
            }

            if (total <= 0) return false;
            int roll = random.Next(total);
            for (int i = 0; i < usable.Count; i++)
            {
                roll -= usable[i].Weight;
                if (roll >= 0) continue;
                script = usable[i];
                var list = options[i];
                cast = list[random.Next(list.Count)];
                return true;
            }

            return false;
        }

        static List<Cast> Candidates(Script s, List<Player> solos, List<Cast> pairs, List<Cast> couples)
        {
            var result = new List<Cast>();
            if (s.Cast == "solo")
            {
                if (solos == null) return result;
                for (int i = 0; i < solos.Count; i++)
                {
                    var c = new Cast { A = solos[i], Stage = RelationStage.None };
                    if (Matches(s.Condition, c)) result.Add(c);
                }
            }
            else
            {
                var source = s.Cast == "couple" ? couples : s.Cast == "pair" ? pairs : null;
                if (source == null) return result;
                for (int i = 0; i < source.Count; i++)
                {
                    if (Matches(s.Condition, source[i])) result.Add(source[i]);
                }
            }

            return result;
        }

        /// <summary>条件名は CSV の condition 列。2人の会話では A と B の両方を見るものがある。新しい条件はここに足す。</summary>
        public static bool Matches(string condition, Cast c)
        {
            var a = c.A;
            var b = c.B;
            if (a == null) return false;
            switch (condition)
            {
                case "":
                case "any": return true;
                case "young": return a.Age <= 21 && (b == null || b.Age <= 21);
                case "veteran": return a.Phase == GrowthPhase.Veteran;
                case "married": return c.Stage == RelationStage.Married || (b == null && a.Married);
                case "lover": return c.Stage == RelationStage.Lover;
                case "best_friend": return c.Stage == RelationStage.BestFriend;
                case "children": return a.Children > 0;
                case "single": return b == null && !a.Married;
                default: return false;
            }
        }

        /// <summary>差し込みを埋めた台詞（line1〜3）。line3 が無ければ2つ。</summary>
        public static string[] Lines(Script script, Cast cast, string clubName, int year)
        {
            if (script == null) return new string[0];
            var a = cast.A;
            var b = cast.B ?? cast.A;
            var tokens = new Dictionary<string, string>
            {
                { "nameA", a != null ? a.Name ?? "" : "" },
                { "nameB", b != null ? b.Name ?? "" : "" },
                { "meA", a != null ? CharacterLife.Pronoun(a.Id) : "" },
                { "meB", b != null ? CharacterLife.Pronoun(b.Id) : "" },
                { "foodA", a != null ? CharacterLife.FavoriteFood(a.Id) : "" },
                { "foodB", b != null ? CharacterLife.FavoriteFood(b.Id) : "" },
                { "hobbyA", a != null ? CharacterLife.Hobby(a.Id) : "" },
                { "club", clubName ?? "" },
                { "year", year.ToString() }
            };

            var result = new string[script.LineCount];
            result[0] = RelationEventCatalog.Fill(script.Line1, tokens);
            result[1] = RelationEventCatalog.Fill(script.Line2, tokens);
            if (result.Length > 2) result[2] = RelationEventCatalog.Fill(script.Line3, tokens);
            return result;
        }

        public static void Reload()
        {
            _loaded = false;
            Ensure();
        }

        static void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            _seasons.Clear();
            _scripts.Clear();

            // 列: id, month, day, banner, title, background
            var rows = MasterCsv.Rows("Seasons.csv");
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Length < 5) continue;
                string id = (row[0] ?? "").Trim();
                int month;
                int day;
                if (id.Length == 0 || id == "id") continue;
                if (!int.TryParse((row[1] ?? "").Trim(), out month) || month < 1 || month > 12) continue;
                if (!int.TryParse((row[2] ?? "").Trim(), out day) || day < 1 || day > 31) continue;
                // 1月1日は新年あいさつがあるので使わない。
                if (month == 1 && day == 1) continue;
                _seasons.Add(new Season
                {
                    Id = id,
                    Month = month,
                    Day = day,
                    Banner = (row[3] ?? "").Trim(),
                    Title = (row[4] ?? "").Trim(),
                    Background = row.Length > 5 ? (row[5] ?? "").Trim() : ""
                });
            }

            // 列: season, cast, condition, weight, line1, line2, line3
            var talk = MasterCsv.Rows("SeasonTalk.csv");
            for (int i = 0; i < talk.Count; i++)
            {
                var row = talk[i];
                if (row == null || row.Length < 6) continue;
                string season = (row[0] ?? "").Trim();
                string cast = (row[1] ?? "").Trim();
                if (season.Length == 0 || season == "season") continue;
                if (cast != "solo" && cast != "pair" && cast != "couple") continue;
                int weight;
                if (!int.TryParse((row[3] ?? "").Trim(), out weight)) weight = 1;
                if (weight <= 0) continue;
                string line1 = (row[4] ?? "").Trim();
                string line2 = (row[5] ?? "").Trim();
                if (line1.Length == 0 || line2.Length == 0) continue;
                _scripts.Add(new Script
                {
                    Index = _scripts.Count,
                    SeasonId = season,
                    Cast = cast,
                    Condition = (row[2] ?? "").Trim(),
                    Weight = weight,
                    Line1 = line1,
                    Line2 = line2,
                    Line3 = row.Length > 6 ? (row[6] ?? "").Trim() : ""
                });
            }

            // 会話が1つも無い季節には、1人の会話を組み込みで足す（CSV が無くても止まらない）。
            for (int i = 0; i < _seasons.Count; i++)
            {
                var s = _seasons[i];
                bool has = false;
                for (int j = 0; j < _scripts.Count; j++)
                {
                    if (_scripts[j].SeasonId == s.Id) has = true;
                }

                if (has) continue;
                string title = string.IsNullOrEmpty(s.Title) ? "今日" : s.Title;
                _scripts.Add(new Script
                {
                    Index = _scripts.Count,
                    SeasonId = s.Id,
                    Cast = "solo",
                    Condition = "any",
                    Weight = 1,
                    Line1 = "今日は" + title + "ですね！",
                    Line2 = "{meA}も楽しみにしていました。",
                    Line3 = ""
                });
            }
        }
    }
}
