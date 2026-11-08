using System;

namespace SoccerClub.Time
{
    /// <summary>
    /// リーグは土曜、カップは水曜。
    /// リーグ前半の最終節の翌日から、後半初戦の前日まで試合は組まない。
    /// 後半は 2 月 1 日以降、かつ前半最終節より後の最初の土曜に開く。
    /// </summary>
    public static class WinterBreak
    {
        public const int FirstHalfRounds = 19;
        public const int CupLegCount = 10;

        public static DateTime SaturdayOnOrAfter(DateTime date)
        {
            var day = date.Date;
            int delta = ((int)DayOfWeek.Saturday - (int)day.DayOfWeek + 7) % 7;
            return day.AddDays(delta);
        }

        public static DateTime FirstHalfOpener(DateTime seasonStart)
        {
            return SaturdayOnOrAfter(seasonStart.Date.AddDays(7));
        }

        /// <summary>前半のリーグ戦日。初戦から 7 日おきの土曜。</summary>
        public static DateTime[] FirstHalfLeagueDates(DateTime seasonStart)
        {
            var dates = new DateTime[FirstHalfRounds];
            var date = FirstHalfOpener(seasonStart);
            for (int i = 0; i < dates.Length; i++)
            {
                dates[i] = date;
                date = date.AddDays(7);
            }

            return dates;
        }

        /// <summary>
        /// カップの各レグ。初戦の次の水曜から。1回戦の翌週は空け、2回戦以降は水曜が続く。
        /// </summary>
        public static DateTime[] CupDates(DateTime seasonStart)
        {
            var round1 = FirstHalfOpener(seasonStart).AddDays(4);
            var dates = new DateTime[CupLegCount];
            dates[0] = round1;
            dates[1] = round1.AddDays(7);
            var round2 = round1.AddDays(21);
            for (int i = 2; i < dates.Length; i++)
                dates[i] = round2.AddDays(7 * (i - 2));
            return dates;
        }

        public static DateTime[] SecondHalfLeagueDates(DateTime seasonStart)
        {
            var dates = new DateTime[FirstHalfRounds];
            var date = SecondHalfOpener(seasonStart);
            for (int i = 0; i < dates.Length; i++)
            {
                dates[i] = date;
                date = date.AddDays(7);
            }

            return dates;
        }

        public static DateTime LastFirstHalf(DateTime seasonStart)
        {
            var dates = FirstHalfLeagueDates(seasonStart);
            return dates[dates.Length - 1];
        }

        public static DateTime SecondHalfOpener(DateTime seasonStart)
        {
            var last = LastFirstHalf(seasonStart);
            int year = last.Month >= 8 ? last.Year + 1 : last.Year;
            var resume = SaturdayOnOrAfter(new DateTime(year, 2, 1));
            while (resume <= last) resume = resume.AddDays(7);
            return resume;
        }

        public static DateTime Start(DateTime seasonStart)
        {
            return LastFirstHalf(seasonStart).AddDays(1);
        }

        public static DateTime End(DateTime seasonStart)
        {
            return SecondHalfOpener(seasonStart).AddDays(-1);
        }

        public static bool Contains(DateTime seasonStart, DateTime date)
        {
            var day = date.Date;
            return day >= Start(seasonStart) && day <= End(seasonStart);
        }
    }
}
