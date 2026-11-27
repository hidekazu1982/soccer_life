using System.Globalization;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Personnel;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// メイン画面でキャラ（選手・現場スタッフ・フロント）をタップしたときの詳細（版 1.61）。
    /// クラブハウスの一覧の左に出している詳細枠（NcDetailPanel）を、そのまま指定の位置へずらして描く。
    /// フロントの「交代」「解雇」のボタンは出さない（クラブハウスで操作する）。
    /// </summary>
    public sealed partial class ClubDialogView
    {
        const float MainDetailCloseSize = 60f;

        /// <summary>
        /// panelOrigin は詳細枠の左上（chrome 座標）。action に文言を渡すと、枠の下の段に横長のボタンを出す。
        /// close は右上の ✕、acted は action のボタンが押されたとき true。
        /// </summary>
        public void DrawMainDetail(GameSession session, Font font, Club club, Player player, StaffMember staff,
            Vector2 panelOrigin, string action, out bool close, out bool acted)
        {
            close = false;
            acted = false;
            if (session == null || (player == null && staff == null)) return;
            EnsureStyles(font);
            EnsureNcStyles();

            var previousColor = GUI.color;
            var previousMatrix = GUI.matrix;
            var offset = panelOrigin - NcDetailPanel.position;
            GUI.matrix = previousMatrix * Matrix4x4.Translate(new Vector3(offset.x, offset.y, 0f));
            GUI.color = Color.white;

            // 背景の地面に溶けないよう、枠の下に薄い影を敷く。
            NcRound(new Rect(NcDetailPanel.x + 4f, NcDetailPanel.y + 8f, NcDetailPanel.width, NcDetailPanel.height), NcShade(0.30f), 24f);

            if (player != null)
            {
                if (club != null) club.EnsureShirtNumbers();
                DrawNcDetail(session, club, player);
            }
            else if (staff.IsFrontOffice)
            {
                DrawMainFrontDetail(session, club, staff, action != null);
            }
            else
            {
                DrawNcStaffDetail(session, club, staff);
            }

            if (!string.IsNullOrEmpty(action))
            {
                var button = new Rect(NcFrontLeftButton.x, NcFrontLeftButton.y, NcFrontRightButton.xMax - NcFrontLeftButton.x, NcFrontLeftButton.height);
                if (NcButton(button, action, true, NcButtonKind.Primary))
                    acted = true;
            }

            // 右上の ✕（枠の角に半分かかる）。
            var closeRect = new Rect(NcDetailPanel.xMax - MainDetailCloseSize * 0.6f, NcDetailPanel.y - MainDetailCloseSize * 0.4f, MainDetailCloseSize, MainDetailCloseSize);
            NcRound(new Rect(closeRect.x, closeRect.y + 3f, closeRect.width, closeRect.height), NcShade(0.35f), MainDetailCloseSize * 0.5f);
            NcBox(closeRect, NcYellow, NcDarkBrown, 4f, MainDetailCloseSize * 0.5f);
            NcLabel(closeRect, "✕", _ncBold, NcDarkBrown);
            if (GUI.Button(closeRect, GUIContent.none, _hit))
                close = true;

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        /// <summary>フロントの詳細。クラブハウスの DrawNcFrontDetail から、交代・解雇のボタンを除いたもの。</summary>
        void DrawMainFrontDetail(GameSession session, Club club, StaffMember staff, bool hasAction)
        {
            DrawNcDetailPanel();
            DrawNcDetailCommon(
                staff.Id,
                NcFrontCode(staff.FrontRole),
                NcFrontRoleFill,
                Color.white,
                staff.Name,
                null,
                staff.Gender,
                staff.Age,
                NcStaffStand(staff, club),
                MoneyText.Format(staff.AnnualSalary));

            NcTopBox("LV", staff.Level.ToString(CultureInfo.InvariantCulture));
            NcTrait(491f, "部署人数", NcFrontCount(club, staff.FrontRole).ToString(CultureInfo.InvariantCulture) + "人");
            NcTrait(531f, "在籍", NcTenure(session, staff));
            NcTrait(571f, "上限", "Lv" + FrontLevelCap.Max().ToString(CultureInfo.InvariantCulture));
            NcEffectRow(667f, "効果", NcFrontEffect(staff.FrontRole, staff.Level));
            NcEffectRow(715f, "次LV", NcFrontNext(staff));
            // ボタンがなければ、関係とパーソナルデータを枠の下まで使う。
            DrawNcInfo(session, staff.Id, staff.Children, NcFrontInfoTop, hasAction ? NcFrontInfoBottom : 899f);
        }
    }
}
