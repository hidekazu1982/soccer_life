using System;
using System.Collections.Generic;
using SoccerClub.Core;

namespace SoccerClub.Data
{
    /// <summary>
    /// 関係イベント（友達・親友・恋人・夫婦）の台詞。Resources/Master/RelationEvents.csv を読む。
    /// 1行が1パターン。進む先の段階（stage）と、2人が満たす条件（condition）が合う行から、weight の重みで1つ選ぶ。
    /// 条件を満たす行が無ければ condition=fallback の行を使う。
    /// 新しい条件を足すときは Matches に case を1つ追加し、CSV にその名前の行を書く。
    /// </summary>
    public static class RelationEventCatalog
    {
        public sealed class Entry
        {
            public RelationStage Stage;
            public string Condition;
            public int Weight;
            public string LineA;
            public string LineB;
            public string Result;
        }

        public const string FallbackCondition = "fallback";

        static readonly List<Entry> _entries = new List<Entry>();
        static bool _loaded;

        public static IReadOnlyList<Entry> Entries
        {
            get
            {
                Ensure();
                return _entries;
            }
        }

        /// <summary>a が話しかけ、b が答える。台詞の {…} は埋めた状態で返す。</summary>
        public static bool TryPick(RelationStage next, Player a, Player b, Func<int, int> random, out string lineA, out string lineB, out string result)
        {
            Ensure();
            lineA = null;
            lineB = null;
            result = null;
            if (a == null || b == null) return false;

            var picked = Pick(next, a, b, false, random) ?? Pick(next, a, b, true, random);
            if (picked == null) return false;

            var tokens = Tokens(a, b);
            lineA = Fill(picked.LineA, tokens);
            lineB = Fill(picked.LineB, tokens);
            result = Fill(string.IsNullOrEmpty(picked.Result) ? DefaultResult(next) : picked.Result, tokens);
            return true;
        }

        static Entry Pick(RelationStage next, Player a, Player b, bool fallback, Func<int, int> random)
        {
            int total = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Stage != next) continue;
                if ((e.Condition == FallbackCondition) != fallback) continue;
                if (!fallback && !Matches(e.Condition, a, b)) continue;
                total += e.Weight;
            }

