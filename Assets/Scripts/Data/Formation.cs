using System;
using SoccerClub.Core;

namespace SoccerClub.Data
{
    /// <summary>監督が得意とするシステムのスロット定義。先頭は必ず GK。左から右。</summary>
    public sealed class Formation
    {
        public FormationType Type { get; }
        public string DisplayName { get; }
        public Position[] Slots { get; }

        public Formation(FormationType type, string displayName, Position[] slots)
        {
            Type = type;
            DisplayName = displayName;
            Slots = slots;
        }
    }

    public static class FormationCatalog
    {
        public static readonly Formation F541A = Line(FormationType.F541A, "5-4-1 A",
            Position.GK, Position.LWB, Position.CB, Position.CB, Position.CB, Position.RWB,
            Position.DM, Position.DM, Position.AM, Position.AM, Position.CF);

        public static readonly Formation F541B = Line(FormationType.F541B, "5-4-1 B",
            Position.GK, Position.LWB, Position.CB, Position.CB, Position.CB, Position.RWB,
            Position.DM, Position.DM, Position.LSH, Position.RSH, Position.CF);

        public static readonly Formation F541C = Line(FormationType.F541C, "5-4-1 C",
            Position.GK, Position.LWB, Position.CB, Position.CB, Position.CB, Position.RWB,
            Position.DM, Position.LSH, Position.AM, Position.RSH, Position.CF);

        public static readonly Formation F532A = Line(FormationType.F532A, "5-3-2 A",
            Position.GK, Position.LWB, Position.CB, Position.CB, Position.CB, Position.RWB,
            Position.DM, Position.DM, Position.AM, Position.CF, Position.CF);

        public static readonly Formation F532B = Line(FormationType.F532B, "5-3-2 B",
            Position.GK, Position.LWB, Position.CB, Position.CB, Position.CB, Position.RWB,
            Position.DM, Position.AM, Position.AM, Position.CF, Position.CF);

        public static readonly Formation F451A = Line(FormationType.F451A, "4-5-1 A",
            Position.GK, Position.LSB, Position.CB, Position.CB, Position.RSB,
            Position.DM, Position.DM, Position.LSH, Position.AM, Position.RSH, Position.CF);

        public static readonly Formation F451B = Line(FormationType.F451B, "4-5-1 B",
            Position.GK, Position.LSB, Position.CB, Position.CB, Position.RSB,
            Position.DM, Position.LSH, Position.AM, Position.AM, Position.RSH, Position.CF);

        public static readonly Formation F442A = Line(FormationType.F442A, "4-4-2 A",
            Position.GK, Position.LSB, Position.CB, Position.CB, Position.RSB,
            Position.DM, Position.DM, Position.AM, Position.AM, Position.CF, Position.CF);

        public static readonly Formation F442B = Line(FormationType.F442B, "4-4-2 B",
            Position.GK, Position.LSB, Position.CB, Position.CB, Position.RSB,
            Position.DM, Position.DM, Position.LSH, Position.RSH, Position.CF, Position.CF);

        public static readonly Formation F442C = Line(FormationType.F442C, "4-4-2 C",
            Position.GK, Position.LSB, Position.CB, Position.CB, Position.RSB,
            Position.DM, Position.LSH, Position.AM, Position.RSH, Position.CF, Position.CF);

        public static readonly Formation F433A = Line(FormationType.F433A, "4-3-3 A",
            Position.GK, Position.LSB, Position.CB, Position.CB, Position.RSB,
            Position.DM, Position.DM, Position.AM, Position.LW, Position.CF, Position.RW);

        public static readonly Formation F433B = Line(FormationType.F433B, "4-3-3 B",
            Position.GK, Position.LSB, Position.CB, Position.CB, Position.RSB,
            Position.DM, Position.AM, Position.AM, Position.LW, Position.CF, Position.RW);

        public static readonly Formation F352 = Line(FormationType.F352, "3-5-2",
            Position.GK, Position.CB, Position.CB, Position.CB,
            Position.DM, Position.DM, Position.LSH, Position.AM, Position.RSH, Position.CF, Position.CF);

        public static readonly Formation F343A = Line(FormationType.F343A, "3-4-3 A",
            Position.GK, Position.CB, Position.CB, Position.CB,
            Position.DM, Position.DM, Position.AM, Position.AM, Position.LW, Position.CF, Position.RW);

        public static readonly Formation F343B = Line(FormationType.F343B, "3-4-3 B",
            Position.GK, Position.CB, Position.CB, Position.CB,
            Position.DM, Position.DM, Position.LSH, Position.RSH, Position.LW, Position.CF, Position.RW);

        public static readonly Formation F343C = Line(FormationType.F343C, "3-4-3 C",
            Position.GK, Position.CB, Position.CB, Position.CB,
            Position.DM, Position.LSH, Position.AM, Position.RSH, Position.LW, Position.CF, Position.RW);

        public static readonly Formation[] All =
        {
            F541A, F541B, F541C, F532A, F532B, F451A, F451B,
            F442A, F442B, F442C, F433A, F433B, F352,
            F343A, F343B, F343C
        };

        public static Formation Get(FormationType type)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Type == type) return All[i];
            }

            return F433A;
        }

        public static FormationType Clamp(FormationType type)
        {
            return Enum.IsDefined(typeof(FormationType), type) ? type : FormationType.F433A;
        }

        static Formation Line(FormationType type, string name, params Position[] slots)
        {
            return new Formation(type, name, slots);
        }
    }
}
