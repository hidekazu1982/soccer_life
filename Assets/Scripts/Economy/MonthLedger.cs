using System.Numerics;

namespace SoccerClub.Economy
{
    /// <summary>自クラブの、月初から月末までの収入と支出。</summary>
    public sealed class MonthLedger
    {
        public int Year;
        public int Month;
        public BigInteger Sponsor;
        public BigInteger Goods;
        public BigInteger Gate;
        public BigInteger Broadcast;
        public BigInteger Prize;
        public BigInteger Operating;
        public BigInteger Advertising;
        public BigInteger Development;
        public BigInteger Facilities;
        public BigInteger FrontWages;
        public BigInteger SquadWages;
        public BigInteger Reinforcement;
        public BigInteger Coins;

        public BigInteger Income => Sponsor + Goods + Gate + Broadcast + Prize;

        public BigInteger Expense =>
            Operating + Advertising + Development + Facilities + FrontWages + SquadWages + Reinforcement;

        public BigInteger Balance => Income - Expense;

        public void Clear()
        {
            Year = 0;
            Month = 0;
            Sponsor = BigInteger.Zero;
            Goods = BigInteger.Zero;
            Gate = BigInteger.Zero;
            Broadcast = BigInteger.Zero;
            Prize = BigInteger.Zero;
            Operating = BigInteger.Zero;
            Advertising = BigInteger.Zero;
            Development = BigInteger.Zero;
            Facilities = BigInteger.Zero;
            FrontWages = BigInteger.Zero;
            SquadWages = BigInteger.Zero;
            Reinforcement = BigInteger.Zero;
            Coins = BigInteger.Zero;
        }

        public MonthLedger Copy()
        {
            return new MonthLedger
            {
                Year = Year,
                Month = Month,
                Sponsor = Sponsor,
                Goods = Goods,
                Gate = Gate,
                Broadcast = Broadcast,
                Prize = Prize,
                Operating = Operating,
                Advertising = Advertising,
                Development = Development,
                Facilities = Facilities,
                FrontWages = FrontWages,
                SquadWages = SquadWages,
                Reinforcement = Reinforcement,
                Coins = Coins
            };
        }
    }
}
