using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// The graybox shapes inside and at the edge of the camp ring. <see cref="Area1SitesBuilder"/> owns the roots and validates
    /// the layout first. Each area keeps part of its block empty for later NPCs and props.
    /// </summary>
    internal static class Area1CampInterior
    {
        private const string TAG = "[Area1CampInterior]";

        private const float WALL_HEIGHT = 4f;
        private const float WALL_THICKNESS = 0.4f;
        private const float DOOR_WIDTH = 2.4f;
        private const float DOOR_HEIGHT = 2.8f;
        private const float DOOR_X = 2f;          // from the warehouse centre, on both long walls
        private const float ROOF_PITCH = 30f;
        private const float LANDING_DEPTH = 3.8f; // keeps the river ramp clear of the wall posts
        private const float RAMP_WIDTH = 3.5f;    // a cart's width
        private const float RAMP_THICKNESS = 0.2f;
        private const float CAMP_RAMP_LENGTH = 13.2f; // 18 degrees over the 4.2 m drop to the tents
        private const float RIVER_RAMP_LENGTH = 10f;
        private const float POST_SPACING = 4f;
        private const float POST_BURY = 0.5f;
        private const int FLOOR_SAMPLES = 5;      // per side: the rim falls away, so one point can sit low

        private const float POST_WALL_HEIGHT = 3f;
        private const float POST_DOOR_X = -2f;      // towards the compound gate
        private const float POST_DOOR_WIDTH = 1.6f;
        private const float POST_DOOR_HEIGHT = 2.4f;
        private const float POST_RAMP_WIDTH = 1.6f;
        private const float POST_RAMP_MIN = 4f;
        private const float POST_RAMP_MAX = 7f;     // stops short of the delivery road
        private const float POST_RAMP_STEP = 0.25f;
        private const float POST_RAMP_MAX_ANGLE = 30f; // the ground keeps falling, so a shallower ramp would run 15 m
        private const float POST_WIDTH = 0.3f;
        private const float SKIRT_THICKNESS = 0.15f;
        private const int GABLE_TIERS = 5;
        private const float BANNER_POLE_HEIGHT = 6f;
        private const string GANG_DECK = "BackDeck";
        private const float GANG_DECK_DEPTH = 3f;
        private const float GANG_DECK_THICKNESS = 0.15f;
        private const float GANG_DECK_CLEARANCE = 0.15f;  // over the highest ground under it, so it stays easy to step onto
        private const float GANG_DOCK_OVERLAP = 0.4f;     // the boards run a little under the deck
        private const float GANG_DOCK_DROP = 0.02f;       // just below the deck, so the overlap doesn't flicker
        private const float GANG_DOCK_BAY = 3f;
        private const float GANG_BOAT_OFFSET = 1f;
        private const float CAGE_SIZE = 2.5f;
        private const float CAGE_HEIGHT = 2.4f;
        private const float CAGE_BAR_GAP = 0.35f;

        private const float GATE_HEIGHT = 5f;
        private const float BOARD_LENGTH = 1f;

        /// <summary>Open leaves between the two hand-built gate posts, with the guard hut beside them, as in the Morning gate art.</summary>
        internal static void BuildInnerGate(Vector2 gate, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Area1RemoteSites.Column("Leaf_West", root, timber, terrain, gate + new Vector2(-3.4f, -1.7f), new Vector2(0.15f, 3f), 3.4f, 6f);
            Area1RemoteSites.Column("Leaf_East", root, timber, terrain, gate + new Vector2(3.4f, -1.7f), new Vector2(0.15f, 3f), 3.4f, -6f);

            Vector2 hutAt = gate + new Vector2(9.7f, -3f);
            var hut = new GameObject("GuardHut").transform;
            hut.SetParent(root, false);
            hut.position = new Vector3(hutAt.x, Area1BlockoutBuilder.Ground(terrain, hutAt), hutAt.y);
            Area1RemoteSites.Local("Walls", hut, timber, new Vector3(0f, 1.1f, 0f), new Vector3(3f, 2.6f, 3f));
            Transform roof = Area1RemoteSites.Local("Roof", hut, timber, new Vector3(0f, 2.6f, 0f), new Vector3(3.6f, 0.12f, 3.8f));
            roof.localRotation = Quaternion.Euler(0f, 0f, 12f);

            Vector2 brazierAt = gate + new Vector2(5f, -1.4f);
            var brazier = new GameObject("Brazier").transform;
            brazier.SetParent(root, false);
            brazier.position = new Vector3(brazierAt.x, Area1BlockoutBuilder.Ground(terrain, brazierAt), brazierAt.y);
            Area1RemoteSites.Local("Post", brazier, timber, new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 1.1f, 0.12f));
            Area1RemoteSites.Local("Basin", brazier, stone, new Vector3(0f, 1f, 0f), new Vector3(0.6f, 0.2f, 0.6f));
        }

        /// <summary>
        /// Built into the north palisade on the rim. The camp door and the river door are both real, so the building is a route
        /// to the docks: Area1BlockoutBuilder drops the palisade panels inside its footprint.
        /// </summary>
        internal static void BuildWarehouse(Vector2 center, float width, float depth, Terrain terrain, Material timber, Transform root)
        {
            float halfWidth = width * 0.5f, halfDepth = depth * 0.5f;
            float floor = float.NegativeInfinity;
            for (int x = 0; x < FLOOR_SAMPLES; x++)
                for (int z = 0; z < FLOOR_SAMPLES; z++)
                    floor = Mathf.Max(floor, Area1BlockoutBuilder.Ground(terrain, center + new Vector2(
                        Mathf.Lerp(-halfWidth, halfWidth, x / (FLOOR_SAMPLES - 1f)), Mathf.Lerp(-halfDepth, halfDepth, z / (FLOOR_SAMPLES - 1f)))));
            root.position = new Vector3(center.x, floor, center.y);

            Area1RemoteSites.Local("Floor", root, timber, new Vector3(0f, -0.15f, 0f), new Vector3(width, 0.3f, depth));
            Area1RemoteSites.Local("Wall_West", root, timber, new Vector3(-halfWidth + WALL_THICKNESS * 0.5f, WALL_HEIGHT * 0.5f, 0f), new Vector3(WALL_THICKNESS, WALL_HEIGHT, depth));
            Area1RemoteSites.Local("Wall_East", root, timber, new Vector3(halfWidth - WALL_THICKNESS * 0.5f, WALL_HEIGHT * 0.5f, 0f), new Vector3(WALL_THICKNESS, WALL_HEIGHT, depth));
            DoorWall("Wall_Camp", -halfDepth + WALL_THICKNESS * 0.5f, halfWidth, timber, root);
            DoorWall("Wall_River", halfDepth - WALL_THICKNESS * 0.5f, halfWidth, timber, root);

            float rise = halfDepth * Mathf.Tan(ROOF_PITCH * Mathf.Deg2Rad);
            float slope = halfDepth / Mathf.Cos(ROOF_PITCH * Mathf.Deg2Rad) + 0.6f;
            Transform south = Area1RemoteSites.Local("Roof_Camp", root, timber, new Vector3(0f, WALL_HEIGHT + rise * 0.5f, -halfDepth * 0.5f), new Vector3(width + 1f, 0.2f, slope));
            south.localRotation = Quaternion.Euler(-ROOF_PITCH, 0f, 0f);
            Transform north = Area1RemoteSites.Local("Roof_River", root, timber, new Vector3(0f, WALL_HEIGHT + rise * 0.5f, halfDepth * 0.5f), new Vector3(width + 1f, 0.2f, slope));
            north.localRotation = Quaternion.Euler(ROOF_PITCH, 0f, 0f);
            foreach (float side in new[] { -1f, 1f })
                Area1RemoteSites.Local(side < 0f ? "Gable_West" : "Gable_East", root, timber,
                    new Vector3(side * (halfWidth - WALL_THICKNESS * 0.5f), WALL_HEIGHT + rise * 0.25f, 0f), new Vector3(WALL_THICKNESS, rise * 0.5f, depth * 0.5f));

            // The rim falls away both sides, so the long walls stand on posts.
            foreach (float z in new[] { -halfDepth, halfDepth })
            {
                int index = 0;
                for (float x = -halfWidth; x <= halfWidth + 0.01f; x += POST_SPACING)
                {
                    if (Mathf.Abs(x - DOOR_X) < DOOR_WIDTH * 0.5f) continue; // never in a doorway
                    Stilt($"Post_{(z < 0f ? "Camp" : "River")}_{index++}", center + new Vector2(x, z), floor, terrain, timber, root);
                }
            }

            // The palisade panels stop here, so this closes the old fence line under the floor.
            Area1BlockoutBuilder.GroundUnder(terrain, center - new Vector2(halfWidth, 0f), center + new Vector2(halfWidth, 0f), out float low, out _, out _);
            float skirtBottom = low - 1f, skirtTop = floor - 0.3f;
            Area1BlockoutBuilder.Box("Skirt", root, timber, new Vector3(center.x, (skirtTop + skirtBottom) * 0.5f, center.y), new Vector3(width, skirtTop - skirtBottom, WALL_THICKNESS));

            Area1RemoteSites.Local("Landing", root, timber, new Vector3(DOOR_X, -0.15f, halfDepth + LANDING_DEPTH * 0.5f), new Vector3(6f, 0.3f, LANDING_DEPTH));
            foreach (float side in new[] { -1f, 1f })
                Stilt(side < 0f ? "Post_Landing_W" : "Post_Landing_E", center + new Vector2(DOOR_X + side * 2.8f, halfDepth + LANDING_DEPTH - 0.2f), floor, terrain, timber, root);

            // Down the river side, westward along the wall to the head of the deck. The bank path runs north of it.
            float rampZ = center.y + halfDepth + LANDING_DEPTH * 0.5f;
            float rampTop = center.x - 1f;
            Vector2 riverEnd = new Vector2(rampTop - RIVER_RAMP_LENGTH, rampZ);
            Slab("Ramp_River", new Vector3(rampTop, floor, rampZ), new Vector3(riverEnd.x, Area1BlockoutBuilder.Ground(terrain, riverEnd), riverEnd.y), root, timber);

            float doorX = center.x + DOOR_X;
            float campZ = center.y - halfDepth;
            Vector2 campEnd = new Vector2(doorX, campZ - CAMP_RAMP_LENGTH);
            Slab("Ramp_Camp", new Vector3(doorX, floor, campZ), new Vector3(campEnd.x, Area1BlockoutBuilder.Ground(terrain, campEnd), campEnd.y), root, timber);

            // Hoist and crates at the bank end, as in the Dock checkpoint art.
            Area1RemoteSites.Local("Hoist_Post", root, timber, new Vector3(halfWidth - 0.4f, WALL_HEIGHT + 0.6f, halfDepth + 0.3f), new Vector3(0.25f, 1.6f, 0.25f));
            Transform arm = Area1RemoteSites.Local("Hoist_Arm", root, timber, new Vector3(halfWidth - 0.4f, WALL_HEIGHT + 1.3f, halfDepth + 1.4f), new Vector3(0.2f, 0.2f, 2.6f));
            arm.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Area1RemoteSites.Thin("Hoist_Rope", root, timber, new Vector3(halfWidth - 0.4f, WALL_HEIGHT - 1.2f, halfDepth + 2.5f), new Vector3(0.05f, 5f, 0.05f));

            Vector2[] crates = { new Vector2(7f, 0.9f), new Vector2(8f, 1.1f), new Vector2(7.4f, 2.1f) }; // east of the landing
            for (int i = 0; i < crates.Length; i++)
            {
                Vector2 at = center + new Vector2(crates[i].x, halfDepth + crates[i].y);
                Area1RemoteSites.Column($"Crate_{i}", root, timber, terrain, at, new Vector2(1f, 1f), 1f, i * 25f);
            }
        }

        private static void Stilt(string name, Vector2 at, float floor, Terrain terrain, Material timber, Transform root)
        {
            float ground = Area1BlockoutBuilder.Ground(terrain, at);
            if (ground >= floor) return;
            float bottom = ground - POST_BURY;
            Area1BlockoutBuilder.Box(name, root, timber, new Vector3(at.x, (floor + bottom) * 0.5f, at.y), new Vector3(0.3f, floor - bottom, 0.3f));
        }

        private static void DoorWall(string name, float z, float halfWidth, Material timber, Transform root,
            float doorX = DOOR_X, float doorWidth = DOOR_WIDTH, float doorHeight = DOOR_HEIGHT, float wallHeight = WALL_HEIGHT)
        {
            float leftEnd = doorX - doorWidth * 0.5f, rightStart = doorX + doorWidth * 0.5f;
            float leftLength = leftEnd + halfWidth, rightLength = halfWidth - rightStart;
            Area1RemoteSites.Local($"{name}_L", root, timber, new Vector3(-halfWidth + leftLength * 0.5f, wallHeight * 0.5f, z), new Vector3(leftLength, wallHeight, WALL_THICKNESS));
            Area1RemoteSites.Local($"{name}_R", root, timber, new Vector3(halfWidth - rightLength * 0.5f, wallHeight * 0.5f, z), new Vector3(rightLength, wallHeight, WALL_THICKNESS));
            Area1RemoteSites.Local($"{name}_Header", root, timber, new Vector3(doorX, (doorHeight + wallHeight) * 0.5f, z), new Vector3(doorWidth, wallHeight - doorHeight, WALL_THICKNESS));
        }

        /// <summary>A flat walkable board between two points, its top on the line.</summary>
        private static void Slab(string name, Vector3 from, Vector3 to, Transform root, Material timber, float width = RAMP_WIDTH)
        {
            Vector3 along = to - from;
            Quaternion rotation = Quaternion.LookRotation(along);
            Transform slab = Area1BlockoutBuilder.Box(name, root, timber, (from + to) * 0.5f - rotation * Vector3.up * (RAMP_THICKNESS * 0.5f),
                new Vector3(width, RAMP_THICKNESS, along.magnitude));
            slab.rotation = rotation;
        }

        /// <summary>
        /// A gate with its road along local X, turned clockwise from above by <paramref name="yaw"/>. The leaves swing back along
        /// local +X and the guard post stands on that side. It stands open in the blockout, so the route stays walkable;
        /// restricting it is later gameplay.
        /// </summary>
        internal static void BuildGate(Vector2 gate, float width, Terrain terrain, Material timber, Transform root, float yaw = 0f, bool guardPost = true)
        {
            float half = width * 0.5f;
            Vector2 south = GateOffset(gate, new Vector2(0f, -half), yaw), north = GateOffset(gate, new Vector2(0f, half), yaw);
            float southGround = Area1BlockoutBuilder.Ground(terrain, south), northGround = Area1BlockoutBuilder.Ground(terrain, north);
            float top = Mathf.Max(southGround, northGround) + GATE_HEIGHT; // the bank falls across the gate, so both posts reach one top
            Area1RemoteSites.Column("Post_South", root, timber, terrain, south, new Vector2(0.5f, 0.5f), top - southGround, yaw);
            Area1RemoteSites.Column("Post_North", root, timber, terrain, north, new Vector2(0.5f, 0.5f), top - northGround, yaw);
            Transform lintel = Area1BlockoutBuilder.Box("Lintel", root, timber, new Vector3(gate.x, top, gate.y), new Vector3(0.35f, 0.35f, width + 0.8f));
            lintel.rotation = Quaternion.Euler(0f, yaw, 0f);

            float leafX = half * 0.5f + 0.15f; // each leaf is half the gate's width, swung back along the road
            Area1RemoteSites.Column("Leaf_South", root, timber, terrain, GateOffset(gate, new Vector2(leafX, -half - 0.1f), yaw), new Vector2(half, 0.12f), 2.6f, yaw - 4f);
            Area1RemoteSites.Column("Leaf_North", root, timber, terrain, GateOffset(gate, new Vector2(leafX, half + 0.1f), yaw), new Vector2(half, 0.12f), 2.6f, yaw + 4f);

            if (guardPost)
                Area1RemoteSites.BuildLeanTo("GuardPost", GateOffset(gate, new Vector2(half + 1.7f, -half - 0.4f), yaw), GateOffset(Vector2.zero, Vector2.up, yaw), terrain, timber, root); // long side along the bank
        }

        internal static Vector2 GateOffset(Vector2 gate, Vector2 local, float yaw)
        {
            Vector3 turned = Quaternion.Euler(0f, yaw, 0f) * new Vector3(local.x, 0f, local.y);
            return gate + new Vector2(turned.x, turned.z);
        }

        internal static void BuildWorkGangTents(Vector2 center, GameObject tent, Terrain terrain, Material stone, Transform root)
        {
            (Vector2 local, float yaw)[] tents =
            {
                (new Vector2(-7f, -3f), 20f), (new Vector2(-1f, -4f), -15f), (new Vector2(5f, -5f), 10f), (new Vector2(-5f, 4f), 200f), (new Vector2(1f, 3f), 165f),
            };
            for (int i = 0; i < tents.Length; i++)
            {
                Vector2 at = center + tents[i].local;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(tent, root);
                instance.name = $"Tent_{i}";
                instance.transform.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), Quaternion.Euler(0f, tents[i].yaw, 0f));
            }

            Vector2 fireAt = center + new Vector2(-2f, 0f);
            var fire = new GameObject("FireRing").transform;
            fire.SetParent(root, false);
            fire.position = new Vector3(fireAt.x, Area1BlockoutBuilder.Ground(terrain, fireAt), fireAt.y);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                Area1RemoteSites.Local($"Stone_{i}", fire, stone, new Vector3(Mathf.Cos(angle) * 0.7f, 0.12f, Mathf.Sin(angle) * 0.7f), new Vector3(0.3f, 0.24f, 0.3f));
            }
        }

        internal static void BuildLatrine(Vector2 center, Terrain terrain, Material timber, Material stone, Transform root)
        {
            root.position = new Vector3(center.x, Area1BlockoutBuilder.Ground(terrain, center), center.y);
            Area1RemoteSites.Local("Trench", root, stone, new Vector3(0f, 0.02f, 0f), new Vector3(5f, 0.06f, 1.2f));
            Area1RemoteSites.Local("Bench", root, timber, new Vector3(0f, 0.45f, 0f), new Vector3(5f, 0.12f, 0.6f));
            Area1RemoteSites.Local("Screen_Back", root, timber, new Vector3(0f, 0.8f, 1.1f), new Vector3(6f, 2f, 0.12f));
            Area1RemoteSites.Local("Screen_West", root, timber, new Vector3(-3f, 0.8f, 0.2f), new Vector3(0.12f, 2f, 1.8f));
            Area1RemoteSites.Local("Screen_East", root, timber, new Vector3(3f, 0.8f, 0.2f), new Vector3(0.12f, 2f, 1.8f));
        }

        internal static void BuildCart(string name, Vector2 at, float yaw, Terrain terrain, Material timber, Transform parent)
        {
            var cart = new GameObject(name).transform;
            cart.SetParent(parent, false);
            cart.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), Quaternion.Euler(0f, yaw, 0f));
            Area1RemoteSites.Local("Bed", cart, timber, new Vector3(0f, 0.7f, 0f), new Vector3(1.2f, 0.5f, 2f));
            Area1RemoteSites.Local("Wheel_L", cart, timber, new Vector3(-0.7f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.8f));
            Area1RemoteSites.Local("Wheel_R", cart, timber, new Vector3(0.7f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.8f));
            Area1RemoteSites.Local("Handle", cart, timber, new Vector3(0f, 0.6f, 1.6f), new Vector3(0.9f, 0.08f, 1.4f));
        }

        /// <summary>Keeps north of the delivery road and west of the tents-to-fire strip.</summary>
        internal static void BuildStagingYard(Vector2 center, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Vector2 shedAt = center + new Vector2(-7f, 0.5f);
            var shed = new GameObject("CartShed").transform;
            shed.SetParent(root, false);
            shed.position = new Vector3(shedAt.x, Area1BlockoutBuilder.Ground(terrain, shedAt), shedAt.y);
            foreach (float x in new[] { -2.8f, 2.8f })
                foreach (float z in new[] { -2.2f, 2.2f })
                    Stilt($"Post_{x:0}_{z:0}", shedAt + new Vector2(x, z), shed.position.y + 2.75f, terrain, timber, shed); // the bank falls under it
            Area1RemoteSites.Local("Roof", shed, timber, new Vector3(0f, 2.82f, 0f), new Vector3(6.4f, 0.14f, 5.2f));
            BuildCart("Cart_0", shedAt + new Vector2(-1.3f, 0f), 0f, terrain, timber, root);
            BuildCart("Cart_1", shedAt + new Vector2(1.3f, 0.3f), 4f, terrain, timber, root);

            Vector2[] crates = { new Vector2(0f, 2f), new Vector2(1.3f, 2.3f), new Vector2(0.5f, 0.8f), new Vector2(2.6f, 0.5f), new Vector2(2.2f, 2.8f) };
            for (int i = 0; i < crates.Length; i++)
                Area1RemoteSites.Column($"Crate_{i}", root, timber, terrain, center + crates[i], new Vector2(1f, 1f), 1f + (i % 2) * 0.8f, i * 17f);

            Vector2[] sacks = { new Vector2(0.5f, -2.5f), new Vector2(1.7f, -2.8f), new Vector2(2.9f, -2.5f), new Vector2(1.2f, -1.5f) };
            for (int i = 0; i < sacks.Length; i++)
                Area1RemoteSites.Column($"Sack_{i}", root, stone, terrain, center + sacks[i], new Vector2(1.1f, 0.6f), 0.5f, i * 31f);

            BuildSmokehouse(center + new Vector2(-13.5f, 0f), terrain, timber, stone, root);
            BuildNetRack("NetRack_0", center + new Vector2(-1f, 4.5f), 8f, terrain, timber, root);
            BuildNetRack("NetRack_1", center + new Vector2(8f, 3.7f), -12f, terrain, timber, root);

            Area1RemoteSites.Column("GuttingTable", root, timber, terrain, center + new Vector2(3f, 4.5f), new Vector2(2.4f, 0.9f), 0.9f, 6f);
            Vector2[] barrels = { new Vector2(5f, 5.1f), new Vector2(5.8f, 4.5f), new Vector2(5.2f, 4f) };
            for (int i = 0; i < barrels.Length; i++)
                Area1RemoteSites.Column($"Barrel_{i}", root, stone, terrain, center + barrels[i], new Vector2(0.7f, 0.7f), 1f, i * 20f);
        }

        private static void BuildSmokehouse(Vector2 at, Terrain terrain, Material timber, Material stone, Transform root)
        {
            const float size = 4f, wall = 2.4f, pitch = 35f;
            var house = new GameObject("Smokehouse").transform;
            house.SetParent(root, false);
            house.position = new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y);

            Area1RemoteSites.Local("Body", house, timber, new Vector3(0f, wall * 0.5f - 0.5f, 0f), new Vector3(size, wall + 1f, size));
            float half = size * 0.5f;
            float rise = half * Mathf.Tan(pitch * Mathf.Deg2Rad);
            float slope = half / Mathf.Cos(pitch * Mathf.Deg2Rad) + 0.4f;
            foreach (float side in new[] { -1f, 1f })
            {
                Transform roof = Area1RemoteSites.Local(side < 0f ? "Roof_West" : "Roof_East", house, timber,
                    new Vector3(side * half * 0.5f, wall + rise * 0.5f, 0f), new Vector3(slope, 0.16f, size + 0.6f));
                roof.localRotation = Quaternion.Euler(0f, 0f, -side * pitch);
                for (int tier = 0; tier < GABLE_TIERS; tier++)
                {
                    float height = rise / GABLE_TIERS;
                    Area1RemoteSites.Local($"{(side < 0f ? "Gable_South" : "Gable_North")}_{tier}", house, timber,
                        new Vector3(0f, wall + height * (tier + 0.5f), side * (half - 0.1f)),
                        new Vector3(size * (1f - (tier + 0.5f) / GABLE_TIERS), height, 0.2f));
                }
            }
            Area1RemoteSites.Local("Vent", house, stone, new Vector3(0f, wall + rise + 0.2f, 0f), new Vector3(0.7f, 0.5f, 0.7f));
        }

        private static void BuildNetRack(string name, Vector2 at, float yaw, Terrain terrain, Material timber, Transform root)
        {
            const float length = 3.2f, height = 2.2f;
            var rack = new GameObject(name).transform;
            rack.SetParent(root, false);
            rack.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), Quaternion.Euler(0f, yaw, 0f));
            foreach (float side in new[] { -1f, 1f })
                Area1RemoteSites.Local(side < 0f ? "Post_L" : "Post_R", rack, timber, new Vector3(side * length * 0.5f, height * 0.5f - 0.2f, 0f), new Vector3(0.14f, height + 0.4f, 0.14f));
            Area1RemoteSites.Local("Bar", rack, timber, new Vector3(0f, height, 0f), new Vector3(length + 0.3f, 0.1f, 0.1f));
            Area1RemoteSites.Thin("Net", rack, timber, new Vector3(0f, height - 0.75f, 0f), new Vector3(length - 0.3f, 1.4f, 0.03f));
        }

        /// <summary>
        /// The corner by the compound gate: an enterable house with its back to the palisade, plus the closed cage, tally
        /// shed and yard props. Local Z faces the delivery road and local -X the gate. The ground falls about 3 m across the
        /// footprint, so the floor takes the highest point and a skirt closes the space under it.
        /// </summary>
        internal static void BuildGuardPost(Vector2 center, float width, float depth, float yaw, Terrain terrain, Material timber, Material stone, Transform root)
        {
            float halfWidth = width * 0.5f, halfDepth = depth * 0.5f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            float floor = BuildHouseShell(center, width, depth, rotation, "Gate", terrain, timber, root);
            Vector2 At(float x, float z) => LocalToGround(center, rotation, x, z);

            // One room: two double bunks along the back wall, hearth at the far end, table between it and the door.
            foreach (int i in new[] { 0, 1 })
                Area1RemoteSites.Local($"Bunks_{i}", root, timber, new Vector3(-2.5f + i * 2.2f, 0.8f, -halfDepth + WALL_THICKNESS + 0.45f), new Vector3(1.9f, 1.6f, 0.8f));
            BuildHearth(halfWidth, depth, stone, root);
            Area1RemoteSites.Local("Table", root, timber, new Vector3(1f, 0.4f, 1f), new Vector3(1.6f, 0.8f, 0.8f));

            BuildLockup(At(-6.77f, -1.32f), rotation, terrain, timber, root);
            BuildTallyShed(At(7.47f, -1.09f), rotation, terrain, timber, root);

            Vector2 bellAt = At(1.58f, 5.36f);
            Area1RemoteSites.Column("BellPost", root, timber, terrain, bellAt, new Vector2(0.25f, 0.25f), 3f, yaw);
            float bellTop = Area1BlockoutBuilder.Ground(terrain, bellAt) + 3f;
            Area1RemoteSites.Thin("Bell", root, stone, new Vector3(1.58f, bellTop - floor - 0.3f, 5.36f + 0.25f), new Vector3(0.35f, 0.4f, 0.35f));
            Area1RemoteSites.Column("WeaponRack", root, timber, terrain, At(2f, 3.8f), new Vector2(1.6f, 0.3f), 1.5f, yaw);
            Area1RemoteSites.Column("ChoppingBlock", root, timber, terrain, At(0.5f, 4.2f), new Vector2(0.6f, 0.6f), 0.6f, yaw);
            Area1RemoteSites.Column("Firewood", root, timber, terrain, At(0.4f, 3f), new Vector2(2.6f, 1f), 1.2f, yaw);

            Vector2 bannerAt = At(5.6f, 1.7f);
            Area1RemoteSites.Column("BannerPole", root, timber, terrain, bannerAt, new Vector2(0.18f, 0.18f), BANNER_POLE_HEIGHT, yaw);
            float bannerTop = Area1BlockoutBuilder.Ground(terrain, bannerAt) + BANNER_POLE_HEIGHT - floor;
            Area1RemoteSites.Thin("BannerArm", root, timber, new Vector3(5.6f, bannerTop - 0.2f, 2.2f), new Vector3(0.08f, 0.08f, 1.1f));
            Area1RemoteSites.Thin("Banner", root, stone, new Vector3(5.6f, bannerTop - 1.3f, 2.3f), new Vector3(0.04f, 2.1f, 0.9f));
        }

        /// <summary>
        /// On the shore by the wilderness opening: the one real roof the Company allows the workers' side. Local Z faces the camp,
        /// and the back deck, close to the ground, leads to the dock on the lake side.
        /// </summary>
        internal static void BuildGangHouse(Vector2 center, float width, float depth, float yaw, Terrain terrain, Material timber, Material stone, Transform root)
        {
            float halfWidth = width * 0.5f, halfDepth = depth * 0.5f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            BuildHouseShell(center, width, depth, rotation, "East", terrain, timber, root);

            // One room: bed along the back wall, hearth at the far end, table in the middle, chest by the door.
            Area1RemoteSites.Local("Bed", root, timber, new Vector3(-2.6f, 0.3f, -halfDepth + WALL_THICKNESS + 0.6f), new Vector3(2.1f, 0.6f, 1.1f));
            BuildHearth(halfWidth, depth, stone, root);
            Area1RemoteSites.Local("Table", root, timber, new Vector3(1f, 0.4f, 0.4f), new Vector3(1.8f, 0.8f, 0.9f));
            Area1RemoteSites.Local("Chest", root, timber, new Vector3(-3.6f, 0.3f, halfDepth - WALL_THICKNESS - 0.4f), new Vector3(1f, 0.6f, 0.6f));

            float deckTop = float.NegativeInfinity;
            foreach (float x in new[] { -halfWidth, 0f, halfWidth })
                foreach (float z in new[] { 0f, GANG_DECK_DEPTH })
                    deckTop = Mathf.Max(deckTop, Area1BlockoutBuilder.Ground(terrain, LocalToGround(center, rotation, x, -halfDepth - z)));
            deckTop += GANG_DECK_CLEARANCE;

            Vector2 deckAt = LocalToGround(center, rotation, 0f, -halfDepth - GANG_DECK_DEPTH * 0.5f);
            Transform deck = Area1BlockoutBuilder.Box(GANG_DECK, root, timber, new Vector3(deckAt.x, deckTop - GANG_DECK_THICKNESS * 0.5f, deckAt.y),
                new Vector3(width, GANG_DECK_THICKNESS, GANG_DECK_DEPTH));
            deck.rotation = rotation;
        }

        /// <summary>Boards at the back deck's height on posts into the lake bed, with a small boat at the head.</summary>
        internal static void BuildGangDock(Vector2[] line, float width, Terrain terrain, Material timber, Transform house, Transform root)
        {
            Vector2 from = line[0], to = line[line.Length - 1];
            Vector2 along = (to - from).normalized;
            float length = Vector2.Distance(from, to);
            float top = house.Find(GANG_DECK).GetComponent<Renderer>().bounds.max.y;

            Vector2 middle = (from + to) * 0.5f;
            Transform boards = Area1BlockoutBuilder.Box("Boards", root, timber, new Vector3(middle.x, top - GANG_DOCK_DROP - GANG_DECK_THICKNESS * 0.5f, middle.y),
                new Vector3(width, GANG_DECK_THICKNESS, length + GANG_DOCK_OVERLAP));
            boards.rotation = Area1BlockoutBuilder.Yaw(along);

            Vector2 across = new Vector2(along.y, -along.x) * (width * 0.5f);
            int bays = Mathf.CeilToInt(length / GANG_DOCK_BAY);
            for (int i = 1; i <= bays; i++)
            {
                Vector2 at = from + along * (length * i / bays);
                Stilt($"Post_{i}_L", at + across, top, terrain, timber, root);
                Stilt($"Post_{i}_R", at - across, top, terrain, timber, root);
            }

            Area1SitesBuilder.BuildBoat(to - along * 2f + across.normalized * (width * 0.5f + GANG_BOAT_OFFSET), along, timber, root);
        }

        private static Vector2 LocalToGround(Vector2 center, Quaternion rotation, float x, float z)
        {
            Vector3 local = rotation * new Vector3(x, 0f, z);
            return center + new Vector2(local.x, local.z);
        }

        /// <summary>Floor, walls, door, pitched roof, closed underside and ramp. Returns the floor height.</summary>
        internal static float BuildHouseShell(Vector2 center, float width, float depth, Quaternion rotation, string sideName, Terrain terrain, Material timber, Transform root)
        {
            float halfWidth = width * 0.5f, halfDepth = depth * 0.5f;
            Vector2 At(float x, float z) => LocalToGround(center, rotation, x, z);

            float floor = float.NegativeInfinity;
            for (int x = 0; x < FLOOR_SAMPLES; x++)
                for (int z = 0; z < FLOOR_SAMPLES; z++)
                    floor = Mathf.Max(floor, Area1BlockoutBuilder.Ground(terrain, At(
                        Mathf.Lerp(-halfWidth, halfWidth, x / (FLOOR_SAMPLES - 1f)), Mathf.Lerp(-halfDepth, halfDepth, z / (FLOOR_SAMPLES - 1f)))));
            root.SetPositionAndRotation(new Vector3(center.x, floor, center.y), rotation);

            Area1RemoteSites.Local("Floor", root, timber, new Vector3(0f, -0.15f, 0f), new Vector3(width, 0.3f, depth));
            Area1RemoteSites.Local($"Wall_{sideName}", root, timber, new Vector3(-halfWidth + WALL_THICKNESS * 0.5f, POST_WALL_HEIGHT * 0.5f, 0f), new Vector3(WALL_THICKNESS, POST_WALL_HEIGHT, depth));
            Area1RemoteSites.Local("Wall_End", root, timber, new Vector3(halfWidth - WALL_THICKNESS * 0.5f, POST_WALL_HEIGHT * 0.5f, 0f), new Vector3(WALL_THICKNESS, POST_WALL_HEIGHT, depth));
            Area1RemoteSites.Local("Wall_Back", root, timber, new Vector3(0f, POST_WALL_HEIGHT * 0.5f, -halfDepth + WALL_THICKNESS * 0.5f), new Vector3(width, POST_WALL_HEIGHT, WALL_THICKNESS));
            DoorWall("Wall_Front", halfDepth - WALL_THICKNESS * 0.5f, halfWidth, timber, root, POST_DOOR_X, POST_DOOR_WIDTH, POST_DOOR_HEIGHT, POST_WALL_HEIGHT);

            float rise = RoofRise(depth);
            float slope = halfDepth / Mathf.Cos(ROOF_PITCH * Mathf.Deg2Rad) + 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                Transform roof = Area1RemoteSites.Local(side < 0f ? "Roof_Back" : "Roof_Front", root, timber,
                    new Vector3(0f, POST_WALL_HEIGHT + rise * 0.5f, side * halfDepth * 0.5f), new Vector3(width + 0.8f, 0.2f, slope));
                roof.localRotation = Quaternion.Euler(side * ROOF_PITCH, 0f, 0f);

                // Stepped, because the room is entered and a square gable leaves sky at both ends.
                for (int tier = 0; tier < GABLE_TIERS; tier++)
                {
                    float height = rise / GABLE_TIERS;
                    Area1RemoteSites.Local($"{(side < 0f ? $"Gable_{sideName}" : "Gable_End")}_{tier}", root, timber,
                        new Vector3(side * (halfWidth - WALL_THICKNESS * 0.5f), POST_WALL_HEIGHT + height * (tier + 0.5f), 0f),
                        new Vector3(WALL_THICKNESS, height, depth * (1f - (tier + 0.5f) / GABLE_TIERS)));
                }
            }

            BuildPostUnderside(width, depth, floor, At, terrain, timber, root);
            BuildPostRamp(At(POST_DOOR_X, halfDepth), rotation * Vector3.forward, floor, terrain, timber, root);
            return floor;
        }

        private static float RoofRise(float depth) => depth * 0.5f * Mathf.Tan(ROOF_PITCH * Mathf.Deg2Rad);

        private static void BuildHearth(float halfWidth, float depth, Material stone, Transform root)
        {
            const float hearthTop = 1.2f;
            float x = halfWidth - WALL_THICKNESS - 0.5f;
            float chimneyTop = POST_WALL_HEIGHT + RoofRise(depth) + 0.7f;
            Area1RemoteSites.Local("Hearth", root, stone, new Vector3(x, 0.6f, 0f), new Vector3(1f, hearthTop, 1.6f));
            Area1RemoteSites.Local("Chimney", root, stone, new Vector3(x, (hearthTop + chimneyTop) * 0.5f, 0f), new Vector3(0.7f, chimneyTop - hearthTop, 0.7f));
        }

        /// <summary>Posts along both long walls, with boards between them so nothing walks or bakes under the floor.</summary>
        private static void BuildPostUnderside(float width, float depth, float floor, System.Func<float, float, Vector2> at, Terrain terrain, Material timber, Transform root)
        {
            float halfWidth = width * 0.5f, halfDepth = depth * 0.5f;
            int index = 0;
            foreach (float z in new[] { -halfDepth, halfDepth })
                for (float x = -halfWidth; x <= halfWidth + 0.01f; x += POST_SPACING * 0.5f)
                    if (z < 0f || Mathf.Abs(x - POST_DOOR_X) >= (POST_RAMP_WIDTH + POST_WIDTH) * 0.5f) // the ramp meets the front wall here
                        Stilt($"Post_{index++}", at(x, z), floor, terrain, timber, root);

            (string name, Vector2 a, Vector2 b, Vector3 local, Vector2 size)[] sides =
            {
                ("Skirt_Front", at(-halfWidth, halfDepth), at(halfWidth, halfDepth), new Vector3(0f, 0f, halfDepth - SKIRT_THICKNESS), new Vector2(width, SKIRT_THICKNESS)),
                ("Skirt_Back", at(-halfWidth, -halfDepth), at(halfWidth, -halfDepth), new Vector3(0f, 0f, -halfDepth + SKIRT_THICKNESS), new Vector2(width, SKIRT_THICKNESS)),
                ("Skirt_Gate", at(-halfWidth, -halfDepth), at(-halfWidth, halfDepth), new Vector3(-halfWidth + SKIRT_THICKNESS, 0f, 0f), new Vector2(SKIRT_THICKNESS, depth)),
                ("Skirt_End", at(halfWidth, -halfDepth), at(halfWidth, halfDepth), new Vector3(halfWidth - SKIRT_THICKNESS, 0f, 0f), new Vector2(SKIRT_THICKNESS, depth)),
            };
            foreach ((string name, Vector2 a, Vector2 b, Vector3 local, Vector2 size) in sides)
            {
                Area1BlockoutBuilder.GroundUnder(terrain, a, b, out float low, out _, out _);
                float bottom = low - floor - POST_BURY, top = -0.3f;
                Area1RemoteSites.Local(name, root, timber, new Vector3(local.x, (top + bottom) * 0.5f, local.z), new Vector3(size.x, top - bottom, size.y));
            }
        }

        /// <summary>Lengthens until the slope drops to the limit, because the ground under the ramp keeps falling away.</summary>
        private static void BuildPostRamp(Vector2 door, Vector3 forward, float floor, Terrain terrain, Material timber, Transform root)
        {
            Vector2 outward = new Vector2(forward.x, forward.z);
            float limit = Mathf.Tan(POST_RAMP_MAX_ANGLE * Mathf.Deg2Rad);
            float length = POST_RAMP_MIN;
            bool TooSteep(float run) => Mathf.Abs(floor - Area1BlockoutBuilder.Ground(terrain, door + outward * run)) / run > limit;
            while (length < POST_RAMP_MAX && TooSteep(length))
                length += POST_RAMP_STEP;
            if (TooSteep(length))
                GameLog.Warn(TAG, $"House ramp stops at {POST_RAMP_MAX} m and is still steeper than {POST_RAMP_MAX_ANGLE} degrees.");

            Vector2 foot = door + outward * length;
            Slab("Ramp", new Vector3(door.x, floor, door.y), new Vector3(foot.x, Area1BlockoutBuilder.Ground(terrain, foot), foot.y), root, timber, POST_RAMP_WIDTH);
        }

        /// <summary>Closed for now: barred on all sides, with no door to open.</summary>
        private static void BuildLockup(Vector2 at, Quaternion rotation, Terrain terrain, Material timber, Transform root)
        {
            var cage = new GameObject("Lockup").transform;
            cage.SetParent(root, false);
            cage.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), rotation);

            float half = CAGE_SIZE * 0.5f;
            int bars = Mathf.CeilToInt(CAGE_SIZE / CAGE_BAR_GAP);
            for (int i = 0; i <= bars; i++)
            {
                float along = -half + CAGE_SIZE * i / bars;
                foreach (float side in new[] { -half, half })
                {
                    Area1RemoteSites.Local($"Bar_X_{i}_{side:0}", cage, timber, new Vector3(along, CAGE_HEIGHT * 0.5f - 0.4f, side), new Vector3(0.09f, CAGE_HEIGHT + 0.8f, 0.09f));
                    if (i > 0 && i < bars)
                        Area1RemoteSites.Local($"Bar_Z_{i}_{side:0}", cage, timber, new Vector3(side, CAGE_HEIGHT * 0.5f - 0.4f, along), new Vector3(0.09f, CAGE_HEIGHT + 0.8f, 0.09f));
                }
            }
            Area1RemoteSites.Local("Roof", cage, timber, new Vector3(0f, CAGE_HEIGHT, 0f), new Vector3(CAGE_SIZE + 0.3f, 0.14f, CAGE_SIZE + 0.3f));
        }

        /// <summary>Solid for now, with its hatch and counter facing the road.</summary>
        private static void BuildTallyShed(Vector2 at, Quaternion rotation, Terrain terrain, Material timber, Transform root)
        {
            const float width = 3f, depth = 2.5f, wall = 2.4f;
            var shed = new GameObject("TallyShed").transform;
            shed.SetParent(root, false);
            shed.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), rotation);

            Area1RemoteSites.Local("Body", shed, timber, new Vector3(0f, wall * 0.5f - 1f, 0f), new Vector3(width, wall + 2f, depth)); // the ground falls under the front
            Area1RemoteSites.Local("Body_Back", shed, timber, new Vector3(0f, wall + 0.15f, -depth * 0.25f), new Vector3(width, 0.3f, depth * 0.5f));
            Transform roof = Area1RemoteSites.Local("Roof", shed, timber, new Vector3(0f, wall + 0.2f, 0f), new Vector3(width + 0.6f, 0.14f, depth + 0.7f));
            roof.localRotation = Quaternion.Euler(10f, 0f, 0f);
            Area1RemoteSites.Local("Counter", shed, timber, new Vector3(0f, 1.05f, depth * 0.5f + 0.2f), new Vector3(1.8f, 0.1f, 0.45f));
            Area1RemoteSites.Local("Hatch", shed, timber, new Vector3(0f, 1.85f, depth * 0.5f + 0.3f), new Vector3(1.8f, 0.08f, 0.8f));
        }

        /// <summary>Flat boards over the mud, as in the Tent rows art. No colliders, so they never catch a foot; they are too thin to change a bake.</summary>
        internal static void BuildWalkway(string name, Vector2[] line, float width, Terrain terrain, Material timber, Transform root)
        {
            var walkway = new GameObject(name).transform;
            walkway.SetParent(root, false);
            int index = 0;
            foreach ((Vector2 a, Vector2 b) in Area1TerrainMath.CutPanels(line, BOARD_LENGTH, Vector2.zero, 0f))
            {
                Vector2 middle = (a + b) * 0.5f;
                Vector2 along = b - a;
                Transform board = Area1BlockoutBuilder.Box($"Board_{index++:000}", walkway, timber,
                    new Vector3(middle.x, Area1BlockoutBuilder.Ground(terrain, middle) + 0.03f, middle.y), new Vector3(width, 0.05f, along.magnitude - 0.1f));
                board.rotation = Area1BlockoutBuilder.Yaw(along);
                Object.DestroyImmediate(board.GetComponent<Collider>());
            }
        }
    }
}
