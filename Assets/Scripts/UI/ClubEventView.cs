using System;
using System.Collections.Generic;
using System.Globalization;
using BigInteger = System.Numerics.BigInteger;
using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;
using Nc = SoccerClub.UI.ClubDialogView;

namespace SoccerClub.UI
{
    /// <summary>ウィンターブレイク、年始、移籍、補強のイベントと、季節のパーティの招待画面（Figma の party_invite）。</summary>
    public sealed class ClubEventView
    {
        static readonly Rect Panel = new Rect(460f, 180f, 1000f, 720f);

        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _button;
        Font _font;

        public bool Draw(ClubStory story, GameSession session, Font font)
        {
            if (story == null) return false;
            EnsureStyles(font);
            if (story.Kind == ClubStoryKind.Party)
                return DrawPartyInvite(story, session);

            GUI.color = new Color(0.04f, 0.06f, 0.05f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = new Color(0.10f, 0.16f, 0.14f, 0.98f);
            GUI.DrawTexture(Panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float x = Panel.x + 64f;
            float width = Panel.width - 128f;
            float y = Panel.y + 40f;
            GUI.Label(new Rect(x, y, width, 52f), story.Title ?? "", _title);
            y += 72f;
            float bodyHeight = story.Budget ? 280f : 460f;
            GUI.Label(new Rect(x, y, width, bodyHeight), story.Body ?? "", _body);

            if (story.Budget && session != null && session.PlayerClub != null)
            {
                var club = session.PlayerClub;
                int percent = story.BudgetPercent;
                if (percent < 0) percent = 0;
                if (percent > 100) percent = 100;
                var amount = club.Coins * percent / 100;
                float moneyY = Panel.y + 360f;
                GUI.Label(new Rect(x, moneyY, width, 40f), "資産  " + Money(club.Coins), _body);
                GUI.Label(new Rect(x, moneyY + 48f, width, 40f), "割合  " + percent + "%    強化費へ  " + Money(amount), _body);
                GUI.Label(new Rect(x, moneyY + 96f, width, 40f), "いまの強化費  " + Money(club.Reinforcement), _body);
                if (GUI.Button(new Rect(x, moneyY + 148f, 180f, 56f), "-10%", _button))
                    story.BudgetPercent = Math.Max(0, percent - 10);
                if (GUI.Button(new Rect(x + 200f, moneyY + 148f, 180f, 56f), "+10%", _button))
                    story.BudgetPercent = Math.Min(100, percent + 10);
            }

            string label = story.Budget ? "この予算で進める" : (story.FinalStep ? "確認" : "次へ");
            return GUI.Button(new Rect(x, Panel.yMax - 110f, width, 64f), label, _button);
        }

        // ---- パーティの招待（Figma の party_invite。版 1.47）
        // メイン画面に黒 35% を敷き、クラブハウスと同じ枠・ステッチ・看板。左 192,265 に選んでいる選手の詳細と
        // 仲の良い選手、右 776,329 に選手のカード（144×160 を 156×168 間隔で横 6 枚）、下に費用とボタン。

        static readonly Rect PtDialog = new Rect(160f, 158f, 1600f, 800f);
        static readonly Rect PtStitch = new Rect(176f, 174f, 1568f, 768f);
        static readonly Rect PtPlank = new Rect(700f, 136f, 520f, 76f);
        static readonly Rect PtDetail = new Rect(192f, 265f, 568f, 660f);
        static readonly Rect PtGrid = new Rect(776f, 329f, 924f, 512f);
        static readonly Rect PtTrack = new Rect(1708f, 329f, 10f, 504f);
        const int PtColumns = 6;
        const float PtCardW = 144f;
        const float PtCardH = 160f;
        const float PtStepX = 156f;
        const float PtStepY = 168f;
        const float PtFaceH = 112f;
        const int PtFriendRows = 4;
        const int PtPortraitPixels = 240;
        // character3 の切り抜き（クラブハウスの顔と同じ）。160 角の箱に対して、シートを左 -41 から 244/2048 倍で置く。
        const float PtSheetScale = 244f / 2048f;

        static readonly Color PtCream = Nc.Hex(0xFFF8E7);
        static readonly Color PtDarkBrown = Nc.Hex(0x4A2E14);
        static readonly Color PtStitchColor = Nc.Hex(0xD9C08E);
        static readonly Color PtPanel = Nc.Hex(0xF3E6C8);
        static readonly Color PtPanelLine = Nc.Hex(0xC9A66B);
        static readonly Color PtRowFill = Nc.Hex(0xFBF3DF);
        static readonly Color PtRowLine = Nc.Hex(0xE2CFA5);
        static readonly Color PtBrown = Nc.Hex(0x8B5A2B);
        static readonly Color PtHeadBrown = Nc.Hex(0x6B4423);
        static readonly Color PtText = Nc.Hex(0x3B2A1A);
        static readonly Color PtYellow = Nc.Hex(0xF5C542);
        static readonly Color PtCard = Nc.Hex(0xFFFDF6);
        static readonly Color PtTabOff = Nc.Hex(0xF6EBD0);
        static readonly Color PtMaleFill = Nc.Hex(0xD6E8FA);
        static readonly Color PtMaleText = Nc.Hex(0x2F5E9E);
        static readonly Color PtFemaleFill = Nc.Hex(0xFADDE6);
        static readonly Color PtFemaleText = Nc.Hex(0xB03A68);
        static readonly Color PtHeart = Nc.Hex(0xE05A8A);
        static readonly Color PtStar = Nc.Hex(0xE0A800);
        static readonly Color PtMuted = Nc.Hex(0xB9A07A);
        static readonly Color PtDanger = Nc.Hex(0x9A3F2A);

        GUIStyle _row;
        GUIStyle _rowOn;
        GUIStyle _small;
        GUIStyle _ptText;

        readonly AvatarLibrary _ptAvatars = new AvatarLibrary();
        readonly Dictionary<string, Texture2D> _ptPortraits = new Dictionary<string, Texture2D>();
        readonly List<Player> _ptSquad = new List<Player>();
        readonly List<PtFriend> _ptFriends = new List<PtFriend>();
        ClubStory _ptStory;
        string _ptSelectedId;
        string _ptFriendsKey;
        float _ptScroll;
        bool _ptPress;
        bool _ptDragging;
        Vector2 _ptPressPos;
        float _ptPressScroll;

        struct PtFriend
        {
            public Player Player;
            public RelationStage Stage;
            public int Affinity;
        }

        bool DrawPartyInvite(ClubStory story, GameSession session)
        {
            var club = session != null ? session.PlayerClub : null;
            var party = session != null ? session.OpenParty : null;
            if (_ptStory != story) ResetParty(story);

            // メイン画面を暗くして、枠・ステッチ・看板。
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Nc.NcRound(new Rect(PtDialog.x, PtDialog.y + 10f, PtDialog.width, PtDialog.height), Nc.NcShade(0.3f), 36f);
            Nc.NcBox(PtDialog, PtCream, PtDarkBrown, 6f, 36f);
            Nc.NcDashedRect(PtStitch, PtStitchColor, 3f, 24f, 14f, 10f);
            Nc.NcRound(new Rect(PtPlank.x, PtPlank.y + 5f, PtPlank.width, PtPlank.height), Nc.NcShade(0.4f), 38f);
            GUI.DrawTexture(PtPlank, Nc.NcPlankTexture(), ScaleMode.StretchToFill, true, 0f, Color.white, 0f, 38f);
            Nc.NcOutline(PtPlank, PtDarkBrown, 5f, 38f);
            Text(PtPlank, party != null ? party.Title : (story.Title ?? "パーティー"), 40, true, PtCream, TextAnchor.MiddleCenter);

            if (club == null || party == null)
                return PtButton(new Rect(1512f, 861f, 216f, 64f), "閉じる", true, true);

            CollectSquad(club);
            if (_ptSelectedId == null || FindSquad(_ptSelectedId) == null)
                _ptSelectedId = story.Invited.Count > 0 && FindSquad(story.Invited[0]) != null ? story.Invited[0]
                    : (_ptSquad.Count > 0 ? _ptSquad[0].Id : null);
            var selected = FindSquad(_ptSelectedId);
            BuildFriends(session, selected);

            // 右上：案内と招待の人数。
            Text(new Rect(776f, 265f, 600f, 48f), "招待する選手を選んでください", 24, true, PtHeadBrown, TextAnchor.MiddleLeft);
            DrawInviteCount(story.Invited.Count, party.Slots);

            DrawGrid(session, story, club, selected);
            DrawPartyDetail(session, story, club, party, selected);

            // 下の段：費用と資金、ボタン。
            var cost = session.PartyCost(story);
            bool afford = session.CanAffordParty(story);
            Text(new Rect(776f, 857f, 60f, 44f), "費用", 20, true, PtHeadBrown, TextAnchor.MiddleLeft);
            FitText(new Rect(832f, 853f, 290f, 48f), MoneyText.Format(cost), 30, true, PtDarkBrown, TextAnchor.MiddleLeft);
            if (afford)
                FitText(new Rect(776f, 897f, 346f, 32f), "資金 " + MoneyText.Format(club.Coins), 20, false, PtHeadBrown, TextAnchor.MiddleLeft);
            else
                FitText(new Rect(776f, 897f, 346f, 32f), "資金が足りません（資金 " + MoneyText.Format(club.Coins) + "）", 20, true, PtDanger, TextAnchor.MiddleLeft);

            if (PtButton(new Rect(1128f, 861f, 180f, 64f), "おまかせ", true, false))
                session.RecommendPartyGuests(story);
            if (PtButton(new Rect(1320f, 861f, 180f, 64f), "開かない", true, false))
            {
                story.Invited.Clear();
                return true;
            }

            bool ready = afford && story.Invited.Count > 0;
            return PtButton(new Rect(1512f, 861f, 216f, 64f), "決定", ready, true) && ready;
        }

        void ResetParty(ClubStory story)
        {
            _ptStory = story;
            _ptSelectedId = null;
            _ptFriendsKey = null;
            _ptScroll = 0f;
            _ptPress = false;
            _ptDragging = false;
            ReleasePortraits();
        }

        void ReleasePortraits()
        {
            foreach (var pair in _ptPortraits)
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
            _ptPortraits.Clear();
        }

        /// <summary>並びはポジションの順（GK・DF・MF・FW、同じなら細かいポジション、名前）。</summary>
        void CollectSquad(Club club)
        {
            _ptSquad.Clear();
            for (int i = 0; i < club.Squad.Count; i++)
            {
                var p = club.Squad[i];
                if (p != null && !string.IsNullOrEmpty(p.Id)) _ptSquad.Add(p);
            }

            _ptSquad.Sort((a, b) =>
            {
                var pa = a.BestPosition();
                var pb = b.BestPosition();
                int c = ((int)PositionUtility.GroupOf(pa)).CompareTo((int)PositionUtility.GroupOf(pb));
                if (c != 0) return c;
                c = ((int)pa).CompareTo((int)pb);
                return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
            });
        }

        Player FindSquad(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _ptSquad.Count; i++)
                if (_ptSquad[i].Id == id) return _ptSquad[i];
            return null;
        }

        /// <summary>選んでいる選手と関係のある所属選手（夫婦・恋人・親友・友達の順、同じ段階は親密度の高い順）。</summary>
        void BuildFriends(GameSession session, Player selected)
        {
            string key = (selected != null ? selected.Id : "") + "|" + (session != null ? session.Time.CurrentDate.Ticks : 0L);
            if (_ptFriendsKey == key) return;
            _ptFriendsKey = key;
            _ptFriends.Clear();
            if (selected == null || session == null || session.Relations == null) return;
            var list = session.Relations.For(selected.Id);
            for (int i = 0; list != null && i < list.Count; i++)
            {
                var rel = list[i];
                if (StageOrder(rel.Stage) >= 4) continue;
                var other = FindSquad(rel.Other(selected.Id));
                if (other == null) continue;
                _ptFriends.Add(new PtFriend { Player = other, Stage = rel.Stage, Affinity = rel.Affinity });
            }

            _ptFriends.Sort((a, b) =>
            {
                int c = StageOrder(a.Stage).CompareTo(StageOrder(b.Stage));
                return c != 0 ? c : b.Affinity.CompareTo(a.Affinity);
            });
        }

        static int StageOrder(RelationStage stage)
        {
            switch (stage)
            {
                case RelationStage.Married: return 0;
                case RelationStage.Lover: return 1;
                case RelationStage.BestFriend: return 2;
                case RelationStage.Friend: return 3;
                default: return 4;
            }
        }

        static bool IsPartner(RelationStage stage)
        {
            return stage == RelationStage.Married || stage == RelationStage.Lover;
        }

        /// <summary>選んでいる選手との関係（カードの印に使う）。無ければ None。</summary>
        RelationStage StageWithSelected(Player p)
        {
            for (int i = 0; i < _ptFriends.Count; i++)
                if (_ptFriends[i].Player == p) return _ptFriends[i].Stage;
            return RelationStage.None;
        }

        void DrawInviteCount(int count, int slots)
        {
            const float Dot = 28f;
            const float Gap = 8f;
            float dotsWidth = slots * Dot + (slots - 1) * Gap;
            float right = 1728f;
            float dotsX = right - dotsWidth;
            for (int i = 0; i < slots; i++)
            {
                var r = new Rect(dotsX + i * (Dot + Gap), 275f, Dot, Dot);
                bool on = i < count;
                Nc.NcBox(r, on ? PtYellow : PtCard, on ? PtDarkBrown : PtPanelLine, 2f, Dot * 0.5f);
            }

            Text(new Rect(dotsX - 220f, 265f, 208f, 48f),
                "招待 " + count.ToString(CultureInfo.InvariantCulture) + " / " + slots.ToString(CultureInfo.InvariantCulture) + "人",
                24, true, PtDarkBrown, TextAnchor.MiddleRight);
        }

        // ---- 右：選手のカード

        void DrawGrid(GameSession session, ClubStory story, Club club, Player selected)
        {
            int rows = Mathf.CeilToInt(_ptSquad.Count / (float)PtColumns);
            float content = Mathf.Max(0f, rows * PtStepY - (PtStepY - PtCardH));
            float maxScroll = Mathf.Max(0f, content - PtGrid.height);
            _ptScroll = Mathf.Clamp(_ptScroll, 0f, maxScroll);
            HandleGridInput(session, story, maxScroll);

            GUI.BeginGroup(PtGrid);
            for (int i = 0; i < _ptSquad.Count; i++)
            {
                float x = (i % PtColumns) * PtStepX;
                float y = (i / PtColumns) * PtStepY - _ptScroll;
                if (y + PtCardH < 0f || y > PtGrid.height) continue;
                var p = _ptSquad[i];
                DrawCard(new Rect(x, y, PtCardW, PtCardH), p, club, story.Invited.Contains(p.Id), p == selected, StageWithSelected(p));
            }

            GUI.EndGroup();

            if (maxScroll > 0.5f)
            {
                Nc.NcRound(PtTrack, PtRowLine, 5f);
                float thumbH = Mathf.Clamp(PtTrack.height * PtGrid.height / content, 40f, PtTrack.height);
                float span = Mathf.Max(1f, PtTrack.height - thumbH);
                Nc.NcRound(new Rect(PtTrack.x, PtTrack.y + span * (_ptScroll / maxScroll), PtTrack.width, thumbH), PtBrown, 5f);
            }
        }

        /// <summary>
        /// カードのタップで左の詳細に出す。選んでいるカードをもう一度タップすると、招待を入れ替える。
        /// 上下のドラッグとホイールでスクロール（動いたらタップにしない）。
        /// </summary>
        void HandleGridInput(GameSession session, ClubStory story, float maxScroll)
        {
            var e = Event.current;
            if (e == null) return;
            var area = new Rect(PtGrid.x, PtGrid.y, PtTrack.xMax - PtGrid.x, PtGrid.height);
            bool over = area.Contains(e.mousePosition);
            if (e.type == EventType.ScrollWheel && over)
            {
                _ptScroll = Mathf.Clamp(_ptScroll + e.delta.y * 24f, 0f, maxScroll);
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && over)
            {
                _ptPress = true;
                _ptDragging = false;
                _ptPressPos = e.mousePosition;
                _ptPressScroll = _ptScroll;
                e.Use();
            }
            else if (_ptPress && e.type == EventType.MouseDrag)
            {
                float dy = e.mousePosition.y - _ptPressPos.y;
                if (!_ptDragging && Mathf.Abs(dy) > 8f) _ptDragging = true;
                if (_ptDragging) _ptScroll = Mathf.Clamp(_ptPressScroll - dy, 0f, maxScroll);
                e.Use();
            }
            else if (_ptPress && e.type == EventType.MouseUp)
            {
                _ptPress = false;
                if (!_ptDragging)
                {
                    var p = CardAt(e.mousePosition);
                    if (p != null)
                    {
                        if (p.Id == _ptSelectedId)
                            session.TogglePartyGuest(story, p.Id);
                        else
                            _ptSelectedId = p.Id;
                    }
                }

                _ptDragging = false;
                e.Use();
            }
        }

        Player CardAt(Vector2 mouse)
        {
            float lx = mouse.x - PtGrid.x;
            float ly = mouse.y - PtGrid.y + _ptScroll;
            if (lx < 0f || ly < 0f || mouse.y > PtGrid.yMax) return null;
            int col = Mathf.FloorToInt(lx / PtStepX);
            int row = Mathf.FloorToInt(ly / PtStepY);
            if (col >= PtColumns || lx - col * PtStepX > PtCardW || ly - row * PtStepY > PtCardH) return null;
            int index = row * PtColumns + col;
            return index >= 0 && index < _ptSquad.Count ? _ptSquad[index] : null;
        }

        void DrawCard(Rect r, Player p, Club club, bool invited, bool selected, RelationStage stage)
        {
            if (selected)
                Nc.NcRound(new Rect(r.x, r.y + 4f, r.width, r.height), Nc.NcShade(0.4f), 16f);
            Nc.NcRound(r, PtCard, 16f);

            // 顔（上 112）。地はクラブハウスの顔と同じグラデーション。
            var face = new Rect(r.x, r.y, r.width, PtFaceH);
            GUI.DrawTexture(face, Nc.NcPortraitBackground(), ScaleMode.StretchToFill, true, 0f, Color.white, Vector4.zero, new Vector4(16f, 16f, 0f, 0f));
            DrawPortrait(face, (PtFaceH - 0f), new Vector2(16f, 0f), p, club);

            // 名前の帯（下 48）。招待中は黄色。
            var bar = new Rect(r.x, r.y + PtFaceH, r.width, r.height - PtFaceH);
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, invited ? PtYellow : PtCard, Vector4.zero, new Vector4(0f, 0f, 16f, 16f));
            FitText(new Rect(bar.x + 8f, bar.y, bar.width - 16f, bar.height), p.Name ?? "", 20, true, PtText, TextAnchor.MiddleCenter);

            // 枠。選んでいるカードは濃い茶色の 4。
            if (selected) Nc.NcOutline(r, PtDarkBrown, 4f, 16f);
            else Nc.NcOutline(r, PtRowLine, 2f, 16f);

            // ポジションの札（左上）。
            var group = PositionUtility.GroupOf(p.BestPosition());
            var pos = new Rect(r.x + 6f, r.y + 6f, 44f, 26f);
            Nc.NcRound(pos, PositionUtility.GroupColor(group), 8f);
            Text(pos, group.ToString(), 16, true, group == PositionGroup.MF ? PtText : Color.white, TextAnchor.MiddleCenter);

            // 選んでいる選手との関係（右上）。♥ 恋人・夫婦、★ 友達・親友。
            if (stage != RelationStage.None)
            {
                var mark = new Rect(r.x + 104f, r.y + 4f, 34f, 34f);
                Nc.NcBox(mark, IsPartner(stage) ? PtHeart : PtStar, Color.white, 2f, 17f);
                Text(mark, IsPartner(stage) ? "♥" : "★", 20, true, Color.white, TextAnchor.MiddleCenter);
            }

            // 招待中（顔の右下に ✓）。
            if (invited)
            {
                var check = new Rect(r.x + 104f, r.y + 74f, 34f, 34f);
                Nc.NcBox(check, PtYellow, PtDarkBrown, 2f, 17f);
                DrawCheck(check, PtDarkBrown);
            }
        }

