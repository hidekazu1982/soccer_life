using System;
using System.Collections.Generic;
using SoccerClub.Data;
using SoccerClub.World;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>クラブのユニフォーム配色。1st シャツ、2nd パンツ、3rd ソックス。</summary>
    public readonly struct UniformKit
    {
        public readonly Color32 First;
        public readonly Color32 Second;
        public readonly Color32 Third;

        public UniformKit(Color32 first, Color32 second, Color32 third)
        {
            First = first;
            Second = second;
            Third = third;
        }
    }

    /// <summary>
    /// Figma の character3 の部品を重ねて、1人分の待機・歩きテクスチャを作る。
    /// 部品は Resources/Character3。配置は layout.csv（Figma の 2048 キャンバスを 0.5 倍して切り抜いた座標）。
    /// 重ね順は Figma と同じ: 頭（肌、髪色、線）→ 体（肌、4th、3rd、2nd、1st、線）→ 口 → 目。
    /// 頭は front_head_01〜 の髪型ごとに head_NN_skin / head_NN_hair / head_NN_line を持ち、ID から 1 つ選ぶ。
    /// </summary>
    public sealed class AvatarLibrary
    {
        /// <summary>髪型が左右にはみ出すので、体の幅 BodyWidth の左右に 70 ずつ足してある。</summary>
        public const int SheetWidth = 811;
        public const int SheetHeight = 999;

        /// <summary>体（顔と胴）が収まる幅。シート中央に置く。枠に合わせて描く画面はこの幅で見た目を揃える。</summary>
        public const int BodyWidth = 671;

        /// <summary>シート幅に対する体の幅の割合。</summary>
        public const float BodyFraction = BodyWidth / (float)SheetWidth;
        public const int DisplayHeight = 168;
        public const int WalkFrames = 6;
        const int MaxHeads = 99;

        const string Folder = "Character3/";

        /// <summary>肌の色。skin_color の部品（頭と体）を塗る。ID から 1 色に決まる。</summary>
        static readonly Color32[] SkinColors =
        {
            new Color32(0xFD, 0xE7, 0xDA, 255),
            new Color32(0xF8, 0xD5, 0xC2, 255),
            new Color32(0xED, 0xB8, 0x94, 255),
            new Color32(0xD6, 0x9A, 0x6E, 255),
            new Color32(0xA8, 0x6B, 0x47, 255),
            new Color32(0x6E, 0x44, 0x30, 255)
        };

        /// <summary>color_4th（スパイク）は選手ごとに自由。ID から決まるのでいつ作り直しても同じ色。</summary>
        static readonly Color32[] SpikeColors =
        {
            new Color32(0x1A, 0x1A, 0x1A, 255),
            new Color32(0xFF, 0xFF, 0xFF, 255),
            new Color32(0xFF, 0xE0, 0x00, 255),
            new Color32(0xFF, 0x5C, 0xA8, 255),
            new Color32(0xFF, 0x8C, 0x00, 255),
            new Color32(0x7C, 0xFC, 0x00, 255),
            new Color32(0x00, 0xBF, 0xFF, 255),
            new Color32(0xE5, 0x20, 0x20, 255),
            new Color32(0xC0, 0xC0, 0xC0, 255),
            new Color32(0x20, 0x40, 0xE0, 255)
        };

        /// <summary>ポジションを持たない人物（スタッフ、フロント）の髪。黒と茶色。ID から決まる。</summary>
        static readonly Color32[] NaturalHairColors =
        {
            new Color32(0x1E, 0x1A, 0x1A, 255),
            new Color32(0x2B, 0x24, 0x22, 255),
            new Color32(0x3B, 0x2A, 0x20, 255),
            new Color32(0x4A, 0x3B, 0x36, 255),
            new Color32(0x5C, 0x3A, 0x21, 255),
            new Color32(0x7A, 0x4E, 0x2D, 255)
        };

        static readonly string[] BodyFills = { "_skin", "_c4", "_c3", "_c2", "_c1" };

        Head[] _heads = new Head[0];
        Part _mouth;
        Part _eyes;
        Pose _stand;
        Pose[] _front = new Pose[0];
        Pose[] _side = new Pose[0];

        readonly Dictionary<int, Scaled> _scaled = new Dictionary<int, Scaled>();
        int _displayWidth;

        public bool Ready { get; private set; }
        public int DisplayWidth => _displayWidth;

        public void Load()
        {
            var layout = LoadLayout();
            if (layout == null) return;
            _heads = LoadHeads(layout);
            _mouth = LoadPart(layout, "mouth", false);
            _eyes = LoadPart(layout, "eyes", false);
            _stand = LoadPose(layout, "stand");
            _front = LoadSeries(layout, "front_");
            _side = LoadSeries(layout, "side_");
            _scaled.Clear();
            _displayWidth = WidthFor(DisplayHeight);
            Ready = _heads.Length > 0 && _stand != null && _front.Length > 0 && _side.Length > 0;
            if (!Ready) Debug.LogError("character3 の部品が足りない: Resources/" + Folder);
        }

        /// <summary>名鑑の順。emblemIndex は 1 から。シャツの色（UI の差し色にも使う）。</summary>
        public static Color32 UniformColor(int emblemIndex)
        {
            return Kit(emblemIndex).First;
        }

        /// <summary>クラブのユニフォーム 3 色。CSV で空欄の色は 2nd←1st、3rd←2nd で埋まる。</summary>
        public static UniformKit Kit(int emblemIndex)
        {
            var club = Identity(emblemIndex);
            if (club == null)
            {
                var red = new Color32(0xFF, 0x17, 0x00, 255);
                return new UniformKit(red, red, red);
            }

            var c = club.Value;
            return new UniformKit(ToColor(c.Color1st), ToColor(c.Color2nd), ToColor(c.Color3rd));
        }

        /// <summary>背番号の文字色。白か黒。</summary>
        public static Color32 NumberColor(int emblemIndex)
        {
            var club = Identity(emblemIndex);
            bool white = club == null || club.Value.NumberTextIsWhite;
            return white ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255);
        }

        public static Color32 SpikeColor(string id)
        {
            if (string.IsNullOrEmpty(id)) return SpikeColors[0];
            return SpikeColors[Hash(id) % SpikeColors.Length];
        }

        /// <summary>肌の色。選手もスタッフも同じ決め方。髪型や髪色とは別の種で選ぶ。</summary>
        public static Color32 SkinColor(string id)
        {
            if (string.IsNullOrEmpty(id)) return SkinColors[0];
            return SkinColors[Hash(id + "#skin") % SkinColors.Length];
        }

        /// <summary>スタッフとフロントの髪色。黒か茶色を ID から選ぶ。</summary>
        public static Color32 NaturalHair(string id)
        {
            if (string.IsNullOrEmpty(id)) return NaturalHairColors[NaturalHairColors.Length / 2];
            return NaturalHairColors[Hash(id + "#hair") % NaturalHairColors.Length];
        }

        /// <summary>髪型の番号（0 から）。スパイクの色とは別の種で選ぶので、両者は連動しない。</summary>
        int HeadIndex(string id)
        {
            if (_heads.Length <= 1 || string.IsNullOrEmpty(id)) return 0;
            return Hash(id + "#head") % _heads.Length;
        }

        static WorldFactory.ClubIdentity? Identity(int emblemIndex)
        {
            int index = emblemIndex - 1;
            if (index < 0 || index >= WorldFactory.Clubs.Length)
                return null;
            return WorldFactory.Clubs[index];
        }

        static Color32 ToColor(int rgb)
        {
            return new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);
        }

        /// <summary>立ち、前歩き、横歩きのコマ。pixelHeight を省くとグラウンド用の高さ（DisplayHeight）。</summary>
        public AvatarFrames Create(string id, UniformKit kit, Color32 hairColor, int pixelHeight = 0)
        {
            if (!Ready || string.IsNullOrEmpty(id)) return null;
            var set = ScaledFor(pixelHeight > 0 ? pixelHeight : DisplayHeight);
            var colors = Colors(id, kit, hairColor);
            var head = set.Heads[HeadIndex(id)];
            var frames = new AvatarFrames
            {
                Stand = Compose(set, head, set.Stand, colors, false),
                WalkFront = new Texture2D[set.Front.Length],
                WalkSide = new Texture2D[set.Side.Length]
            };
            for (int i = 0; i < set.Front.Length; i++)
                frames.WalkFront[i] = Compose(set, head, set.Front[i], colors, false);
            for (int i = 0; i < set.Side.Length; i++)
                frames.WalkSide[i] = Compose(set, head, set.Side[i], colors, false);
            return frames;
        }

        /// <summary>待機ポーズ 1 枚。読み取り可能なテクスチャで返す。pixelHeight が 0 なら DisplayHeight。</summary>
        public Texture2D CreateStand(string id, UniformKit kit, Color32 hairColor, int pixelHeight = 0)
        {
            if (!Ready || string.IsNullOrEmpty(id)) return null;
            var set = ScaledFor(pixelHeight > 0 ? pixelHeight : DisplayHeight);
            return Compose(set, set.Heads[HeadIndex(id)], set.Stand, Colors(id, kit, hairColor), true);
        }

        public static void Destroy(AvatarFrames frames)
        {
            if (frames == null) return;
            DestroyTexture(frames.Stand);
            DestroyAll(frames.WalkFront);
            DestroyAll(frames.WalkSide);
        }

        static Color32[] Colors(string id, UniformKit kit, Color32 hair)
        {
            // 体の塗り順 BodyFills と同じ並び: 肌、4th、3rd、2nd、1st。最後は髪。
            return new[] { SkinColor(id), SpikeColor(id), kit.Third, kit.Second, kit.First, hair };
        }

        Texture2D Compose(Scaled set, ScaledHead head, ScaledPose pose, Color32[] colors, bool readable)
        {
            if (pose == null) return null;
            int dw = set.Width;
            int dh = set.Height;
            var buf = new Color32[dw * dh];
            if (head != null)
            {
                BlitMask(head.Skin, colors[0], buf, dw);
                BlitMask(head.Hair, colors[5], buf, dw);
                BlitLine(head.Line, buf, dw);
            }

            for (int i = 0; i < pose.Fills.Length; i++)
                BlitMask(pose.Fills[i], colors[i], buf, dw);
            BlitLine(pose.Line, buf, dw);
            BlitLine(set.Mouth, buf, dw);
            BlitLine(set.Eyes, buf, dw);

            var raw = new Color32[buf.Length];
            for (int y = 0; y < dh; y++)
                Array.Copy(buf, y * dw, raw, (dh - 1 - y) * dw, dw);

            var tex = new Texture2D(dw, dh, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.SetPixels32(raw);
            tex.Apply(false, !readable);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        static void BlitMask(ScaledPart part, Color32 tint, Color32[] buf, int dw)
        {
            if (part == null) return;
            for (int y = 0; y < part.H; y++)
            {
                int dst = (part.Y + y) * dw + part.X;
                int src = y * part.W;
                for (int x = 0; x < part.W; x++)
                {
                    byte a = part.Alpha[src + x];
                    if (a == 0) continue;
                    Blend(ref buf[dst + x], new Color32(tint.r, tint.g, tint.b, a));
                }
            }
        }

        static void BlitLine(ScaledPart part, Color32[] buf, int dw)
        {
            if (part == null) return;
            for (int y = 0; y < part.H; y++)
            {
                int dst = (part.Y + y) * dw + part.X;
                int src = y * part.W;
                for (int x = 0; x < part.W; x++)
                {
                    var c = part.Rgba[src + x];
                    if (c.a == 0) continue;
                    Blend(ref buf[dst + x], c);
                }
            }
        }

        static void Blend(ref Color32 dst, Color32 src)
        {
            if (src.a == 255 || dst.a == 0)
            {
                dst = src;
                return;
            }

            int sa = src.a;
            int da = dst.a * (255 - sa) / 255;
            int oa = sa + da;
            if (oa <= 0) return;
            dst.r = (byte)((src.r * sa + dst.r * da) / oa);
            dst.g = (byte)((src.g * sa + dst.g * da) / oa);
            dst.b = (byte)((src.b * sa + dst.b * da) / oa);
            dst.a = (byte)oa;
        }

        // ---- 表示サイズごとの縮小済み部品 ----

        static int WidthFor(int height)
        {
            return Mathf.Max(1, Mathf.RoundToInt(SheetWidth * (height / (float)SheetHeight)));
        }

        Scaled ScaledFor(int height)
        {
            // Resampler は縮小専用（元の画素を束ねる）。拡大すると隙間が空くので、元の高さで止める。
            // 大きく見せたいときは、この高さで作って描画時に引き伸ばす。
            height = Mathf.Clamp(height, 1, SheetHeight);
            Scaled set;
            if (_scaled.TryGetValue(height, out set)) return set;
            int width = WidthFor(height);
            var map = new Resampler(width, height);
            set = new Scaled
            {
                Width = width,
                Height = height,
                Heads = map.Scale(_heads),
                Mouth = map.Scale(_mouth),
                Eyes = map.Scale(_eyes),
                Stand = map.Scale(_stand),
                Front = map.Scale(_front),
                Side = map.Scale(_side)
            };
            _scaled[height] = set;
            return set;
        }

        /// <summary>
        /// シート座標の面積平均で縮小する。どの部品もシート全体の同じ格子に落とすので、塗りと線がずれない。
        /// </summary>
        sealed class Resampler
        {
            readonly int[] _colOf;
            readonly int[] _rowOf;
            readonly int[] _colCount;
            readonly int[] _rowCount;

            public Resampler(int width, int height)
            {
                _colOf = Bins(SheetWidth, width, out _colCount);
                _rowOf = Bins(SheetHeight, height, out _rowCount);
            }

            static int[] Bins(int source, int target, out int[] count)
            {
                var of = new int[source];
                count = new int[target];
                for (int i = 0; i < source; i++)
                {
                    int b = Mathf.Min(target - 1, (int)((long)i * target / source));
                    of[i] = b;
                    count[b]++;
                }

                return of;
            }

            public ScaledPose[] Scale(Pose[] poses)
            {
                var list = new ScaledPose[poses.Length];
                for (int i = 0; i < poses.Length; i++)
                    list[i] = Scale(poses[i]);
                return list;
            }

            public ScaledHead[] Scale(Head[] heads)
            {
                var list = new ScaledHead[heads.Length];
                for (int i = 0; i < heads.Length; i++)
                {
                    var head = heads[i];
                    list[i] = new ScaledHead { Skin = Scale(head.Skin), Hair = Scale(head.Hair), Line = Scale(head.Line) };
                }

                return list;
            }

            public ScaledPose Scale(Pose pose)
            {
                if (pose == null) return null;
                var fills = new ScaledPart[pose.Fills.Length];
                for (int i = 0; i < fills.Length; i++)
                    fills[i] = Scale(pose.Fills[i]);
                return new ScaledPose { Fills = fills, Line = Scale(pose.Line) };
            }

            public ScaledPart Scale(Part part)
            {
                if (part == null) return null;
                int bx0 = _colOf[Mathf.Clamp(part.X, 0, SheetWidth - 1)];
                int by0 = _rowOf[Mathf.Clamp(part.Y, 0, SheetHeight - 1)];
                int bx1 = _colOf[Mathf.Clamp(part.X + part.W - 1, 0, SheetWidth - 1)];
                int by1 = _rowOf[Mathf.Clamp(part.Y + part.H - 1, 0, SheetHeight - 1)];
                int w = bx1 - bx0 + 1;
                int h = by1 - by0 + 1;
                bool mask = part.Rgba == null;
                var sa = new int[w * h];
                var sr = mask ? null : new int[w * h];
                var sg = mask ? null : new int[w * h];
                var sb = mask ? null : new int[w * h];
                for (int y = 0; y < part.H; y++)
                {
                    int sy = part.Y + y;
                    if ((uint)sy >= SheetHeight) continue;
                    int row = (_rowOf[sy] - by0) * w;
                    for (int x = 0; x < part.W; x++)
                    {
                        int sx = part.X + x;
                        if ((uint)sx >= SheetWidth) continue;
                        int bin = row + _colOf[sx] - bx0;
                        int i = y * part.W + x;
                        if (mask)
                        {
                            sa[bin] += part.Alpha[i];
                        }
                        else
                        {
                            var c = part.Rgba[i];
                            sa[bin] += c.a;
                            sr[bin] += c.r * c.a;
                            sg[bin] += c.g * c.a;
                            sb[bin] += c.b * c.a;
                        }
                    }
                }

                var result = new ScaledPart { X = bx0, Y = by0, W = w, H = h };
                if (mask) result.Alpha = new byte[w * h];
                else result.Rgba = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    int ry = _rowCount[by0 + y];
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        int area = ry * _colCount[bx0 + x];
                        if (area <= 0 || sa[i] == 0) continue;
                        byte a = (byte)Mathf.Min(255, sa[i] / area);
                        if (mask)
                        {
                            result.Alpha[i] = a;
                        }
                        else
                        {
                            result.Rgba[i] = new Color32(
                                (byte)(sr[i] / sa[i]),
                                (byte)(sg[i] / sa[i]),
                                (byte)(sb[i] / sa[i]),
                                a);
                        }
                    }
                }

                return result;
            }
        }

        // ---- 読み込み ----

        static Dictionary<string, Vector2Int> LoadLayout()
        {
            var asset = Resources.Load<TextAsset>(Folder + "layout");
            if (asset == null)
            {
                Debug.LogError("character3 の layout.csv が見つからない: Resources/" + Folder);
                return null;
            }

            var map = new Dictionary<string, Vector2Int>();
            var rows = MasterCsv.Parse(asset.text);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Length < 3) continue;
                int x, y;
                if (!int.TryParse(row[1].Trim(), out x) || !int.TryParse(row[2].Trim(), out y)) continue;
                map[row[0].Trim()] = new Vector2Int(x, y);
            }

            Vector2Int sheet;
            if (map.TryGetValue("sheet", out sheet) && (sheet.x != SheetWidth || sheet.y != SheetHeight))
                Debug.LogWarning("character3 のシートサイズが AvatarLibrary の定数と違う: " + sheet);
            return map;
        }

        /// <summary>head_01_* から番号が途切れるまで読む。線画が無い番号で止める。</summary>
        static Head[] LoadHeads(Dictionary<string, Vector2Int> layout)
        {
            var list = new List<Head>();
            for (int i = 1; i <= MaxHeads; i++)
            {
                string name = "head_" + i.ToString("00");
                var line = LoadPart(layout, name + "_line", false);
                if (line == null) break;
                list.Add(new Head
                {
                    Skin = LoadPart(layout, name + "_skin", true),
                    Hair = LoadPart(layout, name + "_hair", true),
                    Line = line
                });
            }

            return list.ToArray();
        }

        static Pose[] LoadSeries(Dictionary<string, Vector2Int> layout, string prefix)
        {
            var list = new List<Pose>(WalkFrames);
            for (int i = 1; i <= WalkFrames; i++)
            {
                var pose = LoadPose(layout, prefix + i.ToString("00"));
                if (pose != null) list.Add(pose);
            }

            return list.ToArray();
        }

        static Pose LoadPose(Dictionary<string, Vector2Int> layout, string name)
        {
            var line = LoadPart(layout, name + "_line", false);
            if (line == null) return null;
            var fills = new Part[BodyFills.Length];
            for (int i = 0; i < fills.Length; i++)
                fills[i] = LoadPart(layout, name + BodyFills[i], true);
            return new Pose { Fills = fills, Line = line };
        }

        static Part LoadPart(Dictionary<string, Vector2Int> layout, string name, bool mask)
        {
            Vector2Int pos;
            if (!layout.TryGetValue(name, out pos)) return null;
            var asset = Resources.Load<TextAsset>(Folder + name);
            if (asset == null) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(asset.bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            var raw = tex.GetPixels32();
            int w = tex.width;
            int h = tex.height;
            UnityEngine.Object.Destroy(tex);
            var part = new Part { X = pos.x, Y = pos.y, W = w, H = h };
            // Texture2D は下から上の並びなので、上から下に直す。
            if (mask)
            {
                part.Alpha = new byte[w * h];
                for (int y = 0; y < h; y++)
                {
                    int src = (h - 1 - y) * w;
                    int dst = y * w;
                    for (int x = 0; x < w; x++)
                        part.Alpha[dst + x] = raw[src + x].a;
                }
            }
            else
            {
                part.Rgba = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    Array.Copy(raw, (h - 1 - y) * w, part.Rgba, y * w, w);
            }

            return part;
        }

        static int Hash(string id)
        {
            unchecked
            {
                int h = 216613626;
                for (int i = 0; i < id.Length; i++)
                    h = (h ^ id[i]) * 16777619;
                return h & int.MaxValue;
            }
        }

        static void DestroyAll(Texture2D[] frames)
        {
            if (frames == null) return;
            for (int i = 0; i < frames.Length; i++)
                DestroyTexture(frames[i]);
        }

        static void DestroyTexture(Texture2D texture)
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
        }

        /// <summary>シート座標の部品。塗りは Alpha だけ、線と顔は Rgba。</summary>
        sealed class Part
        {
            public int X;
            public int Y;
            public int W;
            public int H;
            public byte[] Alpha;
            public Color32[] Rgba;
        }

        sealed class Head
        {
            public Part Skin;
            public Part Hair;
            public Part Line;
        }

        sealed class Pose
        {
            public Part[] Fills;
            public Part Line;
        }

        sealed class ScaledPart
        {
            public int X;
            public int Y;
            public int W;
            public int H;
            public byte[] Alpha;
            public Color32[] Rgba;
        }

        sealed class ScaledHead
        {
            public ScaledPart Skin;
            public ScaledPart Hair;
            public ScaledPart Line;
        }

        sealed class ScaledPose
        {
            public ScaledPart[] Fills;
            public ScaledPart Line;
        }

        sealed class Scaled
        {
            public int Width;
            public int Height;
            public ScaledHead[] Heads;
            public ScaledPart Mouth;
            public ScaledPart Eyes;
            public ScaledPose Stand;
            public ScaledPose[] Front;
            public ScaledPose[] Side;
        }
    }

    public sealed class AvatarFrames
    {
        public Texture2D Stand;
        public Texture2D[] WalkFront;
        public Texture2D[] WalkSide;
    }
}
