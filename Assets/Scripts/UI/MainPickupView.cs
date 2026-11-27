using System;
using System.Collections.Generic;
using System.Globalization;
using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;
using BigInteger = System.Numerics.BigInteger;
using Random = UnityEngine.Random;

namespace SoccerClub.UI
{
    /// <summary>
    /// メイン画面の拾えるアイコン（版 1.65）。歩いているキャラの頭上に、お金・OFF・DEF・STA のアイコンをときどき出し、
    /// タップすると獲得して「＋」の文字がふわっと上がる（キャラの演出はなし）。
    /// ・1 日に出るのは GameSession.PickupsPerDay 個まで。画面に同時に出るのは 2 個まで。日が変わると消える。
    /// ・OFF・DEF・STA は自クラブの選手だけ（その選手の能力が上がる）。スタッフとフロントはお金だけ。
    /// ・吹き出しアイコンを出しているキャラには出さない。12 秒たつと消える。
    /// ・フルオートでは出てから 1.5 秒で自動で拾う。
    /// </summary>
    public sealed class MainPickupView
    {
        const float Size = 76f;
        const float HitPad = 18f;
        const float Lift = 10f;
        const float Life = 12f;
        const float FadeSeconds = 1f;
        const float PopInSeconds = 0.3f;
        const float AutoCollectSeconds = 1.5f;
        const int MaxOnScreen = 2;
        const float FirstSpawnDelay = 2f;
        const float SpawnMin = 4f;
        const float SpawnMax = 8f;
        const float GainSeconds = 1.3f;
        const float GainRise = 80f;

        static readonly Color Edge = new Color(0.29f, 0.18f, 0.078f, 1f);
        static readonly Color Cream = new Color(1f, 0.992f, 0.965f, 1f);
        static readonly Color MoneyColor = new Color(0.96f, 0.77f, 0.26f, 1f);
        static readonly Color StatColor = new Color(0.18f, 0.37f, 0.62f, 1f);

        sealed class Item
        {
            public string Id;
            public PickupKind Kind;
            public float Age;
            public Rect Last;
            public bool Shown;
        }

        sealed class Gain
        {
            public string Id;
            public Vector2 At;
            public string Text;
            public Color Color;
            public float Age;
        }

        readonly List<Item> _items = new List<Item>();
        readonly List<Gain> _gains = new List<Gain>();
        readonly List<string> _ids = new List<string>();
        long _day = -1;
        int _spawnedToday;
        float _nextSpawn = FirstSpawnDelay;
        Texture2D _coin;
        Texture2D _offense;
        Texture2D _defense;
        Texture2D _stamina;
        Texture2D _disc;
        GUIStyle _gainStyle;
        GUIStyle _hit;
        Font _font;

        /// <summary>能力を拾ったとき（選手名、能力、表示上の増分）。メイン画面左のログに足す。</summary>
        public event Action<string, GrowthStat, int> StatCollected;

        /// <summary>アイコンが新しく出たとき（キャラの ID）。カメラの注視の列に入れる（版 1.66）。</summary>
        public event Action<string> Spawned;

