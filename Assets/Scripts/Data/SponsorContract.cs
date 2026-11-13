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

        /// <summary>候補一覧での月額の倍率。解消条件が厳しい社ほど高い。結んだ契約では使わない（Monthly に反映済み）。</summary>
        public double Rate = 1d;
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

        /// <summary>いま結べるスポンサー契約の上限（リーグLVで増える）。0 のときは基本の上限を表示する。</summary>
        public int ContractCap;
        public int CutRank;
        public int CutLosses;
    }

    /// <summary>この試合で解消したスポンサー。条件は社ごとに違う。</summary>
    public sealed class SponsorCutNotice
    {
        public readonly List<SponsorContract> Contracts = new List<SponsorContract>();
    }

    /// <summary>
    /// スポンサー候補。解消条件は社ごとに 1 つ。
    /// 倍率は解消条件の厳しさに合わせる（厳しいほど高い）。12 社の平均は約 1.44 倍。
    /// </summary>
    public static class SponsorCatalog
    {
        public static readonly SponsorContract[] All =
        {
            Profile("のんびり鉄道", 10, 0, 2.0d),
            Profile("ひだまり銀行", 12, 0, 1.8d),
            Profile("きらきら通信", 14, 0, 1.6d),
            Profile("ころころ運輸", 15, 0, 1.5d),
            Profile("にこにこ食品", 16, 0, 1.4d),
            Profile("もふもふ乳業", 18, 0, 1.2d),
            Profile("まったり観光", 20, 0, 1.0d),
            Profile("ほのぼの保険", 0, 2, 1.8d),
            Profile("すくすく飲料", 0, 3, 1.5d),
            Profile("ふわふわ繊維", 0, 4, 1.3d),
            Profile("わくわく玩具", 0, 5, 1.15d),
            Profile("ぽかぽか電力", 0, 6, 1.0d)
        };

        static SponsorContract Profile(string name, int cutRank, int cutLosses, double rate)
        {
            return new SponsorContract
            {
                Name = name,
                CutRank = cutRank,
                CutLosses = cutLosses,
                Rate = rate
            };
        }
    }
}