        /// <summary>✓ を 2 本の棒で描く（フォントに ✓ が無くても出るように）。下の角を軸に、短い棒は左上、長い棒は右上へ。</summary>
        static void DrawCheck(Rect box, Color color)
        {
            var matrix = GUI.matrix;
            var pivot = new Vector2(box.x + box.width * 0.42f, box.y + box.height * 0.72f);
            GUIUtility.RotateAroundPivot(-45f, pivot);
            GUI.DrawTexture(new Rect(pivot.x - 2f, pivot.y - 11f, 4f, 13f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 2f);
            GUI.matrix = matrix;
            GUIUtility.RotateAroundPivot(45f, pivot);
            GUI.DrawTexture(new Rect(pivot.x - 2f, pivot.y - 20f, 4f, 22f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 2f);
            GUI.matrix = matrix;
        }

        /// <summary>
        /// character3 の顔の切り抜き。box の中に、160 角の箱と同じ比率でシートを置く（size が箱の高さ）。
        /// offset は箱の左上から、160 角の箱の左上までのずれ（カードは横 144 に 112 角を中央寄せ）。
        /// </summary>
        void DrawPortrait(Rect box, float size, Vector2 offset, Player p, Club club)
        {
            var texture = PortraitOf(p, club);
            if (texture == null) return;
            float k = size / 160f;
            var sheet = new Rect(offset.x + (-41f + 212f * PtSheetScale) * k, offset.y + 46f * PtSheetScale * k,
                1622f * PtSheetScale * k, 1998f * PtSheetScale * k);
            GUI.BeginGroup(box);
            GUI.DrawTexture(sheet, texture, ScaleMode.StretchToFill, true);
            GUI.EndGroup();
        }

        Texture2D PortraitOf(Player p, Club club)
        {
            if (p == null || club == null) return null;
            string key = p.Id + ":" + club.EmblemIndex.ToString(CultureInfo.InvariantCulture);
            Texture2D texture;
            if (_ptPortraits.TryGetValue(key, out texture)) return texture;
            if (!_ptAvatars.Ready) _ptAvatars.Load();
            if (!_ptAvatars.Ready) return null;
            var hair = PositionUtility.GroupColor(PositionUtility.GroupOf(p.BestPosition()));
            texture = _ptAvatars.CreateStand(p.Id, AvatarLibrary.Kit(club.EmblemIndex), hair, PtPortraitPixels, gender: p.Gender);
            _ptPortraits[key] = texture;
            return texture;
        }

        // ---- 左：選んでいる選手の詳細と、仲の良い選手

        void DrawPartyDetail(GameSession session, ClubStory story, Club club, PartyCatalog.Party party, Player selected)
        {
            Nc.NcBox(PtDetail, PtPanel, PtPanelLine, 3f, 24f);
            if (selected == null) return;
            float x = PtDetail.x + 24f;
            float y = PtDetail.y + 24f;

            // 顔（180 角、角丸 20）。はみ出した角は詳細枠の色で消す。
            var faceBox = new Rect(x, y, 180f, 180f);
            GUI.DrawTexture(faceBox, Nc.NcPortraitBackground(), ScaleMode.StretchToFill, true, 0f, Color.white, 0f, 20f);
            DrawPortrait(faceBox, 180f, Vector2.zero, selected, club);
            Nc.NcOutline(new Rect(faceBox.x - 20f, faceBox.y - 20f, faceBox.width + 40f, faceBox.height + 40f), PtPanel, 20f, 40f);
            Nc.NcOutline(faceBox, PtPanelLine, 3f, 20f);

            // 右の 4 行。
            float rx = x + 196f;
            const float RowW = 324f;
            var group = PositionUtility.GroupOf(selected.BestPosition());
            bool female = selected.Gender == Gender.Female;
            InfoRow(new Rect(rx, y, RowW, 40f), selected.BestPosition().ToString(), PositionUtility.GroupColor(group),
                group == PositionGroup.MF ? PtText : Color.white, selected.Name, false);
            InfoRow(new Rect(rx, y + 46f, RowW, 40f), female ? "Female" : "Male", female ? PtFemaleFill : PtMaleFill,
                female ? PtFemaleText : PtMaleText, selected.Age.ToString(CultureInfo.InvariantCulture) + "歳", true);
            InfoRow(new Rect(rx, y + 92f, RowW, 40f), "年俸", PtBrown, Color.white, MoneyText.Format(selected.AnnualSalary), true);
            InfoRow(new Rect(rx, y + 138f, RowW, 40f), "費用", PtBrown, Color.white, MoneyText.Format(session.PartyGuestCost(story, selected)), true);

            // 仲の良い選手。
            var head = new Rect(x, PtDetail.y + 224f, 520f, 44f);
            Nc.NcRound(head, PtHeadBrown, 12f);
            Text(new Rect(head.x + 16f, head.y, 200f, head.height), "仲の良い選手", 24, true, PtCream, TextAnchor.MiddleLeft);
            Text(new Rect(head.x + 200f, head.y, 304f, head.height), "♥ 恋人・夫婦　★ 友達・親友", 18, true, PtYellow, TextAnchor.MiddleRight);

            float fy = PtDetail.y + 280f;
            if (_ptFriends.Count == 0)
                Text(new Rect(x, fy, 520f, 56f), "仲の良い選手はまだいません", 22, true, PtMuted, TextAnchor.MiddleCenter);
            int shown = Mathf.Min(PtFriendRows, _ptFriends.Count);
            for (int i = 0; i < shown; i++)
                DrawFriendRow(new Rect(x, fy + i * 62f, 520f, 56f), _ptFriends[i], story, club);

            string hint = _ptFriends.Count > PtFriendRows
                ? "ほかに" + (_ptFriends.Count - PtFriendRows).ToString(CultureInfo.InvariantCulture) + "人。♥・★の相手と一緒に招待すると仲が深まります"
                : "♥・★の相手と一緒に招待すると、仲がもっと深まります";
            FitText(new Rect(x, PtDetail.y + 532f, 520f, 36f), hint, 19, true, PtHeadBrown, TextAnchor.MiddleCenter);

            // 招待の入れ替え。枠がいっぱいか、足すと資金が足りないときは押せない。
            bool invited = story.Invited.Contains(selected.Id);
            bool canAdd = story.Invited.Count < party.Slots
                          && session.PartyCost(story) + session.PartyGuestCost(story, selected) <= club.Coins;
            var button = new Rect(x, PtDetail.y + 584f, 520f, 56f);
            if (invited)
            {
                if (PtButton(button, "招待から外す", true, false))
                    session.TogglePartyGuest(story, selected.Id);
            }
            else if (PtButton(button, "招待する", canAdd, true) && canAdd)
            {
                session.TogglePartyGuest(story, selected.Id);
            }
        }

        void InfoRow(Rect row, string label, Color fill, Color textColor, string value, bool right)
        {
            Nc.NcBox(row, PtRowFill, PtRowLine, 2f, 12f);
            var band = new Rect(row.x, row.y, 80f, row.height);
            Nc.NcRound(band, fill, new Vector4(12f, 0f, 0f, 12f));
            FitText(new Rect(band.x + 4f, band.y, band.width - 8f, band.height), label, 24, true, textColor, TextAnchor.MiddleCenter);
            var valueRect = new Rect(row.x + 88f, row.y, row.width - 96f, row.height);
            FitText(valueRect, value ?? "", 24, false, PtText, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
        }

        /// <summary>仲の良い選手の 1 行。タップすると、その選手を詳細に出す。</summary>
        void DrawFriendRow(Rect row, PtFriend f, ClubStory story, Club club)
        {
            bool invited = story.Invited.Contains(f.Player.Id);
            var fill = invited ? Color.white : PtRowFill;
            Nc.NcBox(row, fill, PtRowLine, 2f, 12f);
            var band = new Rect(row.x, row.y, 96f, row.height);
            bool partner = IsPartner(f.Stage);
            Nc.NcRound(band, partner ? PtHeart : PtStar, new Vector4(12f, 0f, 0f, 12f));
            FitText(band, (partner ? "♥" : "★") + StageText(f.Stage), 22, true, Color.white, TextAnchor.MiddleCenter);

            // 顔（48 の丸）。丸の外は行の地の色で消す。
            var face = new Rect(row.x + 104f, row.y + 4f, 48f, 48f);
            GUI.DrawTexture(face, Nc.NcPortraitBackground(), ScaleMode.StretchToFill, true, 0f, Color.white, 0f, 24f);
            DrawPortrait(face, 48f, Vector2.zero, f.Player, club);
            Nc.NcOutline(new Rect(face.x - 8f, face.y - 8f, face.width + 16f, face.height + 16f), fill, 8f, 32f);

            FitText(new Rect(row.x + 162f, row.y, 150f, row.height), f.Player.Name ?? "", 22, true, PtText, TextAnchor.MiddleLeft);
            Text(new Rect(row.x + 316f, row.y, 96f, row.height), "親密度 " + f.Affinity.ToString(CultureInfo.InvariantCulture), 18, false, PtHeadBrown, TextAnchor.MiddleLeft);
            var chip = new Rect(row.x + 416f, row.y + 13f, 92f, 30f);
            if (invited)
            {
                Nc.NcRound(chip, PtYellow, 8f);
                Text(chip, "招待中", 18, true, PtDarkBrown, TextAnchor.MiddleCenter);
            }
            else
            {
                Text(chip, "未招待", 18, true, PtMuted, TextAnchor.MiddleCenter);
            }

            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                _ptSelectedId = f.Player.Id;
        }

        static string StageText(RelationStage stage)
        {
            switch (stage)
            {
                case RelationStage.Married: return "夫婦";
                case RelationStage.Lover: return "恋人";
                case RelationStage.BestFriend: return "親友";
                default: return "友達";
            }
        }

        /// <summary>
        /// 丸いボタン（高さ 64 なら角丸 32、線 3、下に影）。primary は黄色（#F5C542・線 #4A2E14）、
        /// それ以外はクリーム（#F6EBD0・線 #C9A66B）。押せないときは薄くする。
        /// </summary>
        bool PtButton(Rect rect, string label, bool enabled, bool primary)
        {
            var previous = GUI.color;
            if (!enabled) GUI.color = new Color(1f, 1f, 1f, 0.45f);
            float radius = rect.height * 0.5f;
            Nc.NcRound(new Rect(rect.x, rect.y + 4f, rect.width, rect.height), Nc.NcShade(0.35f), radius);
            Nc.NcBox(rect, primary ? PtYellow : PtTabOff, primary ? PtDarkBrown : PtPanelLine, 3f, radius);
            Text(rect, label, 26, true, primary ? PtDarkBrown : PtHeadBrown, TextAnchor.MiddleCenter);
            GUI.color = previous;
            bool was = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = was;
            return clicked && enabled;
        }

        void Text(Rect rect, string text, int size, bool bold, Color color, TextAnchor anchor)
        {
            _ptText.fontSize = size;
            _ptText.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            _ptText.alignment = anchor;
            _ptText.normal.textColor = color;
            GUI.Label(rect, text, _ptText);
        }

        /// <summary>幅に入らないときだけ小さくする。</summary>
        void FitText(Rect rect, string text, int size, bool bold, Color color, TextAnchor anchor)
        {
            _ptText.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            _ptText.alignment = anchor;
            _ptText.normal.textColor = color;
            Nc.FitNcLabel(rect, text, _ptText, size);
        }

        static string Money(BigInteger amount)
        {
            return MoneyText.Format(amount);
        }

        void EnsureStyles(Font font)
        {
            if (_title != null && _font == font) return;
            _font = font;
            _title = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 26,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _button = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };
            _small = new GUIStyle(_body) { fontSize = 22 };
            _row = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 8, 4, 4)
            };
            _rowOn = new GUIStyle(_row);
            _rowOn.normal.textColor = new Color(1f, 0.85f, 0.35f);
            _rowOn.hover.textColor = new Color(1f, 0.85f, 0.35f);
            _rowOn.fontStyle = FontStyle.Bold;
            _ptText = new GUIStyle(GUI.skin.label)
            {
                font = font,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                richText = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
        }
    }
}
