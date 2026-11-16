using System;
using System.Collections.Generic;
using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 操作がないまま 5 秒たつと、立ち止まってから吹き出しを出す。
    /// 1 人はその場で止まり、2 人は歩み寄って止まる。話のあいだに次の候補が位置について待つ。
    /// ズームと吹き出しだけで、能力やお金は変えない。
    /// </summary>
    public sealed class MiniEventDirector
    {
        const float IdleSeconds = 5f;
        const float TalkSeconds = 5f;
        const float RetrySeconds = 0.6f;
        const float MeetLimitSeconds = 40f;
        const float MeetPauseSeconds = 0.45f;
        /// <summary>操作中でも友達イベントを割り込ませるための候補チェックの間隔。</summary>
        const float FriendCheckSeconds = 0.5f;
        // character_message。Figma main2 の 500×138。吹き出し 500×80、名前枠は x=32 の 240×24、本文 432×71。
        const float BoxWidth = 500f;
        const float BubbleY = 15f;
        const float BubbleHeight = 80f;
        const float NameX = 32f;
        const float NameWidth = 240f;
        const float NameHeight = 24f;
        const float MessageX = 32f;
        const float MessageY = 24f;
        const float MessageWidth = 432f;
        const float MessageHeight = 71f;
        const float TailWidth = 26f;
        const float TailHeight = 45f;
        const float TailOverlap = 8f;
        const float TailY = BubbleY + BubbleHeight - TailOverlap;
        const float BoxHeight = TailY + TailHeight;

        sealed class Cue
        {
            public const int MaxBeats = 3;
            public bool Armed;
            public string A;
            public string B;
            public int BeatCount;

            /// <summary>関係イベント。最後の吹き出しが出たら A と B の段階を Next に進める。</summary>
            public bool Relation;
            public RelationStage Next;
            public readonly string[] Id = new string[MaxBeats];
            public readonly string[] Name = new string[MaxBeats];
            public readonly string[] Line = new string[MaxBeats];

            public void Clear()
            {
                Armed = false;
                A = null;
                B = null;
                BeatCount = 0;
                Relation = false;
                Next = RelationStage.None;
                for (int i = 0; i < MaxBeats; i++)
                {
                    Id[i] = null;
                    Name[i] = null;
                    Line[i] = null;
                }
            }

            public bool Uses(string id)
            {
                return !string.IsNullOrEmpty(id) && (id == A || id == B);
            }
        }

        readonly List<string> _visible = new List<string>();
        readonly HashSet<string> _seen = new HashSet<string>();
        readonly Cue _play = new Cue();
        readonly Cue _next = new Cue();
        Cue _fill;
        GroundCrowd _crowd;
        bool _nextAttempted;
        string _speakerId;
        string _speakerName;
        string _line;
        int _beat;
        int _beatCount;
        readonly string[] _beatId = new string[4];
        readonly string[] _beatName = new string[4];
        readonly string[] _beatLine = new string[4];
        bool _operating;
        float _friendCheck;
        bool _talkFriend;
        RelationStage _talkNext;
        bool _friendDone;
        string _friendA;
        string _friendB;
        float _shown;
        float _idle;
        float _retry;
        float _gatherTime;
        float _meetPause = -1f;
        bool _gathering;
        string _lastSpeaker;
        Vector2 _heldFocus;
        bool _hasFocus;
        Font _font;
        GUIStyle _messageStyle;
        GUIStyle _nameStyle;
        Texture2D _bubble;
        Texture2D _namePlate;
        Texture2D _tailDown;
        Texture2D _tailUp;

        public bool Active => !string.IsNullOrEmpty(_speakerId);
        public string SpeakerId => _speakerId;

        /// <summary>友達イベントを見せている最中。オートの日送りを始めずに待ってもらう。</summary>
        public bool HoldsDay => _play.Armed && _play.Relation && (Active || _gathering);

        /// <summary>
        /// canShow はメイン画面が見えているか（試合・ダイアログなどで覆われていない）。
        /// 友達イベントは操作中でも割り込んで最後まで見せる。覆われたときだけ取りやめる（友達にはならない）。
        /// </summary>
        public void Tick(GameSession session, GroundCrowd crowd, bool castPresent, float deltaTime, bool canShow = true)
        {
            _crowd = crowd;
            if (session == null || crowd == null || session.PlayerClub == null)
            {
                ReleaseCues(crowd);
                End();
                return;
            }

            _operating = Operated();
            if (_operating)
            {
                _idle = 0f;
                _retry = 0f;
                if (!_play.Relation)
                {
                    ReleaseCues(crowd);
                    End();
                }
                else if (_next.Armed && !_next.Relation)
                {
                    // 友達イベントは続ける。後ろに控えた普通の話だけ取りやめる。
                    ReleaseExcept(crowd, _next, _play);
                    _next.Clear();
                    _nextAttempted = false;
                }
            }

            if (!canShow && (_play.Relation || _next.Relation))
            {
                ReleaseCues(crowd);
                End();
            }

            if (!castPresent)
            {
                if (Active || _gathering || _play.Armed || _next.Armed)
                {
                    ReleaseCues(crowd);
                    End();
                }

                return;
            }

            if (_next.Armed && crowd.StandLost(_next.A, _next.B))
            {
                crowd.ReleaseStand(_next.A, _next.B);
                _next.Clear();
                _nextAttempted = false;
            }

            if (_gathering)
            {
                if (!_play.Armed || crowd.StandLost(_play.A, _play.B))
                {
                    crowd.ReleaseStand(_play.A, _play.B);
                    _play.Clear();
                    _gathering = false;
                }
                else
                {
                    _gatherTime += deltaTime;
                    bool ready = crowd.StandSettled(_play.A, _play.B) || _gatherTime >= MeetLimitSeconds;
                    if (ready)
                    {
                        if (_meetPause < 0f) _meetPause = MeetPauseSeconds;
                        else _meetPause -= deltaTime;
                        if (_meetPause <= 0f) StartTalk();
                    }

                    QueueNext(session, crowd);
                    return;
                }
            }

            if (Active)
            {
                if (crowd.StandLost(_play.A, _play.B))
                {
                    FinishTalk(session, crowd);
                    return;
                }

                Vector2 live;
                if (crowd.TryFocusPoint(_speakerId, out live))
                {
                    _heldFocus = live;
                    _hasFocus = true;
                }

                // 関係イベントは最後の「〜になった！」の吹き出しが出た時点で段階を進める。途中で止まれば進まない。
                if (_talkFriend && !_friendDone && _beat == _beatCount - 1)
                {
                    _friendDone = true;
                    session.CompleteRelationEvent(_friendA, _friendB, _talkNext);
                }

                QueueNext(session, crowd);
                _shown += deltaTime;
                if (_shown < TalkSeconds) return;
                if (_beat + 1 < _beatCount)
                {
                    ShowBeat(_beat + 1);
                    return;
                }

                FinishTalk(session, crowd);
                return;
            }

            // 操作中や、待ち時間の途中でも、友達イベントの組が画面にいれば割り込ませる。
            if (canShow && (_operating || _idle < IdleSeconds))
            {
                _friendCheck -= deltaTime;
                if (_friendCheck <= 0f)
                {
                    _friendCheck = FriendCheckSeconds;
                    if (session.RelationEventReady && TryFill(session, crowd, _play, true))
                    {
                        _gathering = true;
                        _gatherTime = 0f;
                        _meetPause = -1f;
                        _nextAttempted = false;
                        QueueNext(session, crowd);
                        return;
                    }
                }
            }

            if (_operating) return;
            _idle += deltaTime;
            if (_idle < IdleSeconds) return;
            if (_retry > 0f)
            {
                _retry -= deltaTime;
                return;
            }

            if (!TryFill(session, crowd, _play))
            {
                _retry = RetrySeconds;
                return;
            }

            _gathering = true;
            _gatherTime = 0f;
            _meetPause = -1f;
            _nextAttempted = false;
            QueueNext(session, crowd);
        }

        void QueueNext(GameSession session, GroundCrowd crowd)
        {
            // 操作中は普通の話を控えに並べない。
            if (_operating || _next.Armed || _nextAttempted || !_play.Armed) return;
            _nextAttempted = true;
            TryFill(session, crowd, _next);
        }

        void FinishTalk(GameSession session, GroundCrowd crowd)
        {
            ReleaseExcept(crowd, _play, _next);
            _play.Clear();
            _talkFriend = false;
            _speakerId = null;
            _speakerName = null;
            _line = null;
            _shown = 0f;
            _hasFocus = false;
            _gathering = false;
            if (_next.Armed)
            {
                CopyCue(_next, _play);
                _next.Clear();
            }
            else if (_operating)
            {
                // 割り込んだ友達イベントが終わった。操作中なので次の話は始めない。
                _retry = 0f;
                return;
            }
            else if (!TryFill(session, crowd, _play))
            {
                _retry = RetrySeconds;
                return;
            }

            _nextAttempted = false;
            if (crowd.StandSettled(_play.A, _play.B))
            {
                StartTalk();
                QueueNext(session, crowd);
                return;
            }

            _gathering = true;
            _gatherTime = 0f;
            _meetPause = -1f;
            QueueNext(session, crowd);
        }

        bool TryFill(GameSession session, GroundCrowd crowd, Cue cue, bool friendOnly = false)
        {
            crowd.CopyVisibleIds(_visible);
            for (int i = _visible.Count - 1; i >= 0; i--)
            {
                string id = _visible[i];
                if (cue != _play && _play.Uses(id)) _visible.RemoveAt(i);
                else if (cue != _next && _next.Uses(id)) _visible.RemoveAt(i);
            }

            if (_visible.Count == 0) return false;
            _fill = cue;
            cue.Clear();
            bool started = friendOnly ? TryRelation(session, crowd) : TryStart(session, crowd);
            _fill = null;
            if (started && cue.Armed) return true;
            cue.Clear();
            return false;
        }

        static void CopyCue(Cue from, Cue to)
        {
            to.Armed = from.Armed;
            to.A = from.A;
            to.B = from.B;
            to.BeatCount = from.BeatCount;
            to.Relation = from.Relation;
            to.Next = from.Next;
            for (int i = 0; i < Cue.MaxBeats; i++)
            {
                to.Id[i] = from.Id[i];
                to.Name[i] = from.Name[i];
                to.Line[i] = from.Line[i];
            }
        }

        void ReleaseExcept(GroundCrowd crowd, Cue leaving, Cue staying)
        {
            if (leaving.A != null && (staying == null || !staying.Uses(leaving.A)))
                crowd.ReleaseStand(leaving.A, null);
            if (leaving.B != null && (staying == null || !staying.Uses(leaving.B)))
                crowd.ReleaseStand(leaving.B, null);
        }

        void ReleaseCues(GroundCrowd crowd)
        {
            if (crowd != null)
            {
                crowd.ReleaseStand(_play.A, _play.B);
                crowd.ReleaseStand(_next.A, _next.B);
            }

            _play.Clear();
            _next.Clear();
            _nextAttempted = false;
            _gathering = false;
        }

        void StartTalk()
        {
            _gathering = false;
            _meetPause = -1f;
            _beatCount = _play.BeatCount;
            _talkFriend = _play.Relation;
            _talkNext = _play.Next;
            _friendDone = false;
            _friendA = _play.A;
            _friendB = _play.B;
            for (int i = 0; i < _beatCount && i < Cue.MaxBeats; i++)
            {
                _beatId[i] = _play.Id[i];
                _beatName[i] = _play.Name[i];
                _beatLine[i] = _play.Line[i];
            }

            if (_beatCount > 0) _lastSpeaker = _play.Id[_beatCount - 1];
            ShowBeat(0);
        }

        /// <summary>話しているあいだの注視点。姿が隠れても、直前の位置でズームを保つ。</summary>
        public bool TryCameraFocus(GroundCrowd crowd, out Vector2 focus)
        {
            focus = _heldFocus;
            if (!Active) return false;
            Vector2 live;
            if (crowd != null && crowd.TryFocusPoint(_speakerId, out live))
            {
                _heldFocus = live;
                _hasFocus = true;
                focus = live;
                return true;
            }

            return _hasFocus;
        }

        /// <summary>head は UI 座標。frame_name はクラブカラー、character_name は背番号カラー。</summary>
        public void Draw(Vector2 head, Font font, Color plateColor, Color nameColor)
        {
            if (!Active || font == null || string.IsNullOrEmpty(_line)) return;
            EnsureArt(font);
            bool below = head.y - BoxHeight - 8f < 16f;
            float left = Mathf.Clamp(head.x - BoxWidth * 0.5f, 16f, GameRunner.ReferenceWidth - BoxWidth - 16f);
            float top = below
                ? Mathf.Clamp(head.y + 8f, 16f, GameRunner.ReferenceHeight - BoxHeight - 16f)
                : Mathf.Clamp(head.y - BoxHeight - 8f, 16f, GameRunner.ReferenceHeight - BoxHeight - 16f);
            float tailX = Mathf.Clamp(head.x - left - TailWidth * 0.5f, 24f, BoxWidth - TailWidth - 24f);
            var previous = GUI.color;

            float bubbleTop = below ? TailHeight - TailOverlap : BubbleY;
            float nameTop = bubbleTop - BubbleY;
            var tail = new Rect(left + tailX, below ? top : top + TailY, TailWidth, TailHeight);
            GUI.color = Color.white;
            GUI.DrawTexture(tail, below ? _tailUp : _tailDown, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(new Rect(left, top + bubbleTop, BoxWidth, BubbleHeight), _bubble, ScaleMode.StretchToFill, true);
            GUI.color = plateColor;
            GUI.DrawTexture(new Rect(left + NameX, top + nameTop, NameWidth, NameHeight), _namePlate, ScaleMode.StretchToFill, true);
            GUI.color = previous;

            _nameStyle.normal.textColor = nameColor;
            GUI.Label(new Rect(left + NameX, top + nameTop, NameWidth, NameHeight), _speakerName ?? "", _nameStyle);
            GUI.Label(new Rect(left + MessageX, top + bubbleTop + (MessageY - BubbleY), MessageWidth, MessageHeight), _line, _messageStyle);
        }

        bool TryStart(GameSession session, GroundCrowd crowd)
        {
            var today = session.Time.CurrentDate.Date;
            if (TryPairBirthday(session, today, crowd)) return true;
            if (TryBirthday(session, today)) return true;
            if (TryRelation(session, crowd)) return true;
            if (UnityEngine.Random.value < 0.35f && TryPairTalk(session, crowd)) return true;
            if (TryGoal(session, today)) return true;
            if (UnityEngine.Random.value < 0.02f && TryLife(session)) return true;
            if (session.IsFirstHalf(today) && UnityEngine.Random.value < 0.4f && TrySeasonGoal(session)) return true;
            if (UnityEngine.Random.value < 0.45f && TryAppearance(session)) return true;
            if (TryPersonal(session)) return true;
            return TryChatter(session);
        }

        bool TryPairBirthday(GameSession session, DateTime today, GroundCrowd crowd)
        {
            if (_visible.Count < 2) return false;
            for (int i = 0; i < _visible.Count; i++)
            {
                string id = _visible[i];
                int month;
                int day;
                CharacterLife.Birthday(id, out month, out day);
                if (today.Month != month || today.Day != day) continue;
                string other = OtherVisible(id);
                if (other == null || !Accept(session, other)) continue;
                string key = PairKey(id, other, "birthday", today);
                if (_seen.Contains(key)) continue;
                Player celebrated;
                StaffMember celebratedStaff;
                session.TryClubPerson(id, out celebrated, out celebratedStaff);
                Player friend;
                StaffMember friendStaff;
                session.TryClubPerson(other, out friend, out friendStaff);
                string celebratedName = NameOf(celebrated, celebratedStaff);
                string friendName = NameOf(friend, friendStaff);
                _seen.Add(key);
                _seen.Add(id + ":birthday:" + today.Year);
                return BeginPair(
                    crowd,
                    other, friendName, celebratedName + "、誕生日おめでとう。",
                    id, celebratedName, "ありがとう。自分では言いだしにくかった。");
            }

            return false;
        }

        bool TryPairTalk(GameSession session, GroundCrowd crowd)
        {
            if (_visible.Count < 2) return false;
            ShuffleVisible();
            string first = null;
            string second = null;
            for (int i = 0; i < _visible.Count; i++)
            {
                if (!Accept(session, _visible[i])) continue;
                if (first == null) first = _visible[i];
                else { second = _visible[i]; break; }
            }

            if (first == null || second == null) return false;
            Player aPlayer;
            StaffMember aStaff;
            Player bPlayer;
            StaffMember bStaff;
            session.TryClubPerson(first, out aPlayer, out aStaff);
            session.TryClubPerson(second, out bPlayer, out bStaff);
            string aName = NameOf(aPlayer, aStaff);
            string bName = NameOf(bPlayer, bStaff);
            int topic = -1;
            int start = UnityEngine.Random.Range(0, 4);
            var day = session.Time.CurrentDate.Date;
            for (int n = 0; n < 4; n++)
            {
                int candidate = (start + n) % 4;
                if (_seen.Contains(PairKey(first, second, "talk" + candidate, day))) continue;
                topic = candidate;
                break;
            }

            if (topic < 0) return false;
            _seen.Add(PairKey(first, second, "talk" + topic, day));
            if (topic == 0)
            {
                return BeginPair(
                    crowd,
                    first, aName, "すきなものは" + CharacterLife.FavoriteFood(first) + "。",
                    second, bName, CharacterLife.Pronoun(second) + "は" + CharacterLife.FavoriteFood(second) + "。");
            }

            if (topic == 1)
            {
                string country;
                string activity;
                CharacterLife.Travel(first, out country, out activity);
                string countryB;
                string activityB;
                CharacterLife.Travel(second, out countryB, out activityB);
                return BeginPair(
                    crowd,
                    first, aName, "休みの日は" + country + "で" + activity + "していることが多い。",
                    second, bName, CharacterLife.Pronoun(second) + "は" + countryB + "で" + activityB + "したい。");
            }

            if (topic == 2)
            {
                return BeginPair(
                    crowd,
                    first, aName, "最近何かいいことあった？",
                    second, bName, GoodNews(second));
            }

            string hobby = CharacterLife.Hobby(first);
            string reply = CharacterLife.IsMovie(hobby)
                ? CharacterLife.Movie(second) + "映画はよく観るよ。"
                : CharacterLife.Pronoun(second) + "は" + CharacterLife.Hobby(second) + "。今度一緒にどう？";
            return BeginPair(
                crowd,
                first, aName, "趣味は" + (CharacterLife.IsMovie(hobby) ? "映画" : hobby) + "。",
                second, bName, reply);
        }

        /// <summary>
        /// 関係イベント（友達・親友・恋人・夫婦）。段階が進む条件を満たした自クラブの2人が画面に映っていれば、
        /// 隣に並んで話し、段階が進む。全種類まとめて、前回から2日たつまでは起きない（GameSession.RelationEventReady）。
        /// 結婚 → 恋人 → 親友 → 友達 の順に優先する。
        /// </summary>
        bool TryRelation(GameSession session, GroundCrowd crowd)
        {
            if (_visible.Count < 2 || !session.RelationEventReady) return false;
            // 再生中・待機中の関係イベントがあれば重ねない。
            if ((_fill != _play && _play.Relation) || (_fill != _next && _next.Relation)) return false;
            var candidates = session.RelationEventCandidates();
            if (candidates.Count == 0) return false;
            var visible = new HashSet<string>(_visible);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var swap = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = swap;
            }

            for (int priority = 3; priority >= 0; priority--)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    var next = candidates[i].Next;
                    if (GameSession.RelationEventPriority(next) != priority) continue;
                    var rel = candidates[i].Rel;
                    if (!visible.Contains(rel.PlayerIdA) || !visible.Contains(rel.PlayerIdB)) continue;
                    if (!Free(rel.PlayerIdA) || !Free(rel.PlayerIdB)) continue;
                    Player a;
                    Player b;
                    StaffMember unused;
                    if (!session.TryClubPerson(rel.PlayerIdA, out a, out unused) || a == null) continue;
                    if (!session.TryClubPerson(rel.PlayerIdB, out b, out unused) || b == null) continue;
                    // 話しかけるほうを入れ替えて、毎回同じ人から始まらないようにする。
                    if (UnityEngine.Random.value < 0.5f)
                    {
                        var swap = a;
                        a = b;
                        b = swap;
                    }

                    string lineA;
                    string lineB;
                    string nameA = NameOf(a, null);
                    string nameB = NameOf(b, null);
                    string result;
                    switch (next)
                    {
                        case RelationStage.Married:
                            MarriageLines(a, b, out lineA, out lineB);
                            result = nameA + "と" + nameB + "が結婚した！";
                            break;
                        case RelationStage.Lover:
                            LoverLines(a, b, out lineA, out lineB);
                            result = nameA + "と" + nameB + "が恋人になった！";
                            break;
                        case RelationStage.BestFriend:
                            BestFriendLines(a, b, nameB, out lineA, out lineB);
                            result = nameA + "と" + nameB + "が親友になった！";
                            break;
                        default:
                            FriendLines(a, b, out lineA, out lineB);
                            result = nameA + "と" + nameB + "が友達になった！";
                            break;
                    }

                    if (BeginRelation(crowd, a.Id, nameA, lineA, b.Id, nameB, lineB, result, next)) return true;
                }
            }

            return false;
        }

        static void LoverLines(Player a, Player b, out string lineA, out string lineB)
        {
            string meB = CharacterLife.Pronoun(b.Id);
            switch (UnityEngine.Random.Range(0, 3))
            {
                case 0:
                    lineA = "あのさ、ずっと言いたかったことがあるんだ。好きです。";
                    lineB = "……" + meB + "も、同じ気持ちだよ。";
                    return;
                case 1:
                    lineA = "試合のあと、いちばんに話したくなるのはきみなんだ。";
                    lineB = "うれしい。" + meB + "もそうだったよ。";
                    return;
                default:
                    lineA = "今度のオフ、二人で出かけない？　大事な話がしたい。";
                    lineB = "うん。……その話、たぶん" + meB + "も同じだと思う。";
                    return;
            }
        }

        static void MarriageLines(Player a, Player b, out string lineA, out string lineB)
        {
            switch (UnityEngine.Random.Range(0, 3))
            {
                case 0:
                    lineA = "これからもずっと一緒にいてほしい。結婚しよう。";
                    lineB = "はい。よろしくお願いします！";
                    return;
                case 1:
                    lineA = "引退したあとも、となりにいてくれる？";
                    lineB = "もちろん。ずっと一緒だよ。";
                    return;
                default:
                    lineA = "このクラブで出会えてよかった。家族になってください。";
                    lineB = "……うん。喜んで！";
                    return;
            }
        }

        static void BestFriendLines(Player a, Player b, string nameB, out string lineA, out string lineB)
        {
            switch (UnityEngine.Random.Range(0, 3))
            {
                case 0:
                    lineA = nameB + "とは、何でも話せるね。";
                    lineB = "うん、いちばんの親友だよ！";
                    return;
                case 1:
                    lineA = "調子が悪いとき、いつも声をかけてくれてありがとう。";
                    lineB = "おたがいさま。これからも助け合おう！";
                    return;
                default:
                    lineA = "引退しても、ずっと付き合っていこうな。";
                    lineB = "当たり前だよ。一生の親友だ！";
                    return;
            }
        }

        bool Free(string id)
        {
            if (_play.Uses(id) && _fill != _play) return false;
            if (_next.Uses(id) && _fill != _next) return false;
            return true;
        }

        /// <summary>友達になる理由。2人に共通する点から1つ選ぶ。何も無ければ居残り練習。</summary>
        static void FriendLines(Player a, Player b, out string lineA, out string lineB)
        {
            var reasons = new List<int>(8);
            string food = CharacterLife.FavoriteFood(a.Id);
            string hobby = CharacterLife.Hobby(a.Id);
            string movie = CharacterLife.Movie(a.Id);
            string country;
            string activity;
            CharacterLife.Travel(a.Id, out country, out activity);
            string countryB;
            string activityB;
            CharacterLife.Travel(b.Id, out countryB, out activityB);
            int monthA;
            int dayA;
            int monthB;
            int dayB;
            CharacterLife.Birthday(a.Id, out monthA, out dayA);
            CharacterLife.Birthday(b.Id, out monthB, out dayB);

            if (!string.IsNullOrEmpty(food) && food == CharacterLife.FavoriteFood(b.Id)) reasons.Add(0);
            if (!string.IsNullOrEmpty(hobby) && hobby == CharacterLife.Hobby(b.Id)) reasons.Add(1);
            if (!string.IsNullOrEmpty(country) && country == countryB) reasons.Add(2);
            if (!string.IsNullOrEmpty(movie) && movie == CharacterLife.Movie(b.Id)) reasons.Add(3);
            if (monthA == monthB) reasons.Add(4);
            if (PositionUtility.GroupOf(a.PreferredPosition) == PositionUtility.GroupOf(b.PreferredPosition)) reasons.Add(5);
            if (Math.Abs(a.Age - b.Age) <= 3) reasons.Add(6);
            int reason = reasons.Count > 0 ? reasons[UnityEngine.Random.Range(0, reasons.Count)] : 7;
            string me = CharacterLife.Pronoun(a.Id);

            switch (reason)
            {
                case 0:
                    lineA = "え、" + food + "が好きなの？　" + me + "も大好き！";
                    lineB = "本当？　今度一緒に食べに行こうよ。";
                    return;
                case 1:
                    if (CharacterLife.IsMovie(hobby))
                    {
                        lineA = "趣味が映画なんだって？　" + me + "もよく観るよ。";
                        lineB = "うれしい！　今度おすすめを教えて。";
                    }
                    else
                    {
                        lineA = "趣味が" + hobby + "なんだって？　" + me + "も！";
                        lineB = "仲間がいてうれしい。今度一緒にやろう！";
                    }

                    return;
                case 2:
                    lineA = country + "が好きなの？　" + me + "もずっと気になってた。";
                    lineB = "いつか一緒に" + country + "へ行けたらいいね！";
                    return;
                case 3:
                    lineA = movie + "映画、" + me + "も好きなんだ。";
                    lineB = "話が合うね。次のオフに一緒に観に行こう！";
                    return;
                case 4:
                    lineA = "誕生日、同じ" + monthA + "月なんだね！";
                    lineB = "本当だ！　今年は一緒にお祝いしよう。";
                    return;
                case 5:
                    lineA = "さっきの練習、息がぴったりだったね。";
                    lineB = "同じポジション同士、もっと話そうよ！";
                    return;
                case 6:
                    lineA = "同じ世代だと、話が合うね。";
                    lineB = "うん、これからもよろしく！";
                    return;
                default:
                    lineA = "今日も居残り練習、一緒だったね。";
                    lineB = "ひとりじゃないから頑張れたよ。ありがとう。";
                    return;
            }
        }

        bool BeginRelation(GroundCrowd crowd, string firstId, string firstName, string firstLine, string secondId, string secondName, string secondLine, string result, RelationStage next)
        {
            if (!BeginPair(crowd, firstId, firstName, firstLine, secondId, secondName, secondLine)) return false;
            // 3つ目は名前なしの吹き出しで結果を出す。注視は話しかけたほうに戻す。
            _fill.BeatCount = 3;
            _fill.Id[2] = firstId;
            _fill.Name[2] = "だれか";
            _fill.Line[2] = result;
            _fill.Relation = true;
            _fill.Next = next;
            _lastSpeaker = firstId;
            return true;
        }

        static string GoodNews(string id)
        {
            int pick = 0;
            if (!string.IsNullOrEmpty(id))
                pick = Math.Abs(id.GetHashCode() % 4);
            string pronoun = CharacterLife.Pronoun(id);
            if (pick == 0)
                return pronoun + "は、このあいだゴールが決まった。まだ気持ちがいい。";
            if (pick == 1)
            {
                string hobby = CharacterLife.Hobby(id);
                if (CharacterLife.IsMovie(hobby))
                    return CharacterLife.Movie(id) + "映画を観た。おもしろかった。";
                return "趣味の" + hobby + "がはかどった。";
            }

            if (pick == 2)
                return "すきな" + CharacterLife.FavoriteFood(id) + "を食べた。";
            string country;
            string activity;
            CharacterLife.Travel(id, out country, out activity);
            return country + "で" + activity + "できた。いい休みだった。";
        }

        string OtherVisible(string id)
        {
            for (int i = 0; i < _visible.Count; i++)
            {
                if (_visible[i] != id) return _visible[i];
            }

            return null;
        }

        static string PairKey(string a, string b, string topic, DateTime day)
        {
            string first = string.CompareOrdinal(a, b) <= 0 ? a : b;
            string second = first == a ? b : a;
            return first + ":" + second + ":" + topic + ":" + day.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        }

        bool BeginPair(GroundCrowd crowd, string firstId, string firstName, string firstLine, string secondId, string secondName, string secondLine)
        {
            if (_fill == null || crowd == null || !crowd.BeginStand(firstId, secondId)) return false;
            _fill.A = firstId;
            _fill.B = secondId;
            _fill.BeatCount = 2;
            _fill.Id[0] = firstId;
            _fill.Name[0] = firstName;
            _fill.Line[0] = firstLine;
            _fill.Id[1] = secondId;
            _fill.Name[1] = secondName;
            _fill.Line[1] = secondLine;
            _fill.Armed = true;
            _lastSpeaker = secondId;
            _retry = 0f;
            return true;
        }

        void ShowBeat(int index)
        {
            _beat = index;
            _speakerId = _beatId[index];
            string name = _beatName[index];
            _speakerName = name == "だれか" ? "" : name;
            _line = _beatLine[index] ?? "";
            _shown = 0f;
        }

        bool TryBirthday(GameSession session, DateTime today)
        {
            for (int i = 0; i < _visible.Count; i++)
            {
                string id = _visible[i];
                int month;
                int day;
                CharacterLife.Birthday(id, out month, out day);
                if (today.Month != month || today.Day != day) continue;
                string key = id + ":birthday:" + today.Year;
                if (_seen.Contains(key) || !Accept(session, id)) continue;
                Player player;
                StaffMember staff;
                session.TryClubPerson(id, out player, out staff);
                _line = Say(id, NameOf(player, staff), "birthday", null, null);
                _seen.Add(key);
                return Begin(session, id);
            }

            return false;
        }

        bool TryGoal(GameSession session, DateTime today)
        {
            var yesterday = today.AddDays(-1);
            for (int i = 0; i < _visible.Count; i++)
            {
                Player player;
                StaffMember staff;
                if (!session.TryClubPerson(_visible[i], out player, out staff) || player == null) continue;
                var scored = player.LastGoalDate.Date;
                if (scored != today && scored != yesterday) continue;
                string key = player.Id + ":goal:" + scored.Ticks;
                if (_seen.Contains(key) || !Accept(session, player.Id)) continue;
                _line = Say(player.Id, NameOf(player, null), "goal", null, null);
                _seen.Add(key);
                return Begin(session, player.Id);
            }

            return false;
        }

        bool TryLife(GameSession session)
        {
            ShuffleVisible();
            for (int i = 0; i < _visible.Count; i++)
            {
                Player player;
                StaffMember staff;
                if (!session.TryClubPerson(_visible[i], out player, out staff)) continue;
                // 選手の結婚・子供は RelationshipService が決める。ここではスタッフだけ。
                if (player != null || staff == null) continue;
                int age = player != null ? player.Age : (staff != null ? staff.Age : 0);
                bool married = player != null ? player.Married : staff != null && staff.Married;
                int children = player != null ? player.Children : (staff != null ? staff.Children : 0);
                string id = player != null ? player.Id : staff.Id;
                string name = NameOf(player, staff);
                string key = id + ":life:" + session.CurrentSeasonStart.Year;
                if (_seen.Contains(key) || !Accept(session, id)) continue;
                int roll = UnityEngine.Random.Range(0, 3);
                bool happened = false;
                if (roll == 0 && !married && age >= 20)
                {
                    if (player != null) player.Married = true;
                    else staff.Married = true;
                    _line = Say(id, name, "marriage", null, null);
                    happened = true;
                }
                else if (roll == 1 && married)
                {
                    if (player != null) player.Married = false;
                    else staff.Married = false;
                    _line = Say(id, name, "divorce", null, null);
                    happened = true;
                }
                else if (roll == 2 && married && children < 4)
                {
                    if (player != null) player.Children++;
                    else staff.Children++;
                    int count = player != null ? player.Children : staff.Children;
                    _line = Say(id, name, "child", count.ToString(), null);
                    happened = true;
                }

                if (!happened) return false;
                _seen.Add(key);
                return Begin(session, id);
            }

            return false;
        }

        bool TrySeasonGoal(GameSession session)
        {
            ShuffleVisible();
            for (int i = 0; i < _visible.Count; i++)
            {
                if (!Accept(session, _visible[i])) continue;
                Player player;
                StaffMember staff;
                if (!session.TryClubPerson(_visible[i], out player, out staff) || player == null) continue;
                string id = player.Id;
                _line = Say(id, NameOf(player, staff), "season_goal", null, null);
                return Begin(session, id);
            }

            return false;
        }

        bool TryAppearance(GameSession session)
        {
            ShuffleVisible();
            for (int i = 0; i < _visible.Count; i++)
            {
                Player player;
                StaffMember staff;
                if (!session.TryClubPerson(_visible[i], out player, out staff) || player == null) continue;
                if (player.Appearances <= 0) continue;
                if (!Accept(session, player.Id)) continue;
                _line = Say(player.Id, NameOf(player, null), "appearance", player.Appearances.ToString(), null);
                return Begin(session, player.Id);
            }

            return false;
        }

        bool TryPersonal(GameSession session)
        {
            ShuffleVisible();
            for (int i = 0; i < _visible.Count; i++)
            {
                if (!Accept(session, _visible[i])) continue;
                Player player;
                StaffMember staff;
                session.TryClubPerson(_visible[i], out player, out staff);
                string id = player != null ? player.Id : (staff != null ? staff.Id : _visible[i]);
                string name = NameOf(player, staff);
                string kind;
                switch (UnityEngine.Random.Range(0, 5))
                {
                    case 0: kind = "favorite_food"; break;
                    case 1: kind = "disliked_food"; break;
                    case 2: kind = "hobby"; break;
                    case 3: kind = "dream_job"; break;
                    default: kind = "travel"; break;
                }

                _line = Say(id, name, kind, null, null);

                return Begin(session, id);
            }

            return false;
        }

        bool TryChatter(GameSession session)
        {
            var chatter = TalkCatalog.Chatter;
            if (chatter == null || chatter.Length == 0) return false;
            ShuffleVisible();
            for (int i = 0; i < _visible.Count; i++)
            {
                if (!Accept(session, _visible[i])) continue;
                Player player;
                StaffMember staff;
                session.TryClubPerson(_visible[i], out player, out staff);
                bool office = player == null;
                int index = -1;
                for (int n = 0; n < 16; n++)
                {
                    int pick = UnityEngine.Random.Range(0, chatter.Length);
                    if (office && TalkCatalog.IsSoccer(pick)) continue;
                    index = pick;
                    break;
                }

                if (index < 0) continue;
                string id = player != null ? player.Id : (staff != null ? staff.Id : _visible[i]);
                _line = Say(id, NameOf(player, staff), "chatter", null, chatter[index]);
                return Begin(session, id);
            }

            return false;
        }

        bool Accept(GameSession session, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (_play.Uses(id) && _fill != _play) return false;
            if (_next.Uses(id) && _fill != _next) return false;
            if (id == _lastSpeaker && _visible.Count > 1) return false;
            Player player;
            StaffMember staff;
            return session.TryClubPerson(id, out player, out staff);
        }

        bool Begin(GameSession session, string id)
        {
            if (_fill == null || _crowd == null || !_crowd.BeginStand(id, null)) return false;
            Player player;
            StaffMember staff;
            session.TryClubPerson(id, out player, out staff);
            string name = NameOf(player, staff);
            _fill.A = id;
            _fill.B = null;
            _fill.BeatCount = 1;
            _fill.Id[0] = id;
            _fill.Name[0] = name == "だれか" ? "" : name;
            _fill.Line[0] = _line;
            _fill.Armed = true;
            _lastSpeaker = id;
            _retry = 0f;
            return true;
        }

        void End()
        {
            _speakerId = null;
            _speakerName = null;
            _line = null;
            _beat = 0;
            _beatCount = 0;
            _talkFriend = false;
            _friendDone = false;
            _friendA = null;
            _friendB = null;
            _shown = 0f;
            _gathering = false;
            _gatherTime = 0f;
            _meetPause = -1f;
            _hasFocus = false;
        }

        void ShuffleVisible()
        {
            for (int i = _visible.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                string swap = _visible[i];
                _visible[i] = _visible[j];
                _visible[j] = swap;
            }
        }

        static string Say(string id, string name, string kind, string count, string line)
        {
            string country;
            string activity;
            CharacterLife.Travel(id, out country, out activity);
            var tokens = new Dictionary<string, string>
            {
                { "name", name ?? "" },
                { "count", count ?? "" },
                { "line", line ?? "" },
                { "food", CharacterLife.FavoriteFood(id) },
                { "dislike", CharacterLife.DislikedFood(id) },
                { "hobby", CharacterLife.Hobby(id) },
                { "job", CharacterLife.DreamJob(id) },
                { "country", country },
                { "activity", activity },
                { "goal", CharacterLife.SeasonGoal(id) }
            };
            return StripName(TalkCatalog.Fill(kind, tokens), name);
        }

        static string StripName(string line, string name)
        {
            if (string.IsNullOrEmpty(line)) return "";
            if (string.IsNullOrEmpty(name) || !line.StartsWith(name, StringComparison.Ordinal)) return line;
            return line.Substring(name.Length).TrimStart('、', ',', ' ', '　');
        }

        static bool Operated()
        {
            if (Input.anyKey) return true;
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2)) return true;
            var scroll = Input.mouseScrollDelta;
            return scroll.sqrMagnitude > 0.0001f;
        }

        void EnsureArt(Font font)
        {
            if (_bubble == null) _bubble = Capsule(Mathf.RoundToInt(BoxWidth), Mathf.RoundToInt(BubbleHeight));
            if (_namePlate == null) _namePlate = Capsule(Mathf.RoundToInt(NameWidth), Mathf.RoundToInt(NameHeight));
            if (_tailDown == null) _tailDown = Triangle(Mathf.RoundToInt(TailWidth), Mathf.RoundToInt(TailHeight), true);
            if (_tailUp == null) _tailUp = Triangle(Mathf.RoundToInt(TailWidth), Mathf.RoundToInt(TailHeight), false);
            if (_messageStyle != null && _font == font) return;
            _font = font;
            var pad = new RectOffset(0, 0, 0, 0);
            _messageStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 24,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                clipping = TextClipping.Clip,
                richText = false,
                padding = pad,
                margin = pad,
                normal = { textColor = Color.black }
            };
            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 20,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                richText = false,
                padding = pad,
                margin = pad,
                normal = { textColor = Color.black }
            };
        }

        static Texture2D Capsule(int width, int height)
        {
            var pixels = new Color32[width * height];
            float radius = height * 0.5f;
            float limit = radius * radius;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, width - radius);
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - radius;
                    bool on = dx * dx + dy * dy <= limit;
                    pixels[y * width + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            return Finish(pixels, width, height);
        }

        static Texture2D Triangle(int width, int height, bool pointDown)
        {
            var pixels = new Color32[width * height];
            float center = (width - 1) * 0.5f;
            float span = Mathf.Max(1, height - 1);
            for (int y = 0; y < height; y++)
            {
                // SetPixels は下が先頭なので、画面上で頂点が下に来るよう反転する。
                float along = pointDown ? 1f - y / span : y / span;
                float half = (1f - along) * center;
                for (int x = 0; x < width; x++)
                {
                    bool on = Mathf.Abs(x - center) <= half + 0.35f;
                    pixels[y * width + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            return Finish(pixels, width, height);
        }

        static Texture2D Finish(Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        static string NameOf(Player player, StaffMember staff)
        {
            if (player != null && !string.IsNullOrEmpty(player.Name)) return player.Name;
            if (staff != null && !string.IsNullOrEmpty(staff.Name)) return staff.Name;
            return "だれか";
        }
    }
}