        /// <summary>そのキャラのアイコンを拾う（カメラが寄っているときの画面タップ用）。拾えたら true。</summary>
        public bool CollectFor(string id, GameSession session)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Id != id || !_items[i].Shown) continue;
                Collect(_items[i], session);
                return true;
            }

            return false;
        }

        public void Clear()
        {
            _items.Clear();
            _gains.Clear();
        }

        /// <summary>カメラが注視しているアイコンのキャラ。注視の間は時間切れでも日が変わっても消さない（版 1.68）。</summary>
        public string HoldId { get; set; }

        /// <summary>
        /// 新しいアイコンを出さない（版 1.69）。カメラが別のものを注視している・注視を待っている間は止め、
        /// 「アイコンが出ている＝カメラが寄っている」になるようにする。フルオートでは使わない。
        /// </summary>
        public bool SpawnPaused { get; set; }

        /// <summary>注視の時間が終わっても拾われなかったアイコンを消す（版 1.69）。</summary>
        public void Expire(string id)
        {
            _items.RemoveAll(it => it.Id == id);
        }

        /// <summary>active はメイン画面が見えて操作できるとき。そうでない間は時間を止める（拾った文字だけは進める）。</summary>
        public void Tick(float deltaTime, bool active, bool fullAuto, GameSession session, GroundCrowd crowd, HashSet<string> reserved)
        {
            for (int i = _gains.Count - 1; i >= 0; i--)
            {
                _gains[i].Age += deltaTime;
                if (_gains[i].Age >= GainSeconds) _gains.RemoveAt(i);
            }

            if (session == null || crowd == null) return;
            long day = session.Time.CurrentDate.Date.Ticks;
            if (day != _day)
            {
                _day = day;
                _spawnedToday = 0;
                _items.RemoveAll(it => HoldId == null || it.Id != HoldId);
                _nextSpawn = FirstSpawnDelay;
            }

            if (!active) return;

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                item.Age += deltaTime;
                // 注視している間は時間切れにしない（薄くなり始める手前で止める）。
                // 版 1.68 では年齢ごと止めていたため、出た直後に注視されると大きさ 0 のまま見えなかった。
                if (item.Id == HoldId && item.Age > Life - FadeSeconds) item.Age = Life - FadeSeconds;
                if (item.Age >= Life)
                {
                    _items.RemoveAt(i);
                    continue;
                }

                if (fullAuto && item.Age >= AutoCollectSeconds)
                    Collect(item, session);
            }

            if (SpawnPaused && !fullAuto) return;
            _nextSpawn -= deltaTime;
            if (_nextSpawn > 0f) return;
            _nextSpawn = Random.Range(SpawnMin, SpawnMax);
            if (_items.Count >= MaxOnScreen || _spawnedToday >= GameSession.PickupsPerDay) return;

            crowd.CopyVisibleIds(_ids);
            for (int i = _ids.Count - 1; i >= 0; i--)
            {
                var id = _ids[i];
                Vector2 head;
                if (string.IsNullOrEmpty(id) || (reserved != null && reserved.Contains(id)) || Has(id) || !crowd.TryHeadPoint(id, out head))
                    _ids.RemoveAt(i);
            }

            if (_ids.Count == 0) return;
            var pick = _ids[Random.Range(0, _ids.Count)];
            var kind = PickupKind.Money;
            if (session.IsSquadPlayer(pick))
            {
                // 選手はお金 4 割、OFF・DEF・STA 2 割ずつ。
                float roll = Random.value;
                kind = roll < 0.4f ? PickupKind.Money : roll < 0.6f ? PickupKind.Offense : roll < 0.8f ? PickupKind.Defense : PickupKind.Stamina;
            }

            _items.Add(new Item { Id = pick, Kind = kind });
            _spawnedToday++;
            Spawned?.Invoke(pick);
        }

        /// <summary>そのキャラで拾った「＋」の文字がまだ上がっている途中か（カメラはこの間は引かない。版 1.67）。</summary>
        public bool GainActive(string id)
        {
            for (int i = 0; i < _gains.Count; i++)
                if (_gains[i].Id == id) return true;
            return false;
        }

        /// <summary>そのキャラのアイコンがまだある（拾われていない・消えていない）か。出た直後の描く前でも true。</summary>
        public bool Has(string id)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Id == id) return true;
            return false;
        }

        /// <summary>
        /// アイコンと拾った文字を描く（chrome 座標）。toChrome はワールド座標を chrome 座標へ。
        /// hitRects にアイコンの当たりを足す（キャラのタップと重ならないように）。
        /// showIcons が false のときは拾った文字だけ描く。
        /// </summary>
        public void Draw(GameSession session, GroundCrowd crowd, Func<Vector2, Vector2> toChrome, List<Rect> hitRects, HashSet<string> bubbled, bool showIcons, Font font)
        {
            EnsureAssets(font);
            var previous = GUI.color;
            float now = UnityEngine.Time.unscaledTime;
            if (showIcons && crowd != null)
            {
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    var item = _items[i];
                    Vector2 head;
                    item.Shown = crowd.TryHeadPoint(item.Id, out head);
                    if (!item.Shown) continue;
                    var point = toChrome(head);
                    // 同じキャラに吹き出しが出たら、横にずらして重ねない。
                    if (bubbled != null && bubbled.Contains(item.Id)) point.x += HeadBubbleView.Size * 0.5f + Size * 0.5f + 8f;
                    float pop = Mathf.Clamp01(item.Age / PopInSeconds);
                    float scale = pop < 1f ? EaseOutBack(pop) : 1f;
                    float bob = Mathf.Sin(now * 3.2f + i) * 5f;
                    float fade = Mathf.Clamp01((Life - item.Age) / FadeSeconds);
                    float size = Size * scale;
                    var rect = new Rect(point.x - size * 0.5f, point.y - Lift - size - bob, size, size);
                    item.Last = rect;
                    DrawIcon(rect, item.Kind, fade);
                    var hit = new Rect(rect.x - HitPad, rect.y - HitPad, rect.width + HitPad * 2f, rect.height + HitPad * 2f);
                    if (hitRects != null) hitRects.Add(hit);
                    if (GUI.Button(hit, GUIContent.none, _hit))
                        Collect(item, session);
                }
            }

            for (int i = 0; i < _gains.Count; i++)
                DrawGain(_gains[i]);
            GUI.color = previous;
        }

        void Collect(Item item, GameSession session)
        {
            _items.Remove(item);
            if (session == null) return;
            var at = item.Shown ? item.Last.center : new Vector2(GameRunner.ReferenceWidth * 0.5f, GameRunner.ReferenceHeight * 0.5f);
            if (item.Kind == PickupKind.Money)
            {
                BigInteger amount;
                if (session.CollectPickupMoney(out amount))
                    _gains.Add(new Gain { Id = item.Id, At = at, Text = "+" + MoneyText.Format(amount), Color = MoneyColor });
                return;
            }

            double gain;
            string name;
            if (!session.CollectPickupStat(item.Id, item.Kind, out gain, out name)) return;
            string label = item.Kind == PickupKind.Offense ? "OFF" : item.Kind == PickupKind.Defense ? "DEF" : "STA";
            _gains.Add(new Gain
            {
                Id = item.Id,
                At = at,
                Text = "+" + gain.ToString("0.#", CultureInfo.InvariantCulture) + " " + label,
                Color = StatColor
            });
            var stat = item.Kind == PickupKind.Offense ? GrowthStat.Offense : item.Kind == PickupKind.Defense ? GrowthStat.Defense : GrowthStat.Stamina;
            int shown = Mathf.Max(1, Mathf.RoundToInt((float)gain));
            StatCollected?.Invoke(name, stat, shown);
        }

        void DrawIcon(Rect rect, PickupKind kind, float alpha)
        {
            if (kind == PickupKind.Money && _coin != null)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.25f * alpha);
                GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 5f, rect.width, rect.height), _disc, ScaleMode.StretchToFill, true);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(rect, _coin, ScaleMode.StretchToFill, true);
                return;
            }

            // 能力は、クリームの丸に焦げ茶の縁、中に成長ログと同じアイコン。
            GUI.color = new Color(0f, 0f, 0f, 0.25f * alpha);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 5f, rect.width, rect.height), _disc, ScaleMode.StretchToFill, true);
            GUI.color = new Color(Edge.r, Edge.g, Edge.b, alpha);
            GUI.DrawTexture(rect, _disc, ScaleMode.StretchToFill, true);
            float border = rect.width * 0.06f;
            GUI.color = new Color(Cream.r, Cream.g, Cream.b, alpha);
            GUI.DrawTexture(new Rect(rect.x + border, rect.y + border, rect.width - border * 2f, rect.height - border * 2f), _disc, ScaleMode.StretchToFill, true);
            var icon = kind == PickupKind.Offense ? _offense : kind == PickupKind.Defense ? _defense : _stamina;
            if (icon != null)
            {
                float inner = rect.width * 0.62f;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(rect.center.x - inner * 0.5f, rect.center.y - inner * 0.5f, inner, inner), icon, ScaleMode.ScaleToFit, true);
            }
        }

        void DrawGain(Gain gain)
        {
            float t = Mathf.Clamp01(gain.Age / GainSeconds);
            float rise = GainRise * (1f - (1f - t) * (1f - t));
            float alpha = t < 0.55f ? 1f : Mathf.Clamp01(1f - (t - 0.55f) / 0.45f);
            float pop = gain.Age < 0.15f ? Mathf.Lerp(1.35f, 1f, gain.Age / 0.15f) : 1f;
            var content = new GUIContent(gain.Text);
            _gainStyle.fontSize = Mathf.RoundToInt(36f * pop);
            var size = _gainStyle.CalcSize(content);
            var rect = new Rect(gain.At.x - size.x * 0.5f, gain.At.y - size.y * 0.5f - rise, size.x, size.y);
            // 濃い茶色のふちどり、その上に色の文字。
            _gainStyle.normal.textColor = new Color(Edge.r, Edge.g, Edge.b, alpha);
            for (int dx = -3; dx <= 3; dx += 3)
            {
                for (int dy = -3; dy <= 3; dy += 3)
                {
                    if (dx == 0 && dy == 0) continue;
                    GUI.Label(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), content, _gainStyle);
                }
            }

            _gainStyle.normal.textColor = new Color(gain.Color.r, gain.Color.g, gain.Color.b, alpha);
            GUI.Label(rect, content, _gainStyle);
            _gainStyle.fontSize = 36;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        void EnsureAssets(Font font)
        {
            if (_coin == null) _coin = Resources.Load<Texture2D>("MainScreen/icon_pickup_money");
            if (_offense == null) _offense = Resources.Load<Texture2D>("MainScreen/icon_off2");
            if (_defense == null) _defense = Resources.Load<Texture2D>("MainScreen/icon_def2");
            if (_stamina == null) _stamina = Resources.Load<Texture2D>("MainScreen/icon_sta2");
            if (_disc == null) _disc = MakeDisc(128);
            if (_gainStyle != null && _font == font) return;
            _font = font;
            _gainStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0)
            };
            _hit = new GUIStyle(GUIStyle.none);
        }

        static Texture2D MakeDisc(int size)
        {
            var pixels = new Color32[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float cover = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(cover * 255f));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
    }
}
