using System;
using System.Collections.Generic;

namespace SoccerClub.Data
{
    /// <summary>「ひとこと」を選ぶための選手の状況。UI 側で作って渡す。</summary>
    public sealed class VoiceContext
    {
        public string Id;
        public int Age;
        public int Condition;
        public int InjuryDays;
        public int SeasonMatches;
        public int SeasonGoals;
        public int Children;
        /// <summary>今季の出場が 2 試合以下で、30 日以上出ていない。</summary>
        public bool LongBench;
        public bool Birthday;
        /// <summary>恋人か配偶者の名前（いなければ null）。</summary>
        public string PartnerName;
        public bool PartnerMarried;
        /// <summary>親友の名前（いなければ null）。</summary>
        public string BestFriendName;
        /// <summary>友達の名前（いなければ null）。</summary>
        public string FriendName;
        /// <summary>同じ日は同じ台詞にするための日付（DateTime.Date.Ticks）。</summary>
        public long DayKey;
    }

    /// <summary>
    /// 選手詳細の「ひとこと」（版 1.70）。Resources/Master/PlayerVoice.csv を読む。
    /// 条件に合う行から、weight の重みで 1 つ選ぶ。選手と日付で決まるので、同じ日に開き直しても変わらない。
    /// </summary>
    public static class PlayerVoiceCatalog
    {
        struct Line
        {
            public string Condition;
            public int Weight;
            public string Text;
        }

        static List<Line> _lines;

        public static string Pick(VoiceContext context)
        {
            if (context == null || string.IsNullOrEmpty(context.Id)) return "";
            Ensure();
            int total = 0;
            for (int i = 0; i < _lines.Count; i++)
                if (Matches(_lines[i].Condition, context)) total += _lines[i].Weight;
            if (total <= 0) return "";

            int roll = Hash(context.Id + "|" + context.DayKey.ToString(System.Globalization.CultureInfo.InvariantCulture)) % total;
            for (int i = 0; i < _lines.Count; i++)
            {
                if (!Matches(_lines[i].Condition, context)) continue;
                roll -= _lines[i].Weight;
                if (roll < 0) return Fill(_lines[i].Text, context);
            }

            return "";
        }

        static bool Matches(string condition, VoiceContext c)
        {
            string hobby = CharacterLife.Hobby(c.Id);
            switch (condition)
            {
                case "any": return true;
                case "hobby": return !string.IsNullOrEmpty(hobby) && !CharacterLife.IsMovie(hobby);
                case "hobby_movie": return CharacterLife.IsMovie(hobby);
                case "food": return !string.IsNullOrEmpty(CharacterLife.FavoriteFood(c.Id));
                case "dislike": return !string.IsNullOrEmpty(CharacterLife.DislikedFood(c.Id));
                case "dream": return !string.IsNullOrEmpty(CharacterLife.DreamJob(c.Id));
                case "season_goal": return !string.IsNullOrEmpty(CharacterLife.SeasonGoal(c.Id));
                case "birthday": return c.Birthday;
                case "young": return c.Age <= 20;
                case "veteran": return c.Age >= 32;
                case "injured": return c.InjuryDays > 0;
                case "tired": return c.InjuryDays <= 0 && c.Condition < 40;
                case "fresh": return c.InjuryDays <= 0 && c.Condition >= 90;
                case "bench": return c.InjuryDays <= 0 && c.LongBench;
                case "regular": return c.SeasonMatches >= 10;
                case "scorer": return c.SeasonGoals >= 3;
                case "married": return c.PartnerName != null && c.PartnerMarried;
                case "lover": return c.PartnerName != null && !c.PartnerMarried;
                case "best_friend": return c.BestFriendName != null;
                case "friend": return c.FriendName != null;
                case "children": return c.Children > 0;
                default: return false;
            }
        }

        static string Fill(string text, VoiceContext c)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return text
                .Replace("{me}", CharacterLife.Pronoun(c.Id))
                .Replace("{hobby}", CharacterLife.Hobby(c.Id))
                .Replace("{movie}", CharacterLife.Movie(c.Id))
                .Replace("{food}", CharacterLife.FavoriteFood(c.Id))
                .Replace("{dislike}", CharacterLife.DislikedFood(c.Id))
                .Replace("{job}", CharacterLife.DreamJob(c.Id))
                .Replace("{goal}", CharacterLife.SeasonGoal(c.Id))
                .Replace("{partner}", c.PartnerName ?? "")
                .Replace("{friend}", c.BestFriendName ?? c.FriendName ?? "")
                .Replace("{goals}", c.SeasonGoals.ToString(ci))
                .Replace("{age}", c.Age.ToString(ci))
                .Replace("{children}", c.Children.ToString(ci));
        }

        static void Ensure()
        {
            if (_lines != null) return;
            _lines = new List<Line>();
            var rows = MasterCsv.Rows("PlayerVoice.csv");
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Length < 3) continue;
                string condition = (row[0] ?? "").Trim();
                if (condition.Length == 0 || condition == "condition") continue;
                int weight;
                if (!int.TryParse((row[1] ?? "").Trim(), out weight) || weight <= 0) continue;
                string text = (row[2] ?? "").Trim();
                if (text.Length == 0) continue;
                _lines.Add(new Line { Condition = condition, Weight = weight, Text = text });
            }
        }

        static int Hash(string text)
        {
            int hash = 23;
            for (int i = 0; i < text.Length; i++)
                hash = unchecked(hash * 31 + text[i]);
            return hash & int.MaxValue;
        }
    }
}
