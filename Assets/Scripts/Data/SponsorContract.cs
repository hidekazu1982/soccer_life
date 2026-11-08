using System.Collections.Generic;
using System.Numerics;

namespace SoccerClub.Data
{
    /// <summary>締結済みのスポンサー。月額は毎月 1 日に入り、MonthsLeft 回で終わる。</summary>
    public sealed class SponsorContract
    {
        public string Name;
        public BigInteger Monthly;
        public int MonthsLeft;

        /// <summary>この順位以下で解消。0 なら順位では解消しない。</summary>
        public int CutRank;

        /// <summary>この連敗数以上で解消。0 なら連敗では解消しない。</summary>
        public int CutLosses;
    }

    /// <summary>営業が連れてきた、まだ決裁していないスポンサー。</summary>
    public sealed class SponsorOffer
    {
        public string SponsorName;
        public string StaffName;
        public BigInteger Monthly;
        public BigInteger Upfront;
        public int Months;
        public int ActiveContracts;
        public int CutRank;
        public int CutLosses;
    }

    /// <summary>この試合で解消したスポンサー。条件は社ごとに違う。</summary>
    public sealed class SponsorCutNotice
    {
        public readonly List<SponsorContract> Contracts = new List<SponsorContract>();
    }

    /// <summary>スポンサー候補。解消条件は社ごとに 1 つ。</summary>
    public static class SponsorCatalog
    {
        public static readonly SponsorContract[] All =
        {
            Profile("のんびり鉄道", 10, 0),
            Profile("ひだまり銀行", 12, 0),
            Profile("きらきら通信", 14, 0),
            Profile("ころころ運輸", 15, 0),
            Profile("にこにこ食品", 16, 0),
            Profile("もふもふ乳業", 18, 0),
            Profile("まったり観光", 20, 0),
            Profile("ほのぼの保険", 0, 2),
            Profile("すくすく飲料", 0, 3),
            Profile("ふわふわ繊維", 0, 4),
            Profile("わくわく玩具", 0, 5),
            Profile("ぽかぽか電力", 0, 6)
        };

        static SponsorContract Profile(string name, int cutRank, int cutLosses)
        {
            return new SponsorContract
            {
                Name = name,
                CutRank = cutRank,
                CutLosses = cutLosses
            };
        }
    }
}
