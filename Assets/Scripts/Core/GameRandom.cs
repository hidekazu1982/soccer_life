using System;

namespace SoccerClub.Core
{
    /// <summary>
    /// 内部状態を保存できる乱数。Next の範囲は System.Random と同じ半開区間。
    /// </summary>
    public sealed class GameRandom
    {
        const ulong Mix = 0x2545F4914F6CDD1DUL;
        ulong _state;

        public ulong State => _state;

        public GameRandom(int seed)
        {
            ulong z = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            _state = z == 0UL ? 0x9E3779B97F4A7C15UL : z;
        }

        GameRandom(ulong state)
        {
            _state = state == 0UL ? 0x9E3779B97F4A7C15UL : state;
        }

        public static GameRandom Create()
        {
            int seed = Environment.TickCount ^ Guid.NewGuid().GetHashCode();
            return new GameRandom(seed);
        }

        public static GameRandom FromState(ulong state) => new GameRandom(state);

        public int Next() => (int)(NextRaw() >> 33);

        public int Next(int maxValue)
        {
            if (maxValue <= 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return (int)(NextDouble() * maxValue);
        }

        public int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
            long range = (long)maxValue - minValue;
            if (range <= 0L || range > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return minValue + (int)(NextDouble() * range);
        }

        public double NextDouble() => (NextRaw() >> 11) * (1.0 / (1UL << 53));

        ulong NextRaw()
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * Mix;
        }
    }
}