            if (total <= 0) return null;
            int roll = random != null ? random(total) : 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Stage != next) continue;
                if ((e.Condition == FallbackCondition) != fallback) continue;
                if (!fallback && !Matches(e.Condition, a, b)) continue;
                roll -= e.Weight;
                if (roll < 0) return e;
            }

            return null;
        }

        /// <summary>2人がその条件を満たすか。条件名は CSV の condition 列。</summary>
        public static bool Matches(string condition, Player a, Player b)
        {
            switch (condition)
            {
                case "any":
                    return true;
                case "food":
                    return Same(CharacterLife.FavoriteFood(a.Id), CharacterLife.FavoriteFood(b.Id));
                case "hobby":
                {
                    string hobby = CharacterLife.Hobby(a.Id);
                    return !CharacterLife.IsMovie(hobby) && Same(hobby, CharacterLife.Hobby(b.Id));
                }
                case "hobby_movie":
                {
                    string hobby = CharacterLife.Hobby(a.Id);
                    return CharacterLife.IsMovie(hobby) && Same(hobby, CharacterLife.Hobby(b.Id));
                }
                case "country":
                {
                    CharacterLife.Travel(a.Id, out var countryA, out _);
                    CharacterLife.Travel(b.Id, out var countryB, out _);
                    return Same(countryA, countryB);
                }
                case "movie":
                    return Same(CharacterLife.Movie(a.Id), CharacterLife.Movie(b.Id));
                case "birth_month":
                {
                    CharacterLife.Birthday(a.Id, out var monthA, out _);
                    CharacterLife.Birthday(b.Id, out var monthB, out _);
                    return monthA == monthB;
                }
                case "position":
                    return PositionUtility.GroupOf(a.PreferredPosition) == PositionUtility.GroupOf(b.PreferredPosition);
                case "age_close":
                    return Math.Abs(a.Age - b.Age) <= 3;
                case "veteran":
                    return a.Age >= 30 && b.Age >= 30;
                case "young":
                    return a.Age <= 21 && b.Age <= 21;
                default:
                    return false;
            }
        }

        static bool Same(string x, string y)
        {
            return !string.IsNullOrEmpty(x) && x == y;
        }

        /// <summary>2人の台詞に差し込む値。ExtraPracticeCatalog からも使う。</summary>
        public static Dictionary<string, string> Tokens(Player a, Player b)
        {
            CharacterLife.Travel(a.Id, out var country, out var activity);
            CharacterLife.Birthday(a.Id, out var month, out _);
            return new Dictionary<string, string>
            {
                { "nameA", a.Name ?? "" },
                { "nameB", b.Name ?? "" },
                { "meA", CharacterLife.Pronoun(a.Id) },
                { "meB", CharacterLife.Pronoun(b.Id) },
                { "food", CharacterLife.FavoriteFood(a.Id) },
                { "hobby", CharacterLife.Hobby(a.Id) },
                { "country", country },
                { "activity", activity },
                { "movie", CharacterLife.Movie(a.Id) },
                { "month", month.ToString() }
            };
        }

        public static string Fill(string text, Dictionary<string, string> tokens)
        {
            if (string.IsNullOrEmpty(text)) return "";
            foreach (var pair in tokens)
                text = text.Replace("{" + pair.Key + "}", pair.Value ?? "");
            return text;
        }

        static string DefaultResult(RelationStage next)
        {
            switch (next)
            {
                case RelationStage.Married: return "{nameA}と{nameB}が結婚した！";
                case RelationStage.Lover: return "{nameA}と{nameB}が恋人になった！";
                case RelationStage.BestFriend: return "{nameA}と{nameB}が親友になった！";
                default: return "{nameA}と{nameB}が友達になった！";
            }
        }

        public static bool TryParseStage(string text, out RelationStage stage)
        {
            switch ((text ?? "").Trim())
            {
                case "friend": stage = RelationStage.Friend; return true;
                case "best_friend": stage = RelationStage.BestFriend; return true;
                case "lover": stage = RelationStage.Lover; return true;
                case "married": stage = RelationStage.Married; return true;
                default: stage = RelationStage.None; return false;
            }
        }

        /// <summary>エディタで CSV を書き換えたあとに読み直す。</summary>
        public static void Reload()
        {
            _loaded = false;
            Ensure();
        }

        static void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            _entries.Clear();
            // 列: stage, condition, weight, line_a, line_b, result
            var rows = MasterCsv.Rows("RelationEvents.csv");
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Length < 5) continue;
                if (!TryParseStage(row[0], out var stage)) continue;
                string condition = (row[1] ?? "").Trim();
                if (condition.Length == 0) condition = "any";
                int weight;
                if (!int.TryParse((row[2] ?? "").Trim(), out weight)) weight = 1;
                if (weight <= 0) continue;
                string lineA = (row[3] ?? "").Trim();
                string lineB = (row[4] ?? "").Trim();
                if (lineA.Length == 0 || lineB.Length == 0) continue;
                _entries.Add(new Entry
                {
                    Stage = stage,
                    Condition = condition,
                    Weight = weight,
                    LineA = lineA,
                    LineB = lineB,
                    Result = row.Length > 5 ? (row[5] ?? "").Trim() : ""
                });
            }

            AddBuiltInFallbacks();
        }

        /// <summary>CSV が無い・壊れているときでもイベントが止まらないよう、段階ごとに最低1行を足す。</summary>
        static void AddBuiltInFallbacks()
        {
            AddFallbackIfMissing(RelationStage.Friend, "今日の練習、最後まで一緒に走ったね。", "ひとりじゃないから頑張れたよ。ありがとう。");
            AddFallbackIfMissing(RelationStage.BestFriend, "{nameB}とは、何でも話せるね。", "うん、いちばんの親友だよ！");
            AddFallbackIfMissing(RelationStage.Lover, "あのさ、ずっと言いたかったことがあるんだ。好きです。", "……{meB}も、同じ気持ちだよ。");
            AddFallbackIfMissing(RelationStage.Married, "これからもずっと一緒にいてほしい。結婚しよう。", "はい。よろしくお願いします！");
        }

        static void AddFallbackIfMissing(RelationStage stage, string lineA, string lineB)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Stage == stage && _entries[i].Condition == FallbackCondition) return;
            }

            _entries.Add(new Entry { Stage = stage, Condition = FallbackCondition, Weight = 1, LineA = lineA, LineB = lineB, Result = "" });
        }
    }
}
