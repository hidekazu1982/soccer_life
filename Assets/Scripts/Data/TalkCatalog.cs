using System.Collections.Generic;
using UnityEngine;

namespace SoccerClub.Data
{
    /// <summary>
    /// ミニイベントの台詞。StreamingAssets/Master の CSV を読む。
    /// </summary>
    public static class TalkCatalog
    {
        static readonly string[] Empty = new string[0];
        static string[] _chatter = Empty;
        static bool[] _soccer = new bool[0];
        static Dictionary<string, string[]> _words = new Dictionary<string, string[]>();
        static Dictionary<string, string[]> _templates = new Dictionary<string, string[]>();
        static bool _loaded;

        public static string[] Chatter
        {
            get
            {
                Ensure();
                return _chatter;
            }
        }

        public static bool IsSoccer(int index)
        {
            Ensure();
            if (index < 0 || index >= _soccer.Length) return false;
            return _soccer[index];
        }

        public static string[] Words(string kind)
        {
            Ensure();
            string[] list;
            return _words.TryGetValue(kind ?? "", out list) ? list : Empty;
        }

        public static string Fill(string kind, Dictionary<string, string> tokens)
        {
            Ensure();
            string text = Template(kind);
            if (tokens == null) return text;
            foreach (var pair in tokens)
                text = text.Replace("{" + pair.Key + "}", pair.Value ?? "");
            return text;
        }

        static string Template(string kind)
        {
            string[] list;
            if (_templates.TryGetValue(kind ?? "", out list) && list.Length > 0)
                return list[Random.Range(0, list.Length)];
            return Fallback(kind);
        }

        static string Fallback(string kind)
        {
            switch (kind)
            {
                case "birthday": return "きょうは誕生日。自分で祝うことにした。";
                case "goal": return "さっきのゴールがまだ残ってる。ネットがゆれた。";
                case "marriage": return "けっこんした。ピッチの外の話。";
                case "divorce": return "離婚することにした。名前はそのままで、前をむく。";
                case "child": return "子どもが生まれた。いま{count}人。";
                case "season_goal": return "上半期の目標は{goal}こと。";
                case "appearance": return "この期間の出場は{count}試合。";
                case "favorite_food": return "すきなたべものは{food}。";
                case "disliked_food": return "嫌いな食べ物は{dislike}。";
                case "hobby": return "趣味は{hobby}。";
                case "dream_job": return "サッカー以外なら{job}になりたい。";
                case "travel": return "{country}で{activity}してみたい。";
                default: return "{line}";
            }
        }

        static void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            LoadChatter();
            _words = Groups("LifeWords.csv");
            _templates = Groups("TalkTemplates.csv");
        }

        static void LoadChatter()
        {
            var rows = MasterCsv.Rows("Chatter.csv");
            var lines = new List<string>();
            var flags = new List<bool>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null || rows[i].Length == 0) continue;
                string text = (rows[i][0] ?? "").Trim();
                if (text.Length == 0 || text == "text") continue;
                lines.Add(text);
                flags.Add(rows[i].Length > 1 && (rows[i][1] ?? "").Trim() == "1");
            }

            _chatter = lines.ToArray();
            _soccer = flags.ToArray();
        }

        static string[] Column(string fileName, string header)
        {
            var rows = MasterCsv.Rows(fileName);
            var list = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null || rows[i].Length == 0) continue;
                string text = (rows[i][0] ?? "").Trim();
                if (text.Length == 0 || text == header) continue;
                list.Add(text);
            }

            return list.ToArray();
        }

        static Dictionary<string, string[]> Groups(string fileName)
        {
            var rows = MasterCsv.Rows(fileName);
            var buckets = new Dictionary<string, List<string>>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null || rows[i].Length < 2) continue;
                string kind = (rows[i][0] ?? "").Trim();
                string text = rows[i][1] ?? "";
                if (kind.Length == 0 || kind == "kind") continue;
                if (text.Trim().Length == 0) continue;
                List<string> bucket;
                if (!buckets.TryGetValue(kind, out bucket))
                {
                    bucket = new List<string>();
                    buckets[kind] = bucket;
                }

                bucket.Add(text);
            }

            var result = new Dictionary<string, string[]>();
            foreach (var pair in buckets)
                result[pair.Key] = pair.Value.ToArray();
            return result;
        }
    }
}
