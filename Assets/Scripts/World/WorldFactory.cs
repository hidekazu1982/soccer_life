using System;
using System.Collections.Generic;
using System.Globalization;
using SoccerClub.Core;
using SoccerClub.Data;

namespace SoccerClub.World
{
    /// <summary>20クラブと選手・スタッフを手続き生成する。</summary>
    public static class WorldFactory
    {
        public const string LeagueName = "ゆるふわアニマルリーグ";

        /// <summary>
        /// Resources/Master/ClubColors.csv の順。ユニフォームは 1st シャツ、2nd パンツ、3rd ソックスの RGB。
        /// 2nd と 3rd は省略でき、省略した色は 2nd←1st、3rd←2nd で埋める。背番号の文字色は白か黒。
        /// </summary>
        public readonly struct ClubIdentity
        {
            public readonly string Name;
            public readonly byte UniformR;
            public readonly byte UniformG;
            public readonly byte UniformB;
            public readonly int Color1st;
            public readonly int Color2nd;
            public readonly int Color3rd;
            public readonly bool NumberTextIsWhite;

            /// <summary>second と third は省略なら -1。</summary>
            public ClubIdentity(string name, int first, int second, int third, bool numberTextIsWhite)
            {
                Name = name;
                Color1st = first & 0xFFFFFF;
                Color2nd = second >= 0 ? second & 0xFFFFFF : Color1st;
                Color3rd = third >= 0 ? third & 0xFFFFFF : Color2nd;
                UniformR = (byte)((Color1st >> 16) & 0xFF);
                UniformG = (byte)((Color1st >> 8) & 0xFF);
                UniformB = (byte)(Color1st & 0xFF);
                NumberTextIsWhite = numberTextIsWhite;
            }
        }

        public static ClubIdentity[] Clubs => LoadClubs();

        public static readonly string[] ClubNames;

        static ClubIdentity[] _clubs;

        static WorldFactory()
        {
            var clubs = Clubs;
            ClubNames = new string[clubs.Length];
            for (int i = 0; i < clubs.Length; i++)
                ClubNames[i] = clubs[i].Name;
        }

        static ClubIdentity[] LoadClubs()
        {
            if (_clubs != null) return _clubs;
            _clubs = FitLeagueSize(ReadClubColors());
            return _clubs;
        }

        static ClubIdentity[] ReadClubColors()
        {
            // 列: name,color_1st,color_2nd,color_3rd,number。旧形式 name,uniform,number も読める。
            var rows = MasterCsv.Rows("ClubColors.csv");
            var list = new List<ClubIdentity>();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Length < 3) continue;
                string name = (row[0] ?? "").Trim();
                if (name.Length == 0 || name == "name") continue;
                int first;
                if (!TryHex(row[1], out first)) continue;
                int second = -1;
                int third = -1;
                string number;
                if (row.Length >= 5)
                {
                    if (!TryHex(row[2], out second)) second = -1;
                    if (!TryHex(row[3], out third)) third = -1;
                    number = row[4];
                }
                else
                {
                    number = row[2];
                }

                number = (number ?? "").Trim();
                bool white = number.Equals("white", StringComparison.OrdinalIgnoreCase) || number == "白";
                list.Add(new ClubIdentity(name, first, second, third, white));
            }

