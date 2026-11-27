using System.Collections.Generic;
using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 選手詳細の「ひとこと」（版 1.70）。関係・趣味などの一覧の先頭に、本人の台詞を吹き出しで出す。
    /// 台詞は PlayerVoiceCatalog（Resources/Master/PlayerVoice.csv）から、その日の状況に合うものを 1 つ選ぶ。
    /// </summary>
    public sealed partial class ClubDialogView
    {
        /// <summary>吹き出し（しっぽを含む）が一覧で使う高さ。</summary>
        const float NcVoiceHeight = 104f;
        const float NcVoiceTail = 14f;

        string _ncVoiceKey;
        string _ncVoice;
        GUIStyle _ncVoiceText;

        /// <summary>選手のひとこと。選手と日付が変わったときだけ選び直す。</summary>
        string NcVoiceFor(GameSession session, Player player)
        {
            if (session == null || player == null) return null;
            var today = session.Time.CurrentDate.Date;
            string key = player.Id + "|" + today.Ticks;
            if (_ncVoiceKey == key) return _ncVoice;
            _ncVoiceKey = key;

            int month;
            int day;
            CharacterLife.Birthday(player.Id, out month, out day);
            bool longBench = player.SeasonRatedMatches <= 2
                             && (player.LastPlayedDate == default || (today - player.LastPlayedDate.Date).TotalDays >= 30);
            var context = new VoiceContext
            {
                Id = player.Id,
                Age = player.Age,
                Condition = player.Condition,
                InjuryDays = player.InjuryDays,
                SeasonMatches = player.SeasonRatedMatches,
                SeasonGoals = player.SeasonGoals,
                Children = player.Children,
                LongBench = longBench,
                Birthday = today.Month == month && today.Day == day,
                DayKey = today.Ticks
            };

            var relations = session.Relations != null ? session.Relations.For(player.Id) : null;
            if (relations != null && relations.Count > 0)
            {
                var people = ProfilePeople(session);
                Relationship partner = null;
                Relationship best = null;
                Relationship friend = null;
                for (int i = 0; i < relations.Count; i++)
                {
                    var rel = relations[i];
                    switch (rel.Stage)
                    {
                        case RelationStage.Married:
                        case RelationStage.Lover:
                            if (partner == null || rel.Stage == RelationStage.Married) partner = rel;
                            break;
                        case RelationStage.BestFriend:
                            if (best == null || rel.Affinity > best.Affinity) best = rel;
                            break;
                        case RelationStage.Friend:
                            if (friend == null || rel.Affinity > friend.Affinity) friend = rel;
                            break;
                    }
                }

                context.PartnerName = NameOf(people, partner, player.Id);
                context.PartnerMarried = partner != null && partner.Stage == RelationStage.Married;
                context.BestFriendName = NameOf(people, best, player.Id);
                context.FriendName = NameOf(people, friend, player.Id);
            }

            _ncVoice = PlayerVoiceCatalog.Pick(context);
            return _ncVoice;
        }

        static string NameOf(Dictionary<string, ProfilePerson> people, Relationship rel, string selfId)
        {
            if (rel == null) return null;
            string other = rel.Other(selfId);
            ProfilePerson person;
            return other != null && people.TryGetValue(other, out person) && !string.IsNullOrEmpty(person.Name) ? person.Name : null;
        }

        /// <summary>白い吹き出し。上辺の左寄りに、顔のほうを向いた小さなしっぽを付ける。y は一覧の中の上端。</summary>
        void DrawNcVoice(float y, string text)
        {
            if (_ncVoiceText == null || _ncVoiceText.font != _ncSmall.font)
            {
                _ncVoiceText = new GUIStyle(_ncSmall)
                {
                    fontSize = 22,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(18, 18, 6, 6),
                    normal = { textColor = NcText }
                };
            }

            var box = new Rect(0f, y + NcVoiceTail, NcDetailWidth, NcVoiceHeight - NcVoiceTail - 8f);
            // しっぽ：45 度回した小さな四角を、箱の上辺から半分だけのぞかせる（顔の真下あたり）。
            var tail = new Rect(64f, box.y - 9f, 18f, 18f);
            var previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, tail.center);
            NcBox(tail, Color.white, NcBrown, 2f, 3f);
            GUI.matrix = previous;
            NcBox(box, Color.white, NcBrown, 2f, 16f);
            // しっぽと箱の境目の線を白で消す。
            NcRound(new Rect(tail.x + 1f, box.y + 1f, tail.width - 2f, 4f), Color.white, 0f);
            GUI.Label(box, "「" + text + "」", _ncVoiceText);
        }
    }
}
