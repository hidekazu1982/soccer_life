using System;
using System.Collections.Generic;
using SoccerClub.Data;

namespace SoccerClub.Core
{
    /// <summary>
    /// 居残り練習。自クラブだけ。週に1回、発生するかどうかを抽選し、発生する週は残りの平日から1日を選ぶ。
    /// その日に練習している選手（ベテランを除く）を出場の少ない順に並べ、少ないほう半分から2人を選ぶ。
    /// 2人とも同じ能力1つが「練習1日分」もう1回伸び、親密度が上がる（友達の条件を満たせば友達イベントの候補になる）。
    /// 抽選は RollDailyGrowth、反映は AdvanceDay の成長のあと。高速シミュレーターでも同じ処理を通る。
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>その週に居残り練習が起きる確率。</summary>
        public const double ExtraPracticeWeeklyChance = 0.5d;

        /// <summary>居残り練習で上がる親密度（友達は 30 から）。</summary>
        public const int ExtraPracticeAffinity = 5;

        /// <summary>その日の居残り練習。UI の演出用。</summary>
        public sealed class ExtraPracticeInfo
        {
            public DateTime Date;
            public string PlayerIdA;
            public string PlayerIdB;

            /// <summary>0 攻撃、1 守備、2 スタミナ。</summary>
            public int Stat;

            public double GainA;
            public double GainB;
            public bool Applied;
        }

        /// <summary>抽選済みの週（月曜の日付）。</summary>
        DateTime _extraPracticeWeek;

        /// <summary>その週に居残り練習をする日。default はその週は無し。</summary>
        DateTime _extraPracticeDay;

        ExtraPracticeInfo _extraPractice;

        /// <summary>今日の居残り練習。無ければ null。</summary>
        public ExtraPracticeInfo TodayExtraPractice
        {
            get
            {
                if (_extraPractice == null || _extraPractice.Date != Time.CurrentDate.Date) return null;
                return _extraPractice;
            }
        }

        /// <summary>RollDailyGrowth の最後に呼ぶ（練習日だけ）。</summary>
        void RollExtraPractice()
        {
            var today = Time.CurrentDate.Date;
            if (_extraPractice != null && _extraPractice.Date != today) _extraPractice = null;
            if (PlayerClub == null || PlayerClub.Squad == null) return;

            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            if (_extraPracticeWeek != monday)
            {
                _extraPracticeWeek = monday;
                _extraPracticeDay = default;
                // 今日から金曜までの平日から1日を選ぶ（週の初日が休みでも取りこぼさない）。
                int remaining = 5 - (int)(today - monday).TotalDays;
                if (remaining > 0 && _random.NextDouble() < ExtraPracticeWeeklyChance)
                    _extraPracticeDay = today.AddDays(_random.Next(remaining));
            }

            if (_extraPracticeDay == default || _extraPracticeDay != today) return;

            var pool = ExtraPracticePool();
            if (pool.Count < 2) return;
            int first = _random.Next(pool.Count);
            int second = _random.Next(pool.Count - 1);
            if (second >= first) second++;
            var a = pool[first];
            var b = pool[second];

            // 2人の練習の傾向（ポジション）から、伸ばす能力を1つ決める。
            int stat = _random.Next(2) == 0 ? PickPracticeStat(a.PreferredPosition) : PickPracticeStat(b.PreferredPosition);
            _extraPractice = new ExtraPracticeInfo
            {
                Date = today,
                PlayerIdA = a.Id,
                PlayerIdB = b.Id,
                Stat = stat,
                GainA = ExtraPracticeGain(a, stat),
                GainB = ExtraPracticeGain(b, stat)
            };
        }

        /// <summary>今日練習している自クラブの選手（ベテランを除く）のうち、出場の少ないほう半分。境目と同じ出場数の選手は含める。</summary>
        List<Player> ExtraPracticePool()
        {
            var list = new List<Player>();
            var squad = PlayerClub.Squad;
            for (int i = 0; i < squad.Count; i++)
            {
                var p = squad[i];
                if (p == null || string.IsNullOrEmpty(p.Id) || p.Phase == GrowthPhase.Veteran) continue;
                if (!_pending.ContainsKey(p.Id)) continue;
                list.Add(p);
            }

            if (list.Count < 2) return list;
            list.Sort((x, y) =>
            {
                int c = x.Appearances.CompareTo(y.Appearances);
                return c != 0 ? c : string.CompareOrdinal(x.Id, y.Id);
            });

            int size = Math.Max(2, (list.Count + 1) / 2);
            int limit = list[size - 1].Appearances;
            int count = size;
            while (count < list.Count && list[count].Appearances <= limit) count++;
            if (count < list.Count) list.RemoveRange(count, list.Count - count);
            return list;
        }

        /// <summary>練習1日分の上昇量（上がる抽選に当たったときと同じ式）。</summary>
        double ExtraPracticeGain(Player player, int stat)
        {
            double offenseMult;
            double staminaMult;
            ClubMath.PracticeMultiplier(PlayerClub, player, out offenseMult, out staminaMult);
            double mult = stat == 2 ? staminaMult : offenseMult;
            return ScaledGain(ClubMath.TrainingBaseAdd * mult, player.ConditionGainPercent());
        }

        /// <summary>AdvanceDay で成長を締めたあとに呼ぶ。</summary>
        void ApplyExtraPractice()
        {
            var info = _extraPractice;
            if (info == null || info.Applied || info.Date != Time.CurrentDate.Date) return;
            info.Applied = true;
            Player a;
            Player b;
            StaffMember unused;
            if (!TryClubPerson(info.PlayerIdA, out a, out unused) || a == null) return;
            if (!TryClubPerson(info.PlayerIdB, out b, out unused) || b == null) return;
            ApplyStat(a, info.Stat, info.GainA);
            ApplyStat(b, info.Stat, info.GainB);
            _relations.AddAffinity(a.Id, b.Id, ExtraPracticeAffinity, info.Date);
        }

        static void ApplyStat(Player player, int stat, double gain)
        {
            if (gain <= 0d) return;
            player.ApplyDailyGrowth(stat == 0 ? gain : 0d, stat == 1 ? gain : 0d, stat == 2 ? gain : 0d);
        }

        void WriteExtraPractice(GameSaveFile file)
        {
            file.ExtraPracticeWeekTicks = _extraPracticeWeek == default ? 0 : _extraPracticeWeek.Ticks;
            file.ExtraPracticeDayTicks = _extraPracticeDay == default ? 0 : _extraPracticeDay.Ticks;
        }

        void ReadExtraPractice(GameSaveFile file)
        {
            _extraPracticeWeek = file.ExtraPracticeWeekTicks > 0 ? new DateTime(file.ExtraPracticeWeekTicks) : default;
            _extraPracticeDay = file.ExtraPracticeDayTicks > 0 ? new DateTime(file.ExtraPracticeDayTicks) : default;
        }
    }
}