            return list.ToArray();
        }

        static bool TryHex(string text, out int rgb)
        {
            rgb = 0;
            if (string.IsNullOrEmpty(text)) return false;
            text = text.Trim();
            if (text.StartsWith("#")) text = text.Substring(1);
            if (text.Length != 6) return false;
            return int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb);
        }

        /// <summary>
        /// 色の正は ClubColors.csv だけ。行数が LeagueSize と違うときはエラーを出し、
        /// 足りない分は赤いダミー（クラブNN）で埋め、多い分は切り捨てて動作だけは続ける。
        /// </summary>
        static ClubIdentity[] FitLeagueSize(ClubIdentity[] loaded)
        {
            if (loaded == null) loaded = new ClubIdentity[0];
            if (loaded.Length == LeagueSize) return loaded;

            UnityEngine.Debug.LogError(
                "Resources/Master/ClubColors.csv のクラブ数が " + loaded.Length + " 件です。" + LeagueSize + " 件にしてください。");
            var fitted = new ClubIdentity[LeagueSize];
            for (int i = 0; i < LeagueSize; i++)
            {
                fitted[i] = i < loaded.Length
                    ? loaded[i]
                    : new ClubIdentity("クラブ" + (i + 1).ToString("00"), 0xFF1700, -1, -1, true);
            }

            return fitted;
        }

        static readonly Position[] FieldPositions =
        {
            Position.CB, Position.RSB, Position.LSB, Position.RWB, Position.LWB,
            Position.DM, Position.AM, Position.RSH, Position.LSH,
            Position.RW, Position.LW, Position.CF
        };

        public const int LeagueSize = 20;
        public const int FreeAgentCount = 3000;

        public static List<Club> CreateLeague(GameRandom random, int playerClubIndex, out Club playerClub)
        {
            if (playerClubIndex < 0 || playerClubIndex >= LeagueSize)
                playerClubIndex = 0;

            var clubs = new List<Club>(LeagueSize);
            for (int i = 0; i < LeagueSize; i++)
                clubs.Add(CreateClub(random, i, ClubNames[i], i == playerClubIndex));

            playerClub = clubs[playerClubIndex];
            return clubs;
        }

        public static Club CreateClub(GameRandom random, int index, string name, bool playerOwned)
        {
            var club = new Club
            {
                Id = "club-" + index.ToString("00"),
                Name = name,
                IsPlayerOwned = playerOwned,
                EmblemIndex = index + 1,
                NumberTextIsWhite = Clubs[index].NumberTextIsWhite,
                Coins = 40_000_000 + random.Next(0, 20_000_000),
                Reinforcement = 40_000_000,
                Advertising = 0,
                PreviousLeagueRank = index + 1
            };

            InitFacilities(club.Facilities, random);
            int capacity = ClubMath.Capacity(club);
            club.CoreSupporters = Math.Max(ClubMath.CoreFloor, ClubMath.RoundInt(capacity * 0.20d));
            club.LightSupporters = Math.Max(ClubMath.LightFloor, ClubMath.RoundInt(capacity * 0.65d));

            club.Front.Secretary = CreateAgedStaff(random, club.Id + "-sec", true, FrontRole.Secretary, TeamStaffRole.Manager);
            club.Front.GeneralManager = CreateGm(random, club.Id + "-gm", index == 0);
            club.Front.Strengthening[0] = CreateScout(random, club.Id + "-str0", PositionGroup.FW);
            club.Front.Strengthening[1] = CreateScout(random, club.Id + "-str1", PositionGroup.DF);
            FillPair(club.Front.Sales, random, club.Id + "-sales", FrontRole.Sales);
            FillPair(club.Front.PublicRelations, random, club.Id + "-pr", FrontRole.PublicRelations);
            FillPair(club.Front.Business, random, club.Id + "-biz", FrontRole.Business);
            FillPair(club.Front.Academy, random, club.Id + "-aca", FrontRole.Academy);
            club.TicketPrice = ClubMath.QuoteTicket(club);

            club.Team.Manager = CreateManager(random, club.Id + "-mgr");
            club.Team.HeadCoach = CreateAgedStaff(random, club.Id + "-hc", false, FrontRole.Secretary, TeamStaffRole.HeadCoach);
            club.Team.Coaches[0] = CreateCoach(random, club.Id + "-c0", PositionGroup.DF);
            club.Team.Coaches[1] = CreateCoach(random, club.Id + "-c1", PositionGroup.MF);
            club.Team.PhysicalCoach = CreateAgedStaff(random, club.Id + "-phy", false, FrontRole.Secretary, TeamStaffRole.PhysicalCoach);
            club.Team.Trainers[0] = CreateAgedStaff(random, club.Id + "-tr0", false, FrontRole.Secretary, TeamStaffRole.Trainer);
            club.Team.Trainers[1] = CreateAgedStaff(random, club.Id + "-tr1", false, FrontRole.Secretary, TeamStaffRole.Trainer);
            club.Team.Doctor = CreateAgedStaff(random, club.Id + "-doc", false, FrontRole.Secretary, TeamStaffRole.Doctor);

            club.Squad.Add(CreatePlayer(random, club.Id + "-gk0", 24 + random.Next(8), GrowthPhase.Prime, 1d, Position.GK));
            club.Squad.Add(CreatePlayer(random, club.Id + "-gk1", 21 + random.Next(6), GrowthPhase.Youth, 0.85d, Position.GK));
            club.Squad.Add(CreatePlayer(random, club.Id + "-gk2", 28 + random.Next(6), GrowthPhase.Prime, 0.9d, Position.GK));
            for (int i = 0; i < 22; i++)
            {
                var pos = FieldPositions[i % FieldPositions.Length];
                var phase = i % 5 == 0 ? GrowthPhase.Youth : (i % 7 == 0 ? GrowthPhase.Veteran : GrowthPhase.Prime);
                var age = phase == GrowthPhase.Youth ? 18 + random.Next(4) : (phase == GrowthPhase.Veteran ? 32 + random.Next(4) : 23 + random.Next(7));
                club.Squad.Add(CreatePlayer(random, club.Id + "-p" + i, age, phase, 1d, pos));
            }

            for (int i = 0; i < 4; i++)
            {
                int age = 15 + random.Next(3);
                club.Academy.Add(CreatePlayer(random, club.Id + "-ac" + i, age, GrowthPhase.Youth, 0.75d));
            }

            return club;
        }

        public static GrowthPhase PhaseForAge(int age)
        {
            if (age <= 21) return GrowthPhase.Youth;
            if (age <= 31) return GrowthPhase.Prime;
            return GrowthPhase.Veteran;
        }

        /// <summary>無所属。18歳から31歳まで。ベテランになった年に引退して18歳に戻る。</summary>
        public static void FillFreeAgents(GameRandom random, List<Player> dest, ref int serial)
        {
            if (dest == null) return;
            dest.Clear();
            serial = 0;
            int span = ClubMath.FreeAgentMaxAge - 18 + 1;
            for (int i = 0; i < FreeAgentCount; i++)
            {
                int age = 18 + (i % span);
                dest.Add(CreateFreeAgent(random, serial, age));
                serial++;
            }
        }

        public static Player CreateFreeAgent(GameRandom random, int serial, int age)
        {
            if (age < 18) age = 18;
            if (age > ClubMath.FreeAgentMaxAge) age = ClubMath.FreeAgentMaxAge;
            var position = serial % 14 == 0
                ? Position.GK
                : FieldPositions[Math.Abs(serial) % FieldPositions.Length];
            return CreatePlayer(random, "free-" + serial.ToString("D4"), age, PhaseForAge(age), 1d, position);
        }

        public static List<Player> CreateTransferMarket(GameRandom random, string prefix, int count)
        {
            var market = new List<Player>(count);
            var groups = new[] { PositionGroup.GK, PositionGroup.DF, PositionGroup.MF, PositionGroup.FW };
            for (int i = 0; i < count; i++)
            {
                var phase = (GrowthPhase)random.Next(3);
                int age = phase == GrowthPhase.Youth
                    ? 17 + random.Next(4)
                    : (phase == GrowthPhase.Veteran ? 32 + random.Next(5) : 22 + random.Next(8));
                var group = groups[i % groups.Length];
                var positions = PositionUtility.PositionsIn(group);
                var position = positions[random.Next(positions.Length)];
                var player = CreatePlayer(random, prefix + "-" + i, age, phase, 1d, position);
                double center = 80d + (1 + random.Next(80)) * 12d;
                ScaleOverall(player, center * (0.85d + random.NextDouble() * 0.3d));
                market.Add(player);
            }

            return market;
        }

        public static List<StaffMember> CreateTeamStaffMarket(GameRandom random, string prefix, int levelMin, int levelMax)
        {
            var roles = new[]
            {
                TeamStaffRole.Manager, TeamStaffRole.HeadCoach, TeamStaffRole.Coach,
                TeamStaffRole.PhysicalCoach, TeamStaffRole.Trainer, TeamStaffRole.Doctor
            };
            var market = new List<StaffMember>(roles.Length * 2);
            for (int r = 0; r < roles.Length; r++)
            {
                for (int n = 0; n < 2; n++)
                {
                    var staff = CreateMarketStaff(random, prefix + "-" + r + "-" + n, false, FrontRole.Secretary, roles[r], levelMin, levelMax);
                    if (roles[r] == TeamStaffRole.Manager)
                    {
                        staff.FavoriteFormation = (FormationType)random.Next(FormationCatalog.All.Length);
                        staff.FavoriteStyle = (PlayingStyle)random.Next(5);
                        staff.LineupPriority = (LineupPriority)random.Next(2);
                    }

                    if (roles[r] == TeamStaffRole.Coach)
                        staff.ScoutedGroup = (PositionGroup)random.Next(4);
                    market.Add(staff);
                }
            }

            return market;
        }

        public static void ScaleOverall(Player player, double target)
        {
            if (player == null) return;
            double current = player.Overall;
            if (current <= 0d || target <= 0d) return;
            double scale = target / current;
            player.Offense = Math.Max(1d, Math.Round(player.Offense * scale, 1));
            player.Defense = Math.Max(1d, Math.Round(player.Defense * scale, 1));
            player.StaminaMax = Math.Max(1d, Math.Round(player.StaminaMax * scale, 1));
            player.StaminaCurrent = player.StaminaMax;
        }

        static void InitFacilities(ClubFacilities facilities, GameRandom random)
        {
            int lv = 8 + random.Next(12);
            facilities.Stadium.BaseCost = 40_000_000;
            facilities.Stadium.Level = 1;
            facilities.Transport.BaseCost = 400_000;
            facilities.Transport.Level = Math.Max(1, lv - 2);
            facilities.TrainingGround.BaseCost = 500_000;
            facilities.TrainingGround.Level = 10;
            facilities.TrainingFacility.BaseCost = 500_000;
            facilities.TrainingFacility.Level = 10;
            facilities.Clubhouse.BaseCost = 600_000;
            facilities.Clubhouse.Level = 10;
        }

        public static Player CreatePlayer(GameRandom random, string id, int age, GrowthPhase phase, double scale, Position? forced = null)
        {
            var position = forced ?? FieldPositions[random.Next(FieldPositions.Length)];
            var offense = (40d + random.NextDouble() * 40d) * scale;
            var defense = (40d + random.NextDouble() * 40d) * scale;
            if (position == Position.GK)
            {
                offense *= 0.35d;
                defense *= 1.25d;
            }

            var player = new Player
            {
                Id = id,
                Name = NameCatalog.ForId(id),
                Gender = PlayerTraits.GenderForId(id),
                Age = age,
                Phase = phase,
                Rarity = RollRarity(random),
                Style = (PlayingStyle)random.Next(5),
                PreferredPosition = position,
                Offense = Math.Max(1d, Math.Round(offense, 1)),
                Defense = Math.Max(1d, Math.Round(defense, 1)),
                StaminaMax = Math.Max(1d, Math.Round(70d + random.NextDouble() * 30d, 1)),
                Condition = 80
            };
            player.StaminaCurrent = player.StaminaMax;
            FillSuitabilities(player, position, random);
            return player;
        }

        /// <summary>得意は ◎。同グループの ○ は 0〜3 個。フィールドの残りは △。</summary>
        static void FillSuitabilities(Player player, Position preferred, GameRandom random)
        {
            for (int i = 0; i < player.Suitabilities.Length; i++)
                player.Suitabilities[i] = Suitability.None;

            player.SetSuitability(preferred, Suitability.Excellent);
            if (preferred == Position.GK) return;

            var groupPositions = PositionUtility.PositionsIn(PositionUtility.GroupOf(preferred));
            int siblingCount = 0;
            for (int i = 0; i < groupPositions.Length; i++)
            {
                if (groupPositions[i] != preferred) siblingCount++;
            }

            if (siblingCount > 0)
            {
                int grant = random.Next(0, 4);
                if (grant > siblingCount) grant = siblingCount;

                var pool = new Position[siblingCount];
                int n = 0;
                for (int i = 0; i < groupPositions.Length; i++)
                {
                    if (groupPositions[i] != preferred) pool[n++] = groupPositions[i];
                }

                for (int i = 0; i < grant; i++)
                {
                    int pick = i + random.Next(siblingCount - i);
                    var chosen = pool[pick];
                    pool[pick] = pool[i];
                    pool[i] = chosen;
                    player.SetSuitability(chosen, Suitability.Good);
                }
            }

            foreach (Position pos in Enum.GetValues(typeof(Position)))
            {
                if (player.SuitabilityAt(pos) == Suitability.None)
                    player.SetSuitability(pos, Suitability.Fair);
            }
        }

        public static StaffMember CreateFrontCandidate(GameRandom random, string id, FrontRole role, int levelMin, int levelMax)
        {
            var staff = CreateMarketStaff(random, id, true, role, TeamStaffRole.Manager, levelMin, levelMax);
            staff.Name = NameCatalog.ForId(id);
            if (role == FrontRole.GeneralManager)
                staff.Policy = (ReinforcementPolicy)random.Next(3);
            if (role == FrontRole.Strengthening)
                staff.ScoutedGroup = (PositionGroup)random.Next(4);
            return staff;
        }

        static Rarity RollRarity(GameRandom random)
        {
            int roll = random.Next(100);
            if (roll < 3) return Rarity.Legendary;
            if (roll < 12) return Rarity.Epic;
            if (roll < 40) return Rarity.Rare;
            return Rarity.Common;
        }

        static StaffMember CreateGm(GameRandom random, string id, bool aoi)
        {
            var staff = CreateAgedStaff(random, id, true, FrontRole.GeneralManager, TeamStaffRole.Manager);
            if (aoi)
            {
                staff.Age = 40;
                staff.Level = 10;
                staff.Policy = ReinforcementPolicy.Strength;
            }
            else
            {
                staff.Policy = (ReinforcementPolicy)random.Next(3);
            }

            return staff;
        }

        static StaffMember CreateScout(GameRandom random, string id, PositionGroup group)
        {
            var staff = CreateAgedStaff(random, id, true, FrontRole.Strengthening, TeamStaffRole.Manager);
            staff.ScoutedGroup = group;
            return staff;
        }

        static StaffMember CreateCoach(GameRandom random, string id, PositionGroup group)
        {
            var staff = CreateAgedStaff(random, id, false, FrontRole.Secretary, TeamStaffRole.Coach);
            staff.ScoutedGroup = group;
            return staff;
        }

        static StaffMember CreateManager(GameRandom random, string id)
        {
            var staff = CreateAgedStaff(random, id, false, FrontRole.Secretary, TeamStaffRole.Manager);
            staff.FavoriteFormation = (FormationType)random.Next(FormationCatalog.All.Length);
            staff.FavoriteStyle = (PlayingStyle)random.Next(5);
            staff.LineupPriority = (LineupPriority)random.Next(2);
            return staff;
        }

        static void FillPair(StaffMember[] slots, GameRandom random, string idPrefix, FrontRole role)
        {
            for (int i = 0; i < slots.Length; i++)
                slots[i] = CreateAgedStaff(random, idPrefix + i, true, role, TeamStaffRole.Manager);
        }

        static StaffMember CreateAgedStaff(GameRandom random, string id, bool front, FrontRole frontRole, TeamStaffRole teamRole)
        {
            int age = 32 + random.Next(24);
            int level = Math.Min(10, Math.Max(8, (age - 25) * 2));
            return new StaffMember
            {
                Id = id,
                Name = NameCatalog.ForId(id),
                Age = age,
                Level = level,
                IsFrontOffice = front,
                FrontRole = frontRole,
                TeamRole = teamRole
            };
        }

        static StaffMember CreateMarketStaff(GameRandom random, string id, bool front, FrontRole frontRole, TeamStaffRole teamRole, int levelMin, int levelMax)
        {
            int max = Math.Max(1, Math.Min(ClubMath.MaxStaffLevel, levelMax));
            int min = Math.Max(1, Math.Min(max, levelMin));
            return new StaffMember
            {
                Id = id,
                Name = NameCatalog.ForId(id),
                Age = 26 + random.Next(39),
                Level = min + random.Next(max - min + 1),
                IsFrontOffice = front,
                FrontRole = frontRole,
                TeamRole = teamRole
            };
        }
    }
}
