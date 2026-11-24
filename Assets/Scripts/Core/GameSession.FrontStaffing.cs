using SoccerClub.Data;
using SoccerClub.World;

namespace SoccerClub.Core
{
    /// <summary>
    /// フロントは各部署（秘書・GM・強化部・営業部・広報部・事業部・アカデミー）に最低 1 人いる。
    /// ・部署の最後の 1 人は解雇できない（交代は市場からの契約で行う）。
    /// ・引退で部署が空いたら、市場の下限レベルの人をすぐに補充する（契約金なし。年俸はレベルどおり）。
    /// ・古いセーブで空いている部署も、読み込み時に同じように補充する。
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>この枠の人を解雇できるか。部署にほかの人がいるときだけ解雇できる。</summary>
        public bool CanDismissFront(int slot)
        {
            var front = PlayerClub != null ? PlayerClub.Front : null;
            if (front == null || front.GetAt(slot) == null) return false;
            var role = FrontOffice.RoleAt(slot);
            for (int i = 0; i < FrontOffice.SlotCount; i++)
            {
                if (i == slot || FrontOffice.RoleAt(i) != role) continue;
                if (front.GetAt(i) != null) return true;
            }

            return false;
        }

        void EnsureAllFrontStaffed()
        {
            for (int i = 0; i < Clubs.Count; i++)
                EnsureFrontStaffed(Clubs[i]);
        }

        /// <summary>空いている部署に 1 人ずつ補充する。補充した人数を返す。</summary>
        int EnsureFrontStaffed(Club club)
        {
            if (club == null) return 0;
            if (club.Front == null) club.Front = new FrontOffice();
            var front = club.Front;
            int floor = FrontLevelCap.MarketFloor(club);
            int added = 0;
            for (int slot = 0; slot < FrontOffice.SlotCount; slot++)
            {
                var role = FrontOffice.RoleAt(slot);
                // 部署の最初の枠でだけ判定する。
                if (slot > 0 && FrontOffice.RoleAt(slot - 1) == role) continue;
                if (HasFrontRole(front, role)) continue;
                var staff = WorldFactory.CreateFrontCandidate(
                    _random,
                    "front-auto-" + club.Id + "-" + Time.CurrentDate.Ticks + "-" + slot,
                    role,
                    floor,
                    floor);
                staff.JoinedTicks = Time.CurrentDate.Date.Ticks;
                front.SetAt(slot, staff);
                added++;
            }

            if (added > 0) _supportDirty = true;
            return added;
        }

        static bool HasFrontRole(FrontOffice front, FrontRole role)
        {
            for (int i = 0; i < FrontOffice.SlotCount; i++)
            {
                if (FrontOffice.RoleAt(i) == role && front.GetAt(i) != null) return true;
            }

            return false;
        }
    }
}
