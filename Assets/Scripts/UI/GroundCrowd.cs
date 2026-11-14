using System.Collections.Generic;
using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 所属の全員を area_player / area_staff の中で歩かせる。
    /// アバターは生成時に小さいテクスチャへ焼き、毎フレームは描画だけ行う。
    /// </summary>
    public sealed class GroundCrowd
    {
        const float CharacterScale = 0.5f;
        const float WalkFrameSeconds = 0.1f;
        const float ArriveDistance = 6f;
        const float EdgeMargin = 14f;
        const float DepartDelayMin = 0.15f;
        const float DepartDelaySpan = 4.5f;
        const float FadeOutSeconds = 1f;
        const float FadeInSeconds = 0.5f;
        const float LongPressSeconds = 0.45f;
        const float PopupSeconds = 1.2f;
        const float ClickSlop = 16f;

        // main2 の area。隣の枠とは 12px 重ね、ゲートが両方に入るようにする。
        // 選手: 383,540 1505×254 / 67,617 316×177 / 827,469 369×71
        // スタッフ: 31,865 1857×183 / 31,821 1295×44
        static readonly Rect[] PlayerDesign =
        {
            new Rect(371f, 528f, 1517f, 266f),
            new Rect(67f, 617f, 328f, 177f),
            new Rect(827f, 469f, 369f, 83f)
        };
        static readonly Rect[] StaffDesign =
        {
            new Rect(31f, 853f, 1857f, 195f),
            new Rect(31f, 821f, 1295f, 56f)
        };
        static readonly Vector2 PlayerLeftGateDesign = new Vector2(383f, 700f);
        static readonly Vector2 PlayerTopGateDesign = new Vector2(1000f, 540f);
        static readonly Vector2 StaffGateDesign = new Vector2(640f, 865f);
        const float BodyHeight = 168f * CharacterScale;

        static Rect ZoneRect(Zone zone, int index)
        {
            var source = zone == Zone.Player ? PlayerDesign : StaffDesign;
            return ClipFeet(source[index]);
        }

        static int ZoneCount(Zone zone)
        {
            return zone == Zone.Player ? PlayerDesign.Length : StaffDesign.Length;
        }

        static int RectIndex(Zone zone, Vector2 point)
        {
            int count = ZoneCount(zone);
            for (int i = 0; i < count; i++)
            {
                if (ZoneRect(zone, i).Contains(point)) return i;
            }

            return -1;
        }

        static Vector2 Gate(Zone zone, int a, int b, Vector2 preferred)
        {
            return GateInside(ZoneRect(zone, a), ZoneRect(zone, b), preferred);
        }

        /// <summary>上下が隠れているとき、体が画面内に収まる足元の範囲へ歩行エリアを狭める。</summary>
        static Rect ClipFeet(Rect area)
        {
            float top = GameRunner.VerticalInset + BodyHeight;
            float bottom = GameRunner.ReferenceHeight - GameRunner.VerticalInset;
            if (bottom - top < 24f) return area;
            float yMin = Mathf.Clamp(area.yMin, top, bottom - 16f);
            float yMax = Mathf.Clamp(area.yMax, yMin + 16f, bottom);
            return new Rect(area.xMin, yMin, area.width, Mathf.Max(16f, yMax - yMin));
        }

        static Vector2 GateInside(Rect a, Rect b, Vector2 preferred)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax - xMin < 4f || yMax - yMin < 4f)
            {
                xMin = a.xMin;
                xMax = a.xMax;
                yMin = a.yMin;
                yMax = a.yMax;
            }

            return new Vector2(
                Mathf.Clamp(preferred.x, xMin, xMax),
                Mathf.Clamp(preferred.y, yMin, yMax));
        }

        enum Zone
        {
            Player,
            Staff
        }

        sealed class Walker
        {
            public string Id;
            public Zone Zone;
            public AvatarFrames Frames;
            public Vector2 Feet;
            public Vector2 Target;
            public float Speed;
            public float IdleUntil;
            public float AnimTime;
            public int Facing;
            public bool Walking;
            public bool Squad;
            public bool Leaving;
            public bool Hidden;
            public bool Arriving;
            public bool Direct;
            public bool Held;
            public string FaceId;
            public float Alpha = 1f;
            public float FadeStart = -1f;
            public Vector2 Anchor;
            public Color32 Hair;
            public float PopupAt;
        }

        readonly AvatarLibrary _library = new AvatarLibrary();
        readonly Dictionary<string, Walker> _walkers = new Dictionary<string, Walker>();
        readonly List<Walker> _drawOrder = new List<Walker>();
        readonly List<string> _ids = new List<string>();
        readonly List<Zone> _zones = new List<Zone>();
        readonly List<bool> _squadFlags = new List<bool>();
        readonly List<Color32> _hairColors = new List<Color32>();
        readonly List<string> _remove = new List<string>();
        readonly Dictionary<string, int> _growthDelta = new Dictionary<string, int>();
        readonly List<Walker> _growthRow = new List<Walker>();
        GUIStyle _plusStyle;
        GUIStyle _minusStyle;
        GUIStyle _plusShadow;
        GUIStyle _minusShadow;
        int _signature = int.MinValue;
        float _time;
        Walker _pressed;
        Walker _dragged;
        Vector2 _pressMouse;
        Vector2 _grabOffset;
        float _pressStart;
        float _pressTravel;
        public string SelectedId { get; private set; }
        const float MeetGap = 56f;
        const float MeetSpeed = 1.75f;

        public void ClearSelection()
        {
            SelectedId = null;
        }

        public void Select(string id)
        {
            SelectedId = string.IsNullOrEmpty(id) ? null : id;
        }

        /// <summary>選手紹介の横向き歩き。原画は左向き。</summary>
        public Texture2D WalkSide(string id)
        {
            Walker walker;
            if (string.IsNullOrEmpty(id) || !_walkers.TryGetValue(id, out walker)) return null;
            var frames = walker.Frames;
            if (frames == null || frames.WalkSide == null || frames.WalkSide.Length == 0) return null;
            return frames.WalkSide[0];
        }

        public bool TryFocusPoint(string id, out Vector2 point)
        {
            point = default;
            Walker walker;
            if (string.IsNullOrEmpty(id) || !_walkers.TryGetValue(id, out walker)) return false;
            if (walker.Hidden || walker.Alpha < 0.35f || walker.Frames == null) return false;
            var body = BodyRect(walker);
            point = body.center;
            return true;
        }

        public bool TryHeadPoint(string id, out Vector2 head)
        {
            head = default;
            Walker walker;
            if (string.IsNullOrEmpty(id) || !_walkers.TryGetValue(id, out walker)) return false;
            if (walker.Hidden || walker.Alpha < 0.35f || walker.Frames == null) return false;
            var body = BodyRect(walker);
            head = new Vector2(body.center.x, body.yMin);
            return true;
        }

        /// <summary>1人はその場で、2人は歩み寄って、立ち止まらせる。ほかの待機は止めない。</summary>
        public bool BeginStand(string a, string b)
        {
            Walker left;
            if (!TryWalker(a, out left) || left.Hidden || left.Leaving) return false;
            if (string.IsNullOrEmpty(b))
            {
                left.FaceId = null;
                HoldAt(left, left.Feet);
                return true;
            }

            Walker right;
            if (a == b || !TryWalker(b, out right) || right.Hidden || right.Leaving) return false;
            Vector2 delta = right.Feet - left.Feet;
            if (delta.sqrMagnitude < 4f) delta = Vector2.right * MeetGap;
            Vector2 dir = delta.normalized;
            Vector2 mid = (left.Feet + right.Feet) * 0.5f;
            Vector2 spotA = ClampToZone(left.Zone, mid - dir * (MeetGap * 0.5f));
            Vector2 spotB = ClampToZone(right.Zone, mid + dir * (MeetGap * 0.5f));
            if ((spotA - spotB).sqrMagnitude < 24f * 24f)
            {
                Vector2 center = (spotA + spotB) * 0.5f;
                spotA = ClampToZone(left.Zone, center - Vector2.right * (MeetGap * 0.5f));
                spotB = ClampToZone(right.Zone, center + Vector2.right * (MeetGap * 0.5f));
            }

            left.FaceId = b;
            right.FaceId = a;
            HoldAt(left, spotA);
            HoldAt(right, spotB);
            return true;
        }

        public void ReleaseStand(string a, string b)
        {
            ReleaseHold(a);
            ReleaseHold(b);
        }

        /// <summary>指定した人が目標で立ち止まっている。</summary>
        public bool StandSettled(string a, string b)
        {
            if (!StandStill(a)) return false;
            return string.IsNullOrEmpty(b) || StandStill(b);
        }

        /// <summary>指定した人が退場して、立ち話を続けられない。</summary>
        public bool StandLost(string a, string b)
        {
            if (PersonLost(a)) return true;
            return !string.IsNullOrEmpty(b) && PersonLost(b);
        }

        bool StandStill(string id)
        {
            Walker walker;
            if (!TryWalker(id, out walker)) return false;
            if (walker.Hidden || walker.Leaving) return false;
            return NearTarget(walker);
        }

        bool PersonLost(string id)
        {
            Walker walker;
            if (!TryWalker(id, out walker)) return true;
            return walker.Hidden || walker.Leaving;
        }

        public void CopyVisibleIds(List<string> dest)
        {
            if (dest == null) return;
            dest.Clear();
            foreach (var pair in _walkers)
            {
                var walker = pair.Value;
                if (walker.Hidden || walker.Alpha < 0.35f || walker.Frames == null) continue;
                if (!string.IsNullOrEmpty(walker.Id)) dest.Add(walker.Id);
            }
        }

        public string RandomVisibleId()
        {
            int count = 0;
            string pick = null;
            foreach (var pair in _walkers)
            {
                var walker = pair.Value;
                if (walker.Hidden || walker.Alpha < 0.35f || walker.Frames == null) continue;
                count++;
                if (UnityEngine.Random.Range(0, count) == 0)
                    pick = walker.Id;
            }

            return pick;
        }
        public bool Dragging => _dragged != null;
        float _nextLoadTime;
        int _loadAttempts;

        public void Load()
        {
            _library.Load();
        }

        public void Tick(float deltaTime, Club club, HashSet<string> absentees, bool stageSquadDeparture, bool hideNonSquad)
        {
            if (!_library.Ready && _loadAttempts < 40 && _time >= _nextLoadTime)
            {
                _loadAttempts++;
                _nextLoadTime = _time + 0.5f;
                _library.Load();
                _signature = int.MinValue;
            }

            _time += deltaTime;
            Sync(club);
            if (stageSquadDeparture) PresentSquadForDeparture();
            if (hideNonSquad) HideNonSquad();
            foreach (var pair in _walkers)
            {
                var walker = pair.Value;
                bool absent = absentees != null && absentees.Contains(walker.Id);
                UpdatePresence(walker, absent);
                Step(walker, deltaTime);
                if (!walker.Leaving && walker != _dragged)
                    KeepInZone(walker);
            }
        }

        public void SetGrowthDeltas(Dictionary<string, int> deltas, float daySeconds)
        {
            _growthDelta.Clear();
            if (deltas == null) return;
            float latest = Mathf.Max(0.15f, daySeconds - PopupSeconds);
            foreach (var pair in deltas)
            {
                Walker walker;
                if (pair.Value == 0 || !_walkers.TryGetValue(pair.Key, out walker) || !walker.Squad)
                    continue;
                _growthDelta[pair.Key] = pair.Value;
                walker.PopupAt = Random.Range(0f, latest);
            }
        }

        /// <summary>この選手の増減ポップが出る時刻（その日の経過秒）。グラウンドにいなければ false。</summary>
        public bool TryGetPopupAt(string id, out float at)
        {
            at = 0f;
            Walker walker;
            if (id == null || !_growthDelta.ContainsKey(id) || !_walkers.TryGetValue(id, out walker)) return false;
            at = walker.PopupAt;
            return true;
        }

        public void ClearGrowthDeltas()
        {
            _growthDelta.Clear();
        }

        public bool IsPointerBusy(Vector2 mouse)
        {
            return _dragged != null || HitSquad(mouse) != null;
        }

        public void HandlePointer(Vector2 mouse, bool down, bool held, bool up, bool blocked)
        {
            if (up && _dragged != null)
            {
                EndDrag();
                return;
            }

            if (_dragged != null && held)
            {
                UpdateDrag(mouse);
                return;
            }

            if (blocked)
            {
                if (up) CancelPress(true);
                return;
            }

            if (down)
            {
                var hit = HitSquad(mouse);
                if (hit == null)
                {
                    SelectedId = null;
                    CancelPress(true);
                }
                else
                {
                    _pressed = hit;
                    _pressMouse = mouse;
                    _pressStart = _time;
                    _pressTravel = 0f;
                    hit.Walking = false;
                    hit.IdleUntil = _time + 999f;
                }
            }

            if (_pressed != null && held && _dragged == null && !down)
            {
                _pressTravel = Mathf.Max(_pressTravel, Vector2.Distance(mouse, _pressMouse));
                if (_pressed.Squad && _time - _pressStart >= LongPressSeconds)
                    BeginDrag(mouse);
            }

            if (_dragged != null && held)
                UpdateDrag(mouse);

            if (up && _pressed != null && _dragged == null)
            {
                if (_pressTravel < ClickSlop)
                    SelectedId = _pressed.Id;
                CancelPress(true);
            }
        }

        /// <summary>
        /// behindClubhouse が true のときは area_player_01（広い歩行エリア）にいる人だけ描く。
        /// </summary>
        public void Draw(float dayElapsed, bool behindClubhouse, string talkingId = null)
        {
            _drawOrder.Clear();
            foreach (var pair in _walkers)
            {
                if (InPlayerArea(pair.Value) != behindClubhouse) continue;
                _drawOrder.Add(pair.Value);
            }
            _drawOrder.Sort(CompareDraw);

            var previous = GUI.matrix;
            for (int i = 0; i < _drawOrder.Count; i++)
                DrawWalker(_drawOrder[i], previous, dayElapsed, talkingId);
            GUI.matrix = previous;
        }

        /// <summary>試合画面でも、抽選値を選手の頭上に出す。</summary>
        public void DrawGrowthRow(Rect area, float dayElapsed)
        {
            if (dayElapsed < 0f || _growthDelta.Count == 0) return;
            _growthRow.Clear();
            foreach (var pair in _walkers)
            {
                if (!pair.Value.Squad || pair.Value.Hidden || pair.Value.Alpha <= 0.01f || !_growthDelta.ContainsKey(pair.Key)) continue;
                _growthRow.Add(pair.Value);
            }

            if (_growthRow.Count == 0) return;
            EnsureGrowthStyles();
            float slot = area.width / _growthRow.Count;
            for (int i = 0; i < _growthRow.Count; i++)
            {
                var walker = _growthRow[i];
                float x = area.x + slot * i;
                var body = new Rect(x + 2f, area.y + 36f, Mathf.Max(4f, slot - 4f), Mathf.Max(8f, area.height - 40f));
                var frames = walker.Frames;
                var texture = frames != null ? frames.Stand : null;
                if (texture != null)
                    GUI.DrawTexture(body, texture, ScaleMode.ScaleToFit, true);
                DrawGrowthLabel(walker, new Rect(body.x, body.y, body.width, 8f), dayElapsed);
            }
        }

        void Sync(Club club)
        {
            _ids.Clear();
            _zones.Clear();
            _squadFlags.Clear();
            _hairColors.Clear();
            if (club != null)
                Collect(club);

            int signature = 17;
            for (int i = 0; i < _ids.Count; i++)
            {
                var hair = _hairColors[i];
                signature = signature * 31 + _ids[i].GetHashCode() + ((int)_zones[i] + 1) * 17;
                signature = signature * 31 + hair.r + hair.g * 3 + hair.b * 17;
            }
            if (signature == _signature && _walkers.Count == _ids.Count) return;
            _signature = signature;

            var alive = new HashSet<string>(_ids);
            _remove.Clear();
            foreach (var pair in _walkers)
            {
                if (!alive.Contains(pair.Key)) _remove.Add(pair.Key);
            }

            for (int i = 0; i < _remove.Count; i++)
            {
                AvatarLibrary.Destroy(_walkers[_remove[i]].Frames);
                _walkers.Remove(_remove[i]);
            }

            var uniform = club == null
                ? AvatarLibrary.Kit(1)
                : AvatarLibrary.Kit(club.EmblemIndex);
            for (int i = 0; i < _ids.Count; i++)
            {
                var hair = _hairColors[i];
                Walker walker;
                if (_walkers.TryGetValue(_ids[i], out walker))
                {
                    if (walker.Frames == null || !SameColor(walker.Hair, hair))
                    {
                        AvatarLibrary.Destroy(walker.Frames);
                        walker.Frames = _library.Create(_ids[i], uniform, hair, 0, OutfitOf(_zones[i]));
                        walker.Hair = hair;
                    }

                    walker.Squad = _squadFlags[i];
                    if (walker.Zone != _zones[i])
                    {
                        walker.Zone = _zones[i];
                        if (!walker.Held)
                        {
                            walker.Feet = RandomPoint(walker.Zone);
                            ChooseTarget(walker);
                        }
                    }

                    continue;
                }

                walker = new Walker
                {
                    Id = _ids[i],
                    Zone = _zones[i],
                    Frames = _library.Create(_ids[i], uniform, hair, 0, OutfitOf(_zones[i])),
                    Squad = _squadFlags[i],
                    Hair = hair,
                    Feet = RandomPoint(_zones[i]),
                    Speed = 46f + PositiveHash(_ids[i]) % 22,
                    AnimTime = (PositiveHash(_ids[i]) % 100) / 100f,
                    IdleUntil = _time + Random.Range(0.2f, 1.6f),
                    Facing = 0,
                    Walking = false,
                    Alpha = 1f,
                    Anchor = RandomPoint(_zones[i])
                };
                ChooseTarget(walker);
                _walkers.Add(walker.Id, walker);
            }
        }

        void Collect(Club club)
        {
            var squad = club.Squad;
            for (int i = 0; i < squad.Count; i++)
            {
                var player = squad[i];
                if (player == null) continue;
                var group = PositionUtility.GroupOf(player.BestPosition());
                Add(player.Id, Zone.Player, true, PositionUtility.GroupColor(group));
            }
            var team = club.Team;
            if (team != null)
            {
                Add(team.Manager, Zone.Player, false);
                Add(team.HeadCoach, Zone.Player, false);
                AddSlots(team.Coaches, Zone.Player);
                Add(team.PhysicalCoach, Zone.Player, false);
                AddSlots(team.Trainers, Zone.Player);
                Add(team.Doctor, Zone.Player, false);
            }

            var front = club.Front;
            if (front == null) return;
            Add(front.Secretary, Zone.Staff, false);
            Add(front.GeneralManager, Zone.Staff, false);
            AddSlots(front.Strengthening, Zone.Staff);
            AddSlots(front.Sales, Zone.Staff);
            AddSlots(front.PublicRelations, Zone.Staff);
            AddSlots(front.Business, Zone.Staff);
            AddSlots(front.Academy, Zone.Staff);
        }

        /// <summary>フロント（Staff の区画）はスーツ、選手と監督・コーチはユニフォーム。</summary>
        static AvatarOutfit OutfitOf(Zone zone)
        {
            return zone == Zone.Staff ? AvatarOutfit.Suit : AvatarOutfit.Uniform;
        }

        static bool SameColor(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        void Add(StaffMember staff, Zone zone, bool squad)
        {
            Add(staff != null ? staff.Id : null, zone, squad, AvatarLibrary.NaturalHair(staff != null ? staff.Id : null));
        }

        void AddSlots(StaffMember[] slots, Zone zone)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
                Add(slots[i], zone, false);
        }

        void Add(string id, Zone zone, bool squad, Color32 hair)
        {
            if (string.IsNullOrEmpty(id)) return;
            for (int i = 0; i < _ids.Count; i++)
            {
                if (_ids[i] == id)
                {
                    if (squad)
                    {
                        _zones[i] = zone;
                        _squadFlags[i] = true;
                        _hairColors[i] = hair;
                    }

                    return;
                }
            }

            _ids.Add(id);
            _zones.Add(zone);
            _squadFlags.Add(squad);
            _hairColors.Add(hair);
        }

        void Step(Walker walker, float deltaTime)
        {
            if (walker.Hidden || walker == _dragged || walker == _pressed) return;
            if (walker.Arriving)
            {
                if (_time < walker.IdleUntil) return;
                if (walker.FadeStart < 0f) walker.FadeStart = _time;
                float arrive = Mathf.Clamp01((_time - walker.FadeStart) / FadeInSeconds);
                walker.Alpha = arrive;
                walker.Walking = false;
                if (arrive < 1f) return;
                walker.Arriving = false;
                walker.Alpha = 1f;
                walker.FadeStart = -1f;
                walker.IdleUntil = _time + Random.Range(0.4f, 1.6f);
                return;
            }

            if (walker.Leaving)
            {
                if (_time < walker.IdleUntil) return;
                if (walker.FadeStart < 0f)
                {
                    walker.FadeStart = _time;
                    walker.Facing = -1;
                }

                float leave = Mathf.Clamp01((_time - walker.FadeStart) / FadeOutSeconds);
                walker.Alpha = 1f - leave;
                walker.Walking = true;
                walker.Facing = -1;
                walker.AnimTime += deltaTime;
                walker.Feet.x -= walker.Speed * deltaTime;
                if (leave < 1f) return;
                walker.Alpha = 0f;
                walker.Hidden = true;
                walker.Leaving = false;
                walker.Walking = false;
                walker.Direct = false;
                walker.FadeStart = -1f;
                return;
            }

            if (walker.Held)
            {
                if (walker.Hidden)
                {
                    walker.Held = false;
                    return;
                }

                MoveToward(walker, deltaTime, walker.Speed * MeetSpeed, true);
                if (!walker.Walking) FaceMeet(walker);
                return;
            }

            if (!walker.Walking)
            {
                if (_time < walker.IdleUntil) return;
                walker.Walking = true;
            }

            MoveToward(walker, deltaTime, walker.Speed, false);
        }

        void MoveToward(Walker walker, float deltaTime, float speed, bool hold)
        {
            var dest = NextPoint(walker);
            var delta = dest - walker.Feet;
            float distance = delta.magnitude;
            if (distance <= ArriveDistance)
            {
                walker.Feet = dest;
                if (NearTarget(walker)) Arrive(walker, hold);
                else walker.Walking = true;
                return;
            }

            walker.Walking = true;
            UpdateFacing(walker, delta);
            walker.AnimTime += deltaTime;
            float step = Mathf.Min(distance, speed * deltaTime);
            walker.Feet += delta / distance * step;
            if (distance - step > ArriveDistance) return;

            walker.Feet = dest;
            if (!NearTarget(walker)) return;
            Arrive(walker, hold);
        }

        void Arrive(Walker walker, bool hold)
        {
            walker.Walking = false;
            if (hold)
            {
                FaceMeet(walker);
                return;
            }

            walker.IdleUntil = _time + Random.Range(0.8f, 2.6f);
            ChooseTarget(walker);
        }

        static bool NearTarget(Walker walker)
        {
            return (walker.Target - walker.Feet).sqrMagnitude <= ArriveDistance * ArriveDistance;
        }

        void HoldAt(Walker walker, Vector2 spot)
        {
            walker.Held = true;
            walker.Direct = false;
            walker.Target = spot;
            walker.IdleUntil = _time;
            walker.Walking = !NearTarget(walker);
        }

        void ReleaseHold(string id)
        {
            Walker walker;
            if (string.IsNullOrEmpty(id) || !TryWalker(id, out walker) || !walker.Held) return;
            walker.Held = false;
            walker.FaceId = null;
            walker.Walking = false;
            walker.Direct = false;
            walker.IdleUntil = _time + Random.Range(0.4f, 1.2f);
            ChooseTarget(walker);
        }

        bool TryWalker(string id, out Walker walker)
        {
            walker = null;
            return !string.IsNullOrEmpty(id) && _walkers.TryGetValue(id, out walker);
        }

        void FaceMeet(Walker walker)
        {
            Walker other;
            if (string.IsNullOrEmpty(walker.FaceId) || !TryWalker(walker.FaceId, out other)) return;
            UpdateFacing(walker, other.Feet - walker.Feet);
        }

        /// <summary>土曜が試合日の日曜は、チームスタッフとフロントを最初から退場済みにする。</summary>
        void HideNonSquad()
        {
            foreach (var pair in _walkers)
            {
                var walker = pair.Value;
                if (walker.Squad || walker == _dragged || walker == _pressed) continue;
                walker.Hidden = true;
                walker.Leaving = false;
                walker.Arriving = false;
                walker.Direct = false;
                walker.Walking = false;
                walker.Alpha = 0f;
                walker.FadeStart = -1f;
            }
        }

        /// <summary>練習日の翌日の日曜に、下がっている選手を一度出してから退場させる。</summary>
        void PresentSquadForDeparture()
        {
            foreach (var pair in _walkers)
            {
                var walker = pair.Value;
                if (!walker.Squad || walker == _dragged || walker == _pressed) continue;
                bool onGround = !walker.Hidden && !walker.Leaving && walker.Alpha > 0.5f;
                if (onGround) continue;
                walker.Hidden = false;
                walker.Leaving = false;
                walker.Arriving = false;
                walker.Direct = false;
                walker.Walking = false;
                walker.Alpha = 1f;
                walker.FadeStart = -1f;
                if (!InZone(walker.Zone, walker.Feet))
                    walker.Feet = InZone(walker.Zone, walker.Anchor) ? walker.Anchor : RandomPoint(walker.Zone);
                walker.IdleUntil = _time;
            }
        }

        void UpdatePresence(Walker walker, bool absent)
        {
            if (walker == _dragged || walker == _pressed) return;
            if (absent)
            {
                if (walker.Leaving || walker.Hidden) return;
                walker.Arriving = false;
                walker.Leaving = true;
                walker.Hidden = false;
                walker.Direct = true;
                walker.Walking = false;
                walker.Alpha = 1f;
                walker.FadeStart = -1f;
                walker.Anchor = walker.Feet;
                walker.IdleUntil = _time + DepartDelay(walker);
                return;
            }

            if (walker.Arriving || (!walker.Leaving && !walker.Hidden)) return;
            walker.Leaving = false;
            walker.Hidden = false;
            walker.Arriving = true;
            walker.Direct = false;
            walker.Walking = false;
            walker.Alpha = 0f;
            walker.FadeStart = -1f;
            walker.IdleUntil = _time + DepartDelay(walker);
            if (!InZone(walker.Zone, walker.Anchor))
                walker.Anchor = RandomPoint(walker.Zone);
            walker.Feet = walker.Anchor;
        }

        static float DepartDelay(Walker walker)
        {
            float unit = (PositiveHash(walker.Id) % 1000) / 999f;
            return DepartDelayMin + unit * DepartDelaySpan + Random.Range(0f, 0.4f);
        }

        void ChooseTarget(Walker walker)
        {
            if (walker.Held || walker.Leaving || walker.Hidden || walker.Arriving) return;
            walker.Direct = false;
            walker.Target = RandomPoint(walker.Zone);
        }

        Vector2 NextPoint(Walker walker)
        {
            if (walker.Direct) return walker.Target;
            if (SameRect(walker.Zone, walker.Feet, walker.Target)) return walker.Target;
            int feet = RectIndex(walker.Zone, walker.Feet);
            int goal = RectIndex(walker.Zone, walker.Target);
            if (walker.Zone == Zone.Player)
            {
                if (feet == 1) return Gate(Zone.Player, 0, 1, PlayerLeftGateDesign);
                if (feet == 2) return Gate(Zone.Player, 0, 2, PlayerTopGateDesign);
                if (goal == 1) return Gate(Zone.Player, 0, 1, PlayerLeftGateDesign);
                return Gate(Zone.Player, 0, 2, PlayerTopGateDesign);
            }

            return Gate(Zone.Staff, 0, 1, StaffGateDesign);
        }

        static bool SameRect(Zone zone, Vector2 a, Vector2 b)
        {
            int count = ZoneCount(zone);
            for (int i = 0; i < count; i++)
            {
                var rect = ZoneRect(zone, i);
                if (rect.Contains(a) && rect.Contains(b)) return true;
            }

            return false;
        }

        static void UpdateFacing(Walker walker, Vector2 delta)
        {
            bool side = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) || delta.y < 0f;
            if (!side)
            {
                walker.Facing = 0;
                return;
            }

            if (delta.x < -0.01f) walker.Facing = -1;
            else if (delta.x > 0.01f) walker.Facing = 1;
            else if (walker.Facing == 0) walker.Facing = 1;
        }

        void DrawWalker(Walker walker, Matrix4x4 view, float dayElapsed, string talkingId)
        {
            var frames = walker.Frames;
            if (frames == null) return;
            int frame = (int)(walker.AnimTime / WalkFrameSeconds);
            Texture2D texture;
            bool flip = false;
            if (!walker.Walking || walker.Facing == 0)
            {
                texture = walker.Walking ? FrameAt(frames.WalkFront, frame) : frames.Stand;
            }
            else
            {
                texture = FrameAt(frames.WalkSide, frame);
                // 横歩きの原画は左向き。右へ進むときだけ左右を反転する。
                flip = walker.Facing > 0;
            }

            if (texture == null || walker.Hidden || walker.Alpha <= 0.01f) return;
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, walker.Alpha);
            float height = texture.height * CharacterScale;
            float width = texture.width * CharacterScale;
            if (!string.IsNullOrEmpty(talkingId) && walker.Id == talkingId)
            {
                float pulse = Mathf.Sin(UnityEngine.Time.time * 7f);
                width *= 1f + 0.1f * pulse;
                height *= 1f - 0.08f * pulse;
            }

            var rect = new Rect(walker.Feet.x - width * 0.5f, walker.Feet.y - height, width, height);
            if (!flip)
            {
                GUI.matrix = view;
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            }
            else
            {
                var flipMatrix = Matrix4x4.TRS(new Vector3(rect.xMax, 0f, 0f), Quaternion.identity, new Vector3(-1f, 1f, 1f));
                GUI.matrix = view * flipMatrix;
                GUI.DrawTexture(new Rect(0f, rect.y, rect.width, rect.height), texture, ScaleMode.StretchToFill, true);
            }

            if (walker.Id == SelectedId || walker == _dragged)
            {
                GUI.matrix = view;
                GUI.color = new Color(1f, 0.86f, 0.2f, (walker == _dragged ? 0.7f : 0.4f) * walker.Alpha);
                GUI.DrawTexture(new Rect(walker.Feet.x - 24f, walker.Feet.y - 9.6f, 48f, 12.8f), Texture2D.whiteTexture);
            }

            GUI.color = previousColor;
            GUI.matrix = view;
            DrawGrowthLabel(walker, rect, dayElapsed);
        }

        void DrawGrowthLabel(Walker walker, Rect body, float dayElapsed)
        {
            if (dayElapsed < 0f) return;
            int delta;
            if (!_growthDelta.TryGetValue(walker.Id, out delta) || delta == 0) return;
            float local = dayElapsed - walker.PopupAt;
            if (local < 0f || local > PopupSeconds) return;
            float t = local / PopupSeconds;
            float alpha = (t < 0.2f ? 1f : Mathf.Clamp01(1f - (t - 0.2f) / 0.8f)) * walker.Alpha;
            float rise = t * 72f;
            EnsureGrowthStyles();
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            var label = new Rect(body.center.x - 48f, body.y + 18f - rise, 96f, 32f);
            DrawSigned(label, delta, delta > 0 ? _plusStyle : _minusStyle, delta > 0 ? _plusShadow : _minusShadow);
            GUI.color = previous;
        }

        void EnsureGrowthStyles()
        {
            if (_plusStyle != null) return;
            _plusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.20f, 0.85f, 0.28f, 1f) }
            };
            _minusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.92f, 0.24f, 0.18f, 1f) }
            };
            _plusShadow = new GUIStyle(_plusStyle) { normal = { textColor = new Color(0f, 0f, 0f, 0.8f) } };
            _minusShadow = new GUIStyle(_minusStyle) { normal = { textColor = new Color(0f, 0f, 0f, 0.8f) } };
        }

        static void DrawSigned(Rect rect, int delta, GUIStyle style, GUIStyle shadow)
        {
            string text = delta > 0 ? "+" + delta : delta.ToString();
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadow);
            GUI.Label(rect, text, style);
        }

        static Texture2D FrameAt(Texture2D[] frames, int index)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[index % frames.Length];
        }

        static Vector2 RandomPoint(Zone zone)
        {
            int count = ZoneCount(zone);
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                var rect = ZoneRect(zone, i);
                total += rect.width * rect.height;
            }

            float roll = Random.value * total;
            var pick = ZoneRect(zone, 0);
            for (int i = 0; i < count; i++)
            {
                var rect = ZoneRect(zone, i);
                roll -= rect.width * rect.height;
                pick = rect;
                if (roll <= 0f) break;
            }

            return new Vector2(
                Random.Range(pick.xMin + EdgeMargin, pick.xMax - EdgeMargin),
                Random.Range(pick.yMin + EdgeMargin, pick.yMax - EdgeMargin));
        }

        int CompareDraw(Walker a, Walker b)
        {
            if (a == _dragged) return 1;
            if (b == _dragged) return -1;
            return a.Feet.y.CompareTo(b.Feet.y);
        }

        /// <summary>main2 の広い area_player_01。クラブハウスはこの範囲にかかる。</summary>
        static bool InPlayerArea(Walker walker)
        {
            return walker != null && walker.Zone == Zone.Player && ZoneRect(Zone.Player, 0).Contains(walker.Feet);
        }

        Walker HitSquad(Vector2 mouse)
        {
            Walker best = null;
            float bestY = float.NegativeInfinity;
            foreach (var pair in _walkers)
            {
                var walker = pair.Value;
                if (walker.Frames == null || walker.Hidden || walker.Alpha <= 0.01f) continue;
                if (InPlayerArea(walker) && MainScreenView.ClubhouseRect.Contains(mouse)) continue;
                var body = BodyRect(walker);
                if (!body.Contains(mouse)) continue;
                if (walker.Feet.y < bestY) continue;
                best = walker;
                bestY = walker.Feet.y;
            }

            return best;
        }

        void BeginDrag(Vector2 mouse)
        {
            _dragged = _pressed;
            _grabOffset = _dragged.Feet - mouse;
            _dragged.Walking = false;
            UpdateDrag(mouse);
        }

        void UpdateDrag(Vector2 mouse)
        {
            var next = mouse + _grabOffset;
            var delta = next - _dragged.Feet;
            if (delta.sqrMagnitude > 0.25f)
                UpdateFacing(_dragged, delta);
            _dragged.Feet = next;
            KeepInZone(_dragged);
        }

        void EndDrag()
        {
            var walker = _dragged;
            _dragged = null;
            _pressed = null;
            if (walker == null) return;
            if (!InZone(walker.Zone, walker.Feet))
                walker.Feet = ClampToZone(walker.Zone, walker.Feet);
            walker.Walking = false;
            walker.IdleUntil = _time + 0.35f;
            ChooseTarget(walker);
        }

        void CancelPress(bool resume)
        {
            var walker = _pressed;
            _pressed = null;
            if (!resume || walker == null || walker == _dragged) return;
            walker.IdleUntil = _time + 0.25f;
            ChooseTarget(walker);
        }

        static Rect BodyRect(Walker walker)
        {
            var frames = walker.Frames;
            var texture = frames.Stand != null ? frames.Stand : FrameAt(frames.WalkFront, 0);
            float height = (texture != null ? texture.height : 168f) * CharacterScale;
            float width = (texture != null ? texture.width : 80f) * CharacterScale;
            return new Rect(walker.Feet.x - width * 0.5f, walker.Feet.y - height, width, height);
        }

        void KeepInZone(Walker walker)
        {
            if (walker == null || InZone(walker.Zone, walker.Feet)) return;
            walker.Feet = ClampToZone(walker.Zone, walker.Feet);
        }

        static bool InZone(Zone zone, Vector2 point)
        {
            return RectIndex(zone, point) >= 0;
        }

        static Vector2 ClampToZone(Zone zone, Vector2 point)
        {
            int count = ZoneCount(zone);
            var best = point;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var candidate = ClosestOnRect(Inset(ZoneRect(zone, i)), point);
                float distance = (candidate - point).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate;
            }

            return best;
        }

        static Rect Inset(Rect rect)
        {
            return new Rect(
                rect.xMin + EdgeMargin,
                rect.yMin + EdgeMargin,
                Mathf.Max(8f, rect.width - EdgeMargin * 2f),
                Mathf.Max(8f, rect.height - EdgeMargin * 2f));
        }

        static Vector2 ClosestOnRect(Rect rect, Vector2 point)
        {
            return new Vector2(
                Mathf.Clamp(point.x, rect.xMin, rect.xMax),
                Mathf.Clamp(point.y, rect.yMin, rect.yMax));
        }

        static int PositiveHash(string id)
        {
            unchecked
            {
                int h = 23;
                for (int i = 0; i < id.Length; i++)
                    h = h * 31 + id[i];
                return h & int.MaxValue;
            }
        }
    }
}
