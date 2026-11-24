using System;
using System.Globalization;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Personnel;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 新しいクラブハウスのフロントタブ（Figma のフロントリスト 214:2 / フロント詳細 215:2）。
    /// 一覧は 12 枠（秘書、GM、強化 2、営業 2、広報 2、事業 2、育成 2）。空き枠をタップすると採用候補を出す。
    /// 詳細の下に「交代」「解雇」、候補の一覧では「戻る」「契約」を出す。契約は今までの確認パネルを使う。
    /// 一覧と候補の作り方、契約と解雇の処理は今までのフロント画面（ClubDialogView.cs）と同じ。
    /// </summary>
    public sealed partial class ClubDialogView
    {
        static readonly string[] NcFrontHeadLabels = { "POS", "M/F", "名前", "AGE", "LV", "年俸", "" };

        // 詳細の下のボタン（859〜899）。パーソナルデータは 763〜851 に詰める。
        static readonly Rect NcFrontLeftButton = new Rect(236f, 859f, 176f, 40f);
        static readonly Rect NcFrontRightButton = new Rect(421f, 859f, 176f, 40f);
        const float NcFrontInfoTop = 763f;
        const float NcFrontInfoBottom = 851f;
        const float NcEffectPillWidth = 72f;

        void DrawNcFront(GameSession session, Club club, Action<string> setStatus)
        {
            BuildFrontList(session, club);

            for (int i = 0; i < NcHeadX.Length; i++)
            {
                string label = NcFrontHeadLabels[i];
                if (i == 2 && _frontMarket)
                    label = NcFrontCode(FrontOffice.RoleAt(_frontTargetSlot)) + "の候補";
                DrawNcHead(i, label, false);
            }

            float content = NcContent(_frontListed.Count);
            if (!_frontConfirm)
            {
                int clicked = UpdateNcScroll(content, _frontListed.Count, ref _frontScroll);
                if (clicked >= 0 && clicked < _frontListed.Count)
                {
                    var picked = _frontListed[clicked];
                    if (picked != null)
                        _frontSelectedId = picked.Id;
                    else if (!_frontMarket)
                        OpenFrontMarket(clicked);
                    BuildFrontList(session, club);
                    content = NcContent(_frontListed.Count);
                }
            }

            if (_frontListed.Count == 0)
            {
                GUI.Label(NcRows, _frontMarket ? "採用できる候補はいません。" : "フロントはいません。", _ncCell);
            }
            else
            {
                GUI.BeginGroup(NcRows);
                for (int i = 0; i < _frontListed.Count; i++)
                {
                    float y = i * NcRowStride - _frontScroll;
                    if (y + NcRowHeight < 0f || y > NcRows.height) continue;
                    DrawNcFrontRow(i, _frontListed[i], y);
                }

                GUI.EndGroup();
            }

            DrawNcScrollbar(content, _frontScroll);
            DrawNcFrontDetail(session, club, ResolveFrontSelection(), setStatus);
        }

        /// <summary>BeginGroup の中。index は一覧の番号（自クラブの一覧では枠の番号と同じ）。</summary>
        void DrawNcFrontRow(int index, StaffMember staff, float y)
        {
            if (staff == null)
            {
                NcFill(new Rect(0f, y, NcListWidth, NcRowHeight), NcGray5);
                NcFill(new Rect(0f, y, 100f, 100f), NcGray4);
                FitNcLabel(new Rect(0f, y, 100f, NcRowHeight), NcFrontCode(FrontOffice.RoleAt(index)), _ncCell, 36);
                GUI.Label(new Rect(177f, y, 600f, NcRowHeight), "（空き）タップで採用", _ncCellLeft);
                return;
            }

            DrawNcRowBase(y, staff.Id == _frontSelectedId, NcFrontCode(staff.FrontRole), staff.Gender, staff.Name, staff.Age);
            FitNcLabel(new Rect(1208f - NcListX, y, 124f, NcRowHeight), staff.Level.ToString(CultureInfo.InvariantCulture), _ncCell, 36);
            FitNcLabel(new Rect(1340f - NcListX, y, 288f, NcRowHeight), MoneyText.Format(staff.AnnualSalary), _ncCellRight, 36);
        }

        void DrawNcFrontDetail(GameSession session, Club club, StaffMember staff, Action<string> setStatus)
        {
            NcFill(NcDetailPanel, NcGray5);
            if (staff == null)
            {
                if (_frontMarket && NcButton(NcFrontLeftButton, "戻る", !_frontConfirm))
                    LeaveFrontMarket(club);
                return;
            }

            DrawNcDetailCommon(
                staff.Id,
                NcFrontCode(staff.FrontRole),
                staff.Name,
                null,
                staff.Gender,
                staff.Age,
                NcStaffStand(staff, club),
                MoneyText.Format(staff.AnnualSalary));

            // LV と、部署人数・在籍・上限。
            NcFill(new Rect(NcBoxX, 451f, NcBoxWidth, 40f), NcGray4);
            GUI.Label(new Rect(NcBoxX, 451f, NcBoxWidth, 40f), "LV " + staff.Level.ToString(CultureInfo.InvariantCulture), _ncOverall);
            NcTrait(491f, "部署人数", NcFrontCount(club, staff.FrontRole).ToString(CultureInfo.InvariantCulture) + "人");
            NcTrait(531f, "在籍", _frontMarket ? "候補" : NcTenure(session, staff));
            NcTrait(571f, "上限", "Lv" + FrontLevelCap.Max().ToString(CultureInfo.InvariantCulture));

            // 部署の効果。候補のときは次LVの代わりに契約金。
            NcEffectRow(667f, "効果", NcFrontEffect(staff.FrontRole, staff.Level));
            if (_frontMarket)
                NcEffectRow(715f, "契約金", MoneyText.Format(FrontOfficeHiring.SigningFee(staff)));
            else
                NcEffectRow(715f, "次LV", NcFrontNext(staff));

            DrawNcInfo(session, staff.Id, staff.Children, NcFrontInfoTop, NcFrontInfoBottom);

            bool active = !_frontConfirm;
            if (_frontMarket)
            {
                if (NcButton(NcFrontLeftButton, "戻る", active))
                {
                    LeaveFrontMarket(club);
                    return;
                }

                if (NcButton(NcFrontRightButton, "契約", active))
                {
                    _frontConfirm = true;
                    _frontConfirmId = staff.Id;
                }

                return;
            }

            int slot = FrontSlotOf(staff);
            if (NcButton(NcFrontLeftButton, "交代", active && slot >= 0))
            {
                OpenFrontMarket(slot);
                return;
            }

            bool canDismiss = slot >= 0 && session.CanDismissFront(slot);
            if (NcButton(NcFrontRightButton, "解雇", active && slot >= 0))
            {
                if (!canDismiss)
                    setStatus("部署の最後の1人なので解雇できません。交代は市場から契約してください。");
                else if (session.OwnerDismissesFront(slot))
                {
                    setStatus(staff.Name + "を無償で解雇しました。");
                    if (_frontSelectedId == staff.Id) _frontSelectedId = null;
                }
            }
        }

        /// <summary>左に 72 幅の札、右に文言（効果・次LV・契約金）。</summary>
        void NcEffectRow(float y, string label, string value)
        {
            NcFill(new Rect(NcDetailX, y, NcDetailWidth, 40f), NcGray4);
            NcPill(new Rect(NcPillX, y + 4f, NcEffectPillWidth, 32f), label);
            FitNcLabel(new Rect(NcPillX + NcEffectPillWidth + 12f, y, NcRightX - (NcPillX + NcEffectPillWidth + 12f), 40f), value ?? "", _ncSmall, 20);
        }

        /// <summary>灰色の四角のボタン。押せないときは薄くして、押されても false。</summary>
        bool NcButton(Rect rect, string label, bool enabled)
        {
            NcFill(rect, enabled ? NcGray3 : NcGray5);
            var previous = _ncSmallCenter.normal.textColor;
            if (!enabled) _ncSmallCenter.normal.textColor = NcGray;
            GUI.Label(rect, label, _ncSmallCenter);
            _ncSmallCenter.normal.textColor = previous;
            return enabled && GUI.Button(rect, GUIContent.none, _hit);
        }

        /// <summary>POS の欄の略号。</summary>
        static string NcFrontCode(FrontRole role)
        {
            switch (role)
            {
                case FrontRole.Secretary: return "秘書";
                case FrontRole.GeneralManager: return "GM";
                case FrontRole.Strengthening: return "強化";
                case FrontRole.Sales: return "営業";
                case FrontRole.PublicRelations: return "広報";
                case FrontRole.Business: return "事業";
                default: return "育成";
            }
        }

        /// <summary>そのレベルの人が部署で持つ効果（1 人分の目安）。</summary>
        static string NcFrontEffect(FrontRole role, int level)
        {
            string lv = level.ToString(CultureInfo.InvariantCulture);
            switch (role)
            {
                case FrontRole.Secretary:
                    return "提案の代わり（不在時）";
                case FrontRole.GeneralManager:
                    return "移籍への関与 " + lv + "%";
                case FrontRole.Strengthening:
                    return "補強の狙い 施設Lv" + lv + "相当";
                case FrontRole.Sales:
                    return "スポンサーの質 Lv" + lv;
                case FrontRole.PublicRelations:
                    return "広告の集客 +" + ClubMath.AdvertisingFansAtLevel(level).ToString("N0", CultureInfo.InvariantCulture) + "人/試合";
                case FrontRole.Business:
                    return "施設の上限 Lv" + lv;
                default:
                    return "育成 Lv" + lv;
            }
        }

        /// <summary>次LVの欄。上限の人は「上限（リーグLVで解放）」。</summary>
        static string NcFrontNext(StaffMember staff)
        {
            if (!FrontLevelCap.CanRaise(staff)) return "上限（リーグLVで解放）";
            if (staff.FrontRole == FrontRole.Secretary) return "—";
            return NcFrontEffect(staff.FrontRole, staff.Level + 1);
        }

        /// <summary>自クラブのその部署の人数。</summary>
        static int NcFrontCount(Club club, FrontRole role)
        {
            var front = club != null ? club.Front : null;
            if (front == null) return 0;
            int count = 0;
            for (int i = 0; i < FrontOffice.SlotCount; i++)
            {
                if (FrontOffice.RoleAt(i) == role && front.GetAt(i) != null) count++;
            }

            return count;
        }

        /// <summary>在籍年数。1 年未満は月で出す。</summary>
        static string NcTenure(GameSession session, StaffMember staff)
        {
            int days = session != null ? session.DaysEmployed(staff) : 0;
            if (days >= 365) return (days / 365).ToString(CultureInfo.InvariantCulture) + "年";
            return (days / 30).ToString(CultureInfo.InvariantCulture) + "か月";
        }
    }
}
