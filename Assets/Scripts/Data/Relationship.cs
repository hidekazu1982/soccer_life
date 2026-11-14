using System;
using SoccerClub.Core;

namespace SoccerClub.Data
{
    /// <summary>
    /// 選手2人の関係。PlayerIdA と PlayerIdB は常に文字列の小さい順に揃える。
    /// </summary>
    [Serializable]
    public sealed class Relationship
    {
        public string PlayerIdA;
        public string PlayerIdB;
        public RelationStage Stage;

        /// <summary>0〜100。</summary>
        public int Affinity;

        /// <summary>今の段階になった日。</summary>
        public DateTime Since;

        public bool Involves(string playerId)
        {
            return playerId != null && (PlayerIdA == playerId || PlayerIdB == playerId);
        }

        /// <summary>playerId の相手。関係していなければ null。</summary>
        public string Other(string playerId)
        {
            if (playerId == PlayerIdA) return PlayerIdB;
            if (playerId == PlayerIdB) return PlayerIdA;
            return null;
        }

        /// <summary>2つの id を小さい順に並べる。</summary>
        public static void Order(string a, string b, out string first, out string second)
        {
            if (string.CompareOrdinal(a, b) <= 0)
            {
                first = a;
                second = b;
            }
            else
            {
                first = b;
                second = a;
            }
        }
    }

    /// <summary>
    /// id から決まる値。乱数を使わないので、ワールド生成やシミュレーターの乱数の並びを変えない。
    /// </summary>
    public static class PlayerTraits
    {
        /// <summary>性別。id から 5:5 で決まる。</summary>
        public static Gender GenderForId(string id)
        {
            return (Hash(id, 0x5EED) & 1u) == 0u ? Gender.Male : Gender.Female;
        }

        /// <summary>2人の相性。0 以上 1 未満。順番に関係なく同じ値になる。</summary>
        public static double Compatibility(string a, string b)
        {
            Relationship.Order(a, b, out var first, out var second);
            uint h = Mix(Hash(first, 0xC0A1) ^ (Hash(second, 0xC0B2) * 0x9E3779B1u));
            return (h >> 8) * (1.0 / (1u << 24));
        }

        /// <summary>2人と月から決まる 0〜(range-1) のぶれ。</summary>
        public static int MonthlyNoise(string a, string b, int monthIndex, int range)
        {
            if (range <= 1) return 0;
            Relationship.Order(a, b, out var first, out var second);
            uint h = Mix(Hash(first, 0xA11C) ^ (Hash(second, 0xB0B5) * 0x85EBCA6Bu) ^ ((uint)monthIndex * 0xC2B2AE35u));
            return (int)(h % (uint)range);
        }

        static uint Hash(string text, uint salt)
        {
            uint h = 2166136261u ^ salt;
            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    h ^= text[i];
                    h = unchecked(h * 16777619u);
                }
            }

            return Mix(h);
        }

        static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
