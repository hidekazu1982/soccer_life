namespace SoccerClub.Data
{
    /// <summary>
    /// 台詞用の好み。単語は LifeWords.csv、人物の id から1つ決まる。
    /// </summary>
    public static class CharacterLife
    {
        public static void Birthday(string id, out int month, out int day)
        {
            int hash = Hash(id);
            month = 1 + hash % 12;
            day = 1 + (hash / 12) % 28;
        }

        public static string FavoriteFood(string id) => Pick("food", id, 1);

        public static string DislikedFood(string id) => Pick("dislike", id, 2);

        public static string Hobby(string id) => Pick("hobby", id, 3);

        public static bool IsMovie(string hobby)
        {
            return hobby == "映画" || hobby == "えいが";
        }

        public static string Pronoun(string id)
        {
            return PickFixed(id, 11, "ぼく", "わたし", "おれ", "うち");
        }

        public static string Movie(string id)
        {
            return PickFixed(id, 13, "アクション", "サスペンス", "恋愛", "コメディ", "アニメ", "ドキュメンタリー");
        }

        public static string DreamJob(string id) => Pick("job", id, 4);

        public static string SeasonGoal(string id) => Pick("goal", id, 5);

        public static void Travel(string id, out string country, out string activity)
        {
            country = Pick("country", id, 6);
            activity = Pick("activity", id, 7);
        }

        static string PickFixed(string id, int salt, params string[] list)
        {
            if (list == null || list.Length == 0) return "";
            return list[(Hash(id) + salt * 97) % list.Length];
        }

        static string Pick(string kind, string id, int salt)
        {
            var list = TalkCatalog.Words(kind);
            if (list == null || list.Length == 0) return "";
            return list[(Hash(id) + salt * 97) % list.Length];
        }

        static int Hash(string id)
        {
            int hash = 17;
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < id.Length; i++)
                    hash = unchecked(hash * 31 + id[i]);
            }

            return hash & int.MaxValue;
        }
    }
}
