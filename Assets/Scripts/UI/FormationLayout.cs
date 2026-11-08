using SoccerClub.Core;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// match_preparation の position 枠。値はカード（260×176）の左上。
    /// 並びは Formation.Slots と同じ。
    /// </summary>
    public static class FormationLayout
    {
        static readonly Vector2 Gk = new Vector2(830f, 821f);
        static readonly Vector2 Lwb = new Vector2(294f, 549f);
        static readonly Vector2 Rwb = new Vector2(1366f, 550f);
        static readonly Vector2 RwbC = new Vector2(1366f, 549f);
        static readonly Vector2 Cb3L = new Vector2(562f, 637f);
        static readonly Vector2 Cb3C = new Vector2(830f, 637f);
        static readonly Vector2 Cb3R = new Vector2(1098f, 637f);
        static readonly Vector2 Lsb = new Vector2(428f, 638f);
        static readonly Vector2 Cb4L = new Vector2(696f, 637f);
        static readonly Vector2 Cb4R = new Vector2(964f, 637f);
        static readonly Vector2 Rsb = new Vector2(1232f, 637f);
        static readonly Vector2 DmL = new Vector2(696f, 454f);
        static readonly Vector2 DmR = new Vector2(964f, 453f);
        static readonly Vector2 DmC = new Vector2(830f, 453f);
        static readonly Vector2 ShL = new Vector2(428f, 269f);
        static readonly Vector2 ShR = new Vector2(1232f, 269f);
        static readonly Vector2 ShLLow = new Vector2(428f, 268f);
        static readonly Vector2 ShRLow = new Vector2(1232f, 268f);
        static readonly Vector2 AmL = new Vector2(562f, 269f);
        static readonly Vector2 AmC = new Vector2(830f, 269f);
        static readonly Vector2 AmR = new Vector2(1098f, 269f);
        static readonly Vector2 AmInL = new Vector2(696f, 269f);
        static readonly Vector2 AmInR = new Vector2(964f, 269f);
        static readonly Vector2 CfL = new Vector2(696f, 85f);
        static readonly Vector2 CfC = new Vector2(830f, 85f);
        static readonly Vector2 CfR = new Vector2(964f, 85f);
        static readonly Vector2 Lw = new Vector2(428f, 84f);
        static readonly Vector2 Rw = new Vector2(1232f, 85f);

        static readonly Vector2[] F541A = { Gk, Lwb, Cb3L, Cb3C, Cb3R, Rwb, DmL, DmR, AmL, AmR, CfC };
        static readonly Vector2[] F541B = { Gk, Lwb, Cb3L, Cb3C, Cb3R, Rwb, DmL, DmR, ShLLow, ShRLow, CfC };
        static readonly Vector2[] F541C = { Gk, Lwb, Cb3L, Cb3C, Cb3R, RwbC, DmC, ShL, AmC, ShR, CfC };
        static readonly Vector2[] F532A = { Gk, Lwb, Cb3L, Cb3C, Cb3R, Rwb, DmL, DmR, AmC, CfL, CfR };
        static readonly Vector2[] F532B = { Gk, Lwb, Cb3L, Cb3C, Cb3R, Rwb, DmC, AmInL, AmInR, CfL, CfR };
        static readonly Vector2[] F451A = { Gk, Lsb, Cb4L, Cb4R, Rsb, DmL, DmR, AmL, AmC, AmR, CfC };
        static readonly Vector2[] F451B = { Gk, Lsb, Cb4L, Cb4R, Rsb, DmC, ShL, AmInL, AmInR, ShR, CfC };
        static readonly Vector2[] F442A = { Gk, Lsb, Cb4L, Cb4R, Rsb, DmL, DmR, AmL, AmR, CfL, CfR };
        static readonly Vector2[] F442B = { Gk, Lsb, Cb4L, Cb4R, Rsb, DmL, DmR, ShL, ShR, CfL, CfR };
        static readonly Vector2[] F442C = { Gk, Lsb, Cb4L, Cb4R, Rsb, DmC, ShL, AmC, ShR, CfL, CfR };
        static readonly Vector2[] F433A = { Gk, Lsb, Cb4L, Cb4R, Rsb, DmL, DmR, AmC, Lw, CfC, Rw };
        static readonly Vector2[] F433B = { Gk, Lsb, Cb4L, Cb4R, Rsb, DmC, AmInL, AmInR, Lw, CfC, Rw };
        static readonly Vector2[] F352 = { Gk, Cb3L, Cb3C, Cb3R, DmL, DmR, ShL, AmC, ShR, CfL, CfR };
        static readonly Vector2[] F343A = { Gk, Cb3L, Cb3C, Cb3R, DmL, DmR, AmL, AmR, Lw, CfC, Rw };
        static readonly Vector2[] F343B = { Gk, Cb3L, Cb3C, Cb3R, DmL, DmR, ShL, ShR, Lw, CfC, Rw };
        static readonly Vector2[] F343C = { Gk, Cb3L, Cb3C, Cb3R, DmC, ShL, AmC, ShR, Lw, CfC, Rw };

        public static Vector2 Card(FormationType type, int index)
        {
            var cards = Cards(type);
            if (cards == null || index < 0 || index >= cards.Length) return Gk;
            return cards[index];
        }

        static Vector2[] Cards(FormationType type)
        {
            switch (type)
            {
                case FormationType.F541A: return F541A;
                case FormationType.F541B: return F541B;
                case FormationType.F541C: return F541C;
                case FormationType.F532A: return F532A;
                case FormationType.F532B: return F532B;
                case FormationType.F451A: return F451A;
                case FormationType.F451B: return F451B;
                case FormationType.F442A: return F442A;
                case FormationType.F442B: return F442B;
                case FormationType.F442C: return F442C;
                case FormationType.F433A: return F433A;
                case FormationType.F433B: return F433B;
                case FormationType.F352: return F352;
                case FormationType.F343A: return F343A;
                case FormationType.F343B: return F343B;
                case FormationType.F343C: return F343C;
                default: return F433A;
            }
        }
    }
}
