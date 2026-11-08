using System;
using UnityEngine;

namespace SoccerClub.Core
{
    public static class PositionUtility
    {
        /// <summary>ポジションを持たない人物の髪。</summary>
        public static readonly Color32 NeutralHair = new Color32(0x4A, 0x3B, 0x36, 255);

        /// <summary>グループの基本色。髪の地色に使う。</summary>
        public static Color32 GroupColor(PositionGroup group)
        {
            switch (group)
            {
                case PositionGroup.GK:
                    return new Color32(0xC8, 0x4C, 0x4A, 255);
                case PositionGroup.DF:
                    return new Color32(0x5D, 0x8D, 0xB1, 255);
                case PositionGroup.MF:
                    return new Color32(0xCA, 0xC6, 0x5A, 255);
                case PositionGroup.FW:
                    return new Color32(0x75, 0xB0, 0x5E, 255);
                default:
                    return NeutralHair;
            }
        }

        public static PositionGroup GroupOf(Position position)
        {
            switch (position)
            {
                case Position.GK:
                    return PositionGroup.GK;
                case Position.CB:
                case Position.RSB:
                case Position.LSB:
                case Position.RWB:
                case Position.LWB:
                    return PositionGroup.DF;
                case Position.DM:
                case Position.AM:
                case Position.RSH:
                case Position.LSH:
                    return PositionGroup.MF;
                case Position.RW:
                case Position.LW:
                case Position.CF:
                    return PositionGroup.FW;
                default:
                    throw new ArgumentOutOfRangeException(nameof(position), position, null);
            }
        }

        public static float Multiplier(Suitability suitability)
        {
            return suitability == Suitability.None ? 0f : (int)suitability / 100f;
        }

        public static bool IsPlayable(Suitability suitability) => suitability != Suitability.None;

        public static Position[] PositionsIn(PositionGroup group)
        {
            switch (group)
            {
                case PositionGroup.GK:
                    return new[] { Position.GK };
                case PositionGroup.DF:
                    return new[] { Position.CB, Position.RSB, Position.LSB, Position.RWB, Position.LWB };
                case PositionGroup.MF:
                    return new[] { Position.DM, Position.AM, Position.RSH, Position.LSH };
                default:
                    return new[] { Position.RW, Position.LW, Position.CF };
            }
        }
    }
}
