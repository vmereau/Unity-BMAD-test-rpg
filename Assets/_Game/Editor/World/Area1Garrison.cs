using TMPro;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// The graybox quarter under the fort ramp, on one spine off the fort road. Guards sleep along the palisade by the ramp gate, the
    /// staff house fronts the road, and the alehouse and grain store sit behind it. Nothing here has gameplay: the armoury and grain
    /// store are closed rooms for later theft quests. <see cref="Area1SitesBuilder"/> owns the root and validates the layout first.
    /// </summary>
    internal static class Area1Garrison
    {
        /// <summary>A sized, turned footprint from the layout.</summary>
        internal readonly struct Plot
        {
            public readonly Vector2 Center;
            public readonly float Width;
            public readonly float Depth;
            public readonly float Yaw;

            public Plot(Vector2 center, float width, float depth, float yaw)
            {
                Center = center;
                Width = width;
                Depth = depth;
                Yaw = yaw;
            }
        }

        private const float BURY = 0.3f;
        private const float WALL = 0.4f;
        private const float WALL_HEIGHT = 3f;
        private const float HOUSE_PITCH = 30f;       // the shared shell's roof
        private const float DOOR_RAMP = 1.5f;        // these houses face each other over the 3 m spine, so a ramp stops at its middle
        private const int GABLE_TIERS = 4;

        private const float BARRACKS_DOOR_X = -5.5f; // opens on the yard, clear of the staff house
        private const float ARMOURY_LENGTH = 4f;
        private const float CLADDING = 0.2f;
        private const int BUNKS = 6;

        private const float STAFF_DOOR_X = -2f;
        private const float LEDGER_ROOM_EAST = -3.2f;
        private const float OFFICE_WEST = 2f;

        private const float STILT = 1.1f;
        private const float STILT_WIDTH = 0.3f;
        private const float GRAIN_FLOOR = 1.35f;
        private const float GRAIN_WALL = 2f;
        private const float GRAIN_PITCH = 25f;

        private const int PELLS = 5;
        private const float PELL_SPACING = 1.6f;
        private const float PELL_ROW_X = 0.8f;       // east of the path to the graves
        private const float PELL_ROW_Z = -2f;
        private const float BUTT_OFFSET = 7.2f;      // past the yard's west edge, with the bank behind
        private const float BUTT_ROW_Z = 0.4f;       // clear of the graves
        private const float BUTT_SPACING = 1.6f;

        private const int GRAVE_ROWS = 3;
        private const int GRAVES_PER_ROW = 4;
        private const float GRAVE_LENGTH = 1.6f;
        private const int FIRST_NUMBER = 214;

        /// <summary>The door opens on the yard. The east end is the armoury: stone-faced, with a solid door and no way in.</summary>
        internal static void BuildBarracks(Plot plot, Terrain terrain, Material timber, Material stone, Transform root)
        {
            var barracks = new GameObject("Barracks").transform;
            barracks.SetParent(root, false);
            Area1CampInterior.BuildHouseShell(plot.Center, plot.Width, plot.Depth, Quaternion.Euler(0f, plot.Yaw, 0f), "West", terrain, timber, barracks, BARRACKS_DOOR_X, DOOR_RAMP);

            float halfWidth = plot.Width * 0.5f, halfDepth = plot.Depth * 0.5f;
            float armouryWest = halfWidth - ARMOURY_LENGTH;
            float armouryMiddle = armouryWest + ARMOURY_LENGTH * 0.5f;
            Partition("Armoury_Wall", barracks, stone, armouryWest, plot.Depth);
            foreach (float side in new[] { -1f, 1f })
                Area1RemoteSites.Local(side < 0f ? "Armoury_Back" : "Armoury_Front", barracks, stone,
                    new Vector3(armouryMiddle, (WALL_HEIGHT - BURY) * 0.5f, side * (halfDepth + CLADDING * 0.5f)), new Vector3(ARMOURY_LENGTH + CLADDING * 2f, WALL_HEIGHT + BURY, CLADDING));
            Area1RemoteSites.Local("Armoury_End", barracks, stone, new Vector3(halfWidth + CLADDING * 0.5f, (WALL_HEIGHT - BURY) * 0.5f, 0f), new Vector3(CLADDING, WALL_HEIGHT + BURY, plot.Depth));
            Area1RemoteSites.Thin("Armoury_Door", barracks, timber, new Vector3(armouryMiddle, 1.15f, halfDepth + CLADDING + 0.03f), new Vector3(1.3f, 2.3f, 0.05f));
            Area1RemoteSites.Thin("Armoury_Bar", barracks, stone, new Vector3(armouryMiddle, 1.25f, halfDepth + CLADDING + 0.08f), new Vector3(1.6f, 0.12f, 0.06f));

            for (int i = 0; i < BUNKS; i++)
                Area1RemoteSites.Local($"Bunk_{i}", barracks, timber, new Vector3(-halfWidth + 1.2f + i * 1.6f, 0.35f, -halfDepth + 1.2f), new Vector3(0.9f, 0.7f, 1.9f));
            Area1RemoteSites.Local("Table", barracks, timber, new Vector3(0.5f, 0.4f, 1.2f), new Vector3(2.6f, 0.8f, 0.9f));
            Area1RemoteSites.Local("WeaponRack", barracks, timber, new Vector3(-1f, 0.3f, halfDepth + 0.2f), new Vector3(2.4f, 1.4f, 0.3f)); // low, so it's no step onto the roof; it stands on the ground below the floor
        }

        /// <summary>A hall behind the door, the ledger room to the west, and the pay office to the east with its hatch on the road.</summary>
        internal static void BuildStaffHouse(Plot plot, Terrain terrain, Material timber, Material stone, Transform root)
        {
            var house = new GameObject("StaffHouse").transform;
            house.SetParent(root, false);
            Area1CampInterior.BuildHouseShell(plot.Center, plot.Width, plot.Depth, Quaternion.Euler(0f, plot.Yaw, 0f), "West", terrain, timber, house, STAFF_DOOR_X, DOOR_RAMP);

            float halfWidth = plot.Width * 0.5f, halfDepth = plot.Depth * 0.5f;
            Area1Timberworks.DoorWall("Wall_LedgerRoom", house, timber, LEDGER_ROOM_EAST, 0f, halfDepth);
            Area1Timberworks.DoorWall("Wall_Office", house, timber, OFFICE_WEST, 0f, halfDepth);

            Area1RemoteSites.Local("Shelf_Ledgers", house, timber, new Vector3(-4.6f, 1.1f, -halfDepth + 0.6f), new Vector3(1.8f, 2.2f, 0.4f));
            Area1RemoteSites.Local("LedgerDesk", house, timber, new Vector3(-5.1f, 0.4f, 1.6f), new Vector3(0.8f, 0.8f, 1f));
            Area1RemoteSites.Local("LedgerChest", house, stone, new Vector3(-5.2f, 0.3f, -1.1f), new Vector3(0.6f, 0.6f, 0.8f));
            Area1RemoteSites.Local("HallTable", house, timber, new Vector3(-1f, 0.4f, -1f), new Vector3(2.4f, 0.8f, 0.9f));
            Area1RemoteSites.Local("PayDesk", house, timber, new Vector3(halfWidth - 1f, 0.4f, 0f), new Vector3(0.8f, 0.8f, 2f));
            Area1RemoteSites.Local("Strongbox", house, stone, new Vector3(3.2f, 0.4f, -1.9f), new Vector3(1f, 0.8f, 0.7f));

            Area1RemoteSites.Thin("PayCounter", house, timber, new Vector3(halfWidth + 0.2f, 0.6f, 0f), new Vector3(0.45f, 0.1f, 1.8f)); // at worker height: the ground here is below the floor
            Area1RemoteSites.Thin("PayHatch", house, timber, new Vector3(halfWidth + 0.3f, 1.4f, 0f), new Vector3(0.8f, 0.08f, 1.8f));

            Vector3 railOffset = Quaternion.Euler(0f, plot.Yaw, 0f) * new Vector3(halfWidth + 1.3f, 0f, -1.6f);
            Transform rail = Area1Stoneworks.Group("QueueRail", root, plot.Center + new Vector2(railOffset.x, railOffset.z), plot.Yaw, terrain);
            foreach (float z in new[] { -1.5f, 0f, 1.5f })
                Post($"Post_{z:0}", rail, timber, 0f, z, 1.1f, 0.15f);
            Area1RemoteSites.Thin("Bar", rail, timber, new Vector3(0f, 0.95f, 0f), new Vector3(0.08f, 0.08f, 3.2f));
        }

        /// <summary>Door on the spine, sign beside it. Barrels stand on the east side, where the cooper's cart comes round from next door.</summary>
        internal static void BuildShortMeasure(Plot plot, Terrain terrain, Material timber, Material stone, Transform root)
        {
            var alehouse = new GameObject("ShortMeasure").transform;
            alehouse.SetParent(root, false);
            Quaternion rotation = Quaternion.Euler(0f, plot.Yaw, 0f);
            Area1CampInterior.BuildHouseShell(plot.Center, plot.Width, plot.Depth, rotation, "West", terrain, timber, alehouse, STAFF_DOOR_X, DOOR_RAMP);

            float halfWidth = plot.Width * 0.5f, halfDepth = plot.Depth * 0.5f;
            Area1RemoteSites.Local("Counter", alehouse, timber, new Vector3(3.6f, 0.55f, -0.2f), new Vector3(0.7f, 1.1f, 3.6f));
            Area1RemoteSites.Local("Cask_0", alehouse, timber, new Vector3(4.7f, 0.5f, -1.4f), new Vector3(0.8f, 1f, 0.9f));
            Area1RemoteSites.Local("Cask_1", alehouse, timber, new Vector3(4.7f, 0.5f, 0.4f), new Vector3(0.8f, 1f, 0.9f));
            Area1RemoteSites.Local("Table_0", alehouse, timber, new Vector3(-3.6f, 0.4f, -1.2f), new Vector3(1.8f, 0.8f, 0.9f));
            Area1RemoteSites.Local("Table_1", alehouse, timber, new Vector3(-0.2f, 0.4f, -1.6f), new Vector3(1.8f, 0.8f, 0.9f));
            Area1RemoteSites.Local("Hearth", alehouse, stone, new Vector3(-halfWidth + 0.8f, 0.6f, 0f), new Vector3(1f, 1.2f, 1.6f));
            float chimneyTop = WALL_HEIGHT + halfDepth * Mathf.Tan(HOUSE_PITCH * Mathf.Deg2Rad) + 0.7f;
            Area1RemoteSites.Local("Chimney", alehouse, stone, new Vector3(-halfWidth + 0.8f, (1.2f + chimneyTop) * 0.5f, 0f), new Vector3(0.7f, chimneyTop - 1.2f, 0.7f));

            Vector2 Outside(float x, float z)
            {
                Vector3 local = rotation * new Vector3(x, 0f, z);
                return plot.Center + new Vector2(local.x, local.z);
            }

            Transform sign = Area1Stoneworks.Group("Sign", root, Outside(-3.4f, halfDepth + 0.5f), 0f, terrain);
            Post("Post", sign, timber, 0f, 0f, 3f, 0.2f);
            Area1RemoteSites.Thin("Arm", sign, timber, new Vector3(0.5f, 2.8f, 0f), new Vector3(1.1f, 0.1f, 0.1f));
            Area1RemoteSites.Thin("Board", sign, timber, new Vector3(0.6f, 2.3f, 0f), new Vector3(0.9f, 0.7f, 0.06f));
            Area1RemoteSites.Column("Bench", root, timber, terrain, Outside(1.4f, halfDepth + 0.5f), new Vector2(2f, 0.4f), 0.45f, 0f);
            foreach (int i in new[] { 0, 1, 2 })
                Area1RemoteSites.Column($"Barrel_{i}", root, timber, terrain, Outside(-halfWidth - 0.6f, 1.4f - i * 1.2f), new Vector2(0.8f, 0.8f), 1f, i * 25f);
        }

        /// <summary>Stilts and capped tops keep the rats out. Steps face the spine, and the door is solid.</summary>
        internal static void BuildGrainStore(Plot plot, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Transform store = Area1Stoneworks.Group("GrainStore", root, plot.Center, plot.Yaw, terrain);
            float halfWidth = plot.Width * 0.5f, halfDepth = plot.Depth * 0.5f;

            foreach (float x in new[] { -halfWidth + 0.3f, 0f, halfWidth - 0.3f })
                foreach (float z in new[] { -halfDepth + 0.3f, halfDepth - 0.3f })
                {
                    Post($"Stilt_{x:0}_{z:0}", store, timber, x, z, STILT, STILT_WIDTH);
                    Area1RemoteSites.Local($"Cap_{x:0}_{z:0}", store, stone, new Vector3(x, STILT + 0.05f, z), new Vector3(0.7f, 0.1f, 0.7f));
                }

            Area1RemoteSites.Local("Floor", store, timber, new Vector3(0f, GRAIN_FLOOR - 0.1f, 0f), new Vector3(plot.Width, 0.2f, plot.Depth));
            float wallY = GRAIN_FLOOR + GRAIN_WALL * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                Area1RemoteSites.Local(side < 0f ? "Wall_South" : "Wall_North", store, timber, new Vector3(0f, wallY, side * (halfDepth - WALL * 0.5f)), new Vector3(plot.Width, GRAIN_WALL, WALL));
                Area1RemoteSites.Local(side < 0f ? "Wall_West" : "Wall_East", store, timber, new Vector3(side * (halfWidth - WALL * 0.5f), wallY, 0f), new Vector3(WALL, GRAIN_WALL, plot.Depth));
            }

            float wallTop = GRAIN_FLOOR + GRAIN_WALL;
            Area1Stoneworks.GableRoof(store, timber, new Vector3(0f, wallTop, 0f), plot.Width + 0.8f, halfDepth, 0.4f, GRAIN_PITCH);
            foreach (float side in new[] { -1f, 1f })
                Tiers(side < 0f ? "Gable_West" : "Gable_East", store, timber, side * (halfWidth - WALL * 0.5f), wallTop, plot.Depth, GRAIN_PITCH);

            Area1RemoteSites.Thin("Door", store, timber, new Vector3(0f, GRAIN_FLOOR + 0.9f, -halfDepth - 0.03f), new Vector3(1.2f, 1.8f, 0.05f));
            foreach (int i in new[] { 0, 1, 2 })
            {
                float top = GRAIN_FLOOR * (3 - i) / 3f;
                Area1RemoteSites.Local($"Step_{i}", store, timber, new Vector3(0f, (top - BURY) * 0.5f, -halfDepth - 0.3f - i * 0.6f), new Vector3(1.4f, top + BURY, 0.6f));
            }
        }

        /// <summary>The yard is also the quarter's square. Pells stand in a row, with butts at the west end so shots go into the bank.</summary>
        internal static void BuildDrillYard(Plot plot, Vector2 wellAt, Vector2 bellAt, Terrain terrain, Material timber, Material stone, Transform root)
        {
            for (int i = 0; i < PELLS; i++)
            {
                Vector2 at = plot.Center + new Vector2((i - (PELLS - 1) * 0.5f) * PELL_SPACING + PELL_ROW_X, PELL_ROW_Z);
                Transform pell = Area1Stoneworks.Group($"Pell_{i}", root, at, 0f, terrain);
                Post("Post", pell, timber, 0f, 0f, 1.9f, 0.25f);
                Area1RemoteSites.Thin("Arm", pell, timber, new Vector3(0f, 1.35f, 0f), new Vector3(0.9f, 0.1f, 0.1f));
            }

            for (int i = 0; i < 3; i++)
            {
                Vector2 at = plot.Center + new Vector2(-BUTT_OFFSET, BUTT_ROW_Z + (i - 1) * BUTT_SPACING);
                Transform butt = Area1Stoneworks.Group($"Butt_{i}", root, at, 90f, terrain);
                Area1RemoteSites.Local("Face", butt, timber, new Vector3(0f, 0.9f, 0f), new Vector3(1.3f, 1.3f, 0.35f));
                foreach (int side in new[] { -1, 1 })
                    Post($"Leg_{side}", butt, timber, side * 0.4f, -0.3f, 1.2f, 0.12f);
            }

            Transform well = Area1Stoneworks.Group("Well", root, wellAt, 0f, terrain);
            Area1RemoteSites.Local("Ring", well, stone, new Vector3(0f, 0.25f, 0f), new Vector3(1.4f, 1.1f, 1.4f));
            foreach (int side in new[] { -1, 1 })
                Post($"Post_{side}", well, timber, side * 0.8f, 0f, 2.2f, 0.15f);
            Area1RemoteSites.Thin("Windlass", well, timber, new Vector3(0f, 1.9f, 0f), new Vector3(1.6f, 0.12f, 0.12f));

            Transform bell = Area1Stoneworks.Group("AlarmBell", root, bellAt, 0f, terrain);
            Post("Post", bell, timber, 0f, 0f, 3f, 0.25f);
            Area1RemoteSites.Thin("Arm", bell, timber, new Vector3(0.45f, 2.8f, 0f), new Vector3(0.9f, 0.1f, 0.1f));
            Area1RemoteSites.Thin("Bell", bell, stone, new Vector3(0.75f, 2.5f, 0f), new Vector3(0.35f, 0.4f, 0.35f));
        }

        /// <summary>
        /// Rows and mounds run along the bank, so each lies level on the slope with its stake at one end. The numbers count on from earlier
        /// burials, with gaps, so they read as a ledger and not as a list of people. Each stands on its own ground.
        /// </summary>
        internal static void BuildGraves(Plot plot, Terrain terrain, Material timber, Material stone, Transform root)
        {
            var graves = new GameObject("WorkersGraves").transform;
            graves.SetParent(root, false);
            Quaternion rotation = Quaternion.Euler(0f, plot.Yaw, 0f);
            Vector2 Along(float x, float z)
            {
                Vector3 local = rotation * new Vector3(x, 0f, z);
                return new Vector2(local.x, local.z);
            }

            int number = FIRST_NUMBER;
            int index = 0;
            float rowGap = plot.Width / GRAVE_ROWS, spacing = plot.Depth / GRAVES_PER_ROW;
            for (int row = 0; row < GRAVE_ROWS; row++)
                for (int i = 0; i < GRAVES_PER_ROW; i++, index++)
                {
                    Vector2 at = plot.Center + Along((row - (GRAVE_ROWS - 1) * 0.5f) * rowGap, (i - (GRAVES_PER_ROW - 1) * 0.5f) * spacing);
                    Transform grave = Area1Stoneworks.Group($"Grave_{number}", graves, at, plot.Yaw, terrain);

                    float ground = Area1BlockoutBuilder.Ground(terrain, at);
                    float head = Area1BlockoutBuilder.Ground(terrain, at + Along(0f, -GRAVE_LENGTH * 0.5f)) - ground;
                    float foot = Area1BlockoutBuilder.Ground(terrain, at + Along(0f, GRAVE_LENGTH * 0.5f)) - ground;
                    float tilt = Mathf.Atan2(head - foot, GRAVE_LENGTH) * Mathf.Rad2Deg;

                    Area1RemoteSites.Thin("Mound", grave, stone, new Vector3(0f, 0f, 0f), new Vector3(0.7f, 0.5f, GRAVE_LENGTH)).localRotation = Quaternion.Euler(tilt, 0f, 0f);

                    float stakeTop = head + 0.85f;
                    float stakeZ = -GRAVE_LENGTH * 0.5f;
                    Area1RemoteSites.Local("Stake", grave, timber, new Vector3(0f, (head - BURY + stakeTop) * 0.5f, stakeZ), new Vector3(0.08f, stakeTop - head + BURY, 0.08f));
                    Area1RemoteSites.Thin("Tag", grave, timber, new Vector3(0.05f, stakeTop - 0.12f, stakeZ), new Vector3(0.03f, 0.2f, 0.36f));
                    Number(number, grave, new Vector3(0.075f, stakeTop - 0.12f, stakeZ));

                    number += 1 + index * 7 % 4;
                }
        }

        /// <summary>
        /// Faces downhill, towards anyone reading it from the foot of the row. TextMeshPro, because the built-in font material of a
        /// legacy TextMesh ignores depth and draws through the ground.
        /// </summary>
        private static void Number(int number, Transform parent, Vector3 local)
        {
            var label = new GameObject("Number").AddComponent<TextMeshPro>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.localPosition = local;
            label.rectTransform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            label.rectTransform.sizeDelta = new Vector2(0.36f, 0.2f);
            label.text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            label.fontSize = 1.4f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(0xD8, 0xCC, 0xB0, 0xFF);
        }

        private static void Post(string name, Transform parent, Material timber, float x, float z, float height, float width)
        {
            Area1RemoteSites.Local(name, parent, timber, new Vector3(x, (height - BURY) * 0.5f, z), new Vector3(width, height + BURY, width));
        }

        /// <summary>A wall right across a shell, up to the roof, so a closed room shows no gap at the top.</summary>
        private static void Partition(string name, Transform parent, Material material, float x, float depth)
        {
            Area1RemoteSites.Local(name, parent, material, new Vector3(x, WALL_HEIGHT * 0.5f, 0f), new Vector3(WALL, WALL_HEIGHT, depth));
            Tiers($"{name}_Gable", parent, material, x, WALL_HEIGHT, depth, HOUSE_PITCH);
        }

        /// <summary>Stepped, because a square gable leaves sky at the end of a closed room.</summary>
        private static void Tiers(string name, Transform parent, Material material, float x, float wallTop, float depth, float pitch)
        {
            float rise = depth * 0.5f * Mathf.Tan(pitch * Mathf.Deg2Rad);
            float tier = rise / GABLE_TIERS;
            for (int i = 0; i < GABLE_TIERS; i++)
                Area1RemoteSites.Local($"{name}_{i}", parent, material, new Vector3(x, wallTop + tier * (i + 0.5f), 0f),
                    new Vector3(WALL, tier, depth * (1f - (i + 0.5f) / GABLE_TIERS)));
        }
    }
}
