using System.Globalization;
using System.Numerics;

namespace SoccerClub.Data
{
    /// <summary>
    /// 画面用の金額。9000円以下は100円単位で四捨五入して「9000円」。
    /// それを超える金額は1万円未満を切り捨て、兆・億・万で表す。
    /// </summary>
    public static class MoneyText
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Format(BigInteger amount)
        {
            if (amount < 0) return "-" + Format(-amount);
            if (amount <= 9000) return RoundHundred(amount).ToString(Invariant) + "円";

            var man = amount / 10000;
            if (man <= 0) man = 1;
            return Kanji(man);
        }

        static BigInteger RoundHundred(BigInteger amount)
        {
            var rem = amount % 100;
            if (rem == 0) return amount;
            if (rem >= 50) return amount + (100 - rem);
            return amount - rem;
        }

        static string Kanji(BigInteger man)
        {
            var cho = man / 100000000;
            var rest = man % 100000000;
            var oku = rest / 10000;
            var low = rest % 10000;
            var text = "";
            if (cho > 0) text += cho.ToString(Invariant) + "兆";
            if (oku > 0) text += oku.ToString(Invariant) + "億";
            if (low > 0) text += low.ToString(Invariant) + "万";
            if (text.Length == 0) text = "0";
            return text + "円";
        }
    }
}
