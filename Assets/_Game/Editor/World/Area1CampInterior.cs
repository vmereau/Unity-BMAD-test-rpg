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
        private const float WALL_HEIGHT = 4f;
        private const float WALL_THICKNESS = 0.4f;
        private const float DOOR_WIDTH = 2.4f;
        private const float DOOR_HEIGHT = 2.8f;
        private const float DOOR_X = 2f;          // from the warehouse centre, on both long walls
        private const float ROOF_PITCH = 30f;
        private const float LANDING_DEPTH = 3f;
        private const float RAMP_WIDTH = 2.4f;
        private const float RAMP_THICKNESS = 0.2f;
        private const float CAMP_RAMP_LENGTH = 9f;
        private const float POST_SPACING = 4f;
        private const float POST_BURY = 0.5f;
        private const int FLOOR_SAMPLES = 5;      // per side: the rim falls away, so one point can sit low

        private const float GATE_HEIGHT = 5f;
        private const float GATE_WIDTH = 3f;
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
            Vector2 riverEnd = new Vector2(center.x - 10f, rampZ);
            Slab("Ramp_River", new Vector3(center.x - 1f, floor, rampZ), new Vector3(riverEnd.x, Area1BlockoutBuilder.Ground(terrain, riverEnd), riverEnd.y), root, timber);

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

        private static void DoorWall(string name, float z, float halfWidth, Material timber, Transform root)
        {
            float leftEnd = DOOR_X - DOOR_WIDTH * 0.5f, rightStart = DOOR_X + DOOR_WIDTH * 0.5f;
            float leftLength = leftEnd + halfWidth, rightLength = halfWidth - rightStart;
            Area1RemoteSites.Local($"{name}_L", root, timber, new Vector3(-halfWidth + leftLength * 0.5f, WALL_HEIGHT * 0.5f, z), new Vector3(leftLength, WALL_HEIGHT, WALL_THICKNESS));
            Area1RemoteSites.Local($"{name}_R", root, timber, new Vector3(halfWidth - rightLength * 0.5f, WALL_HEIGHT * 0.5f, z), new Vector3(rightLength, WALL_HEIGHT, WALL_THICKNESS));
            Area1RemoteSites.Local($"{name}_Header", root, timber, new Vector3(DOOR_X, (DOOR_HEIGHT + WALL_HEIGHT) * 0.5f, z), new Vector3(DOOR_WIDTH, WALL_HEIGHT - DOOR_HEIGHT, WALL_THICKNESS));
        }

        /// <summary>A flat walkable board between two points, its top on the line.</summary>
        private static void Slab(string name, Vector3 from, Vector3 to, Transform root, Material timber)
        {
            Vector3 along = to - from;
            Quaternion rotation = Quaternion.LookRotation(along);
            Transform slab = Area1BlockoutBuilder.Box(name, root, timber, (from + to) * 0.5f - rotation * Vector3.up * (RAMP_THICKNESS * 0.5f),
                new Vector3(RAMP_WIDTH, RAMP_THICKNESS, along.magnitude));
            slab.rotation = rotation;
        }

        /// <summary>Stands open in the blockout, so the bank route stays walkable; restricting it is later gameplay.</summary>
        internal static void BuildDockGate(Vector2 gate, Terrain terrain, Material timber, Transform root)
        {
            float half = GATE_WIDTH * 0.5f;
            Vector2 south = gate + new Vector2(0f, -half), north = gate + new Vector2(0f, half);
            float southGround = Area1BlockoutBuilder.Ground(terrain, south), northGround = Area1BlockoutBuilder.Ground(terrain, north);
            float top = Mathf.Max(southGround, northGround) + GATE_HEIGHT; // the bank falls across the gate, so both posts reach one top
            Area1RemoteSites.Column("Post_South", root, timber, terrain, south, new Vector2(0.5f, 0.5f), top - southGround, 0f);
            Area1RemoteSites.Column("Post_North", root, timber, terrain, north, new Vector2(0.5f, 0.5f), top - northGround, 0f);
            Area1BlockoutBuilder.Box("Lintel", root, timber, new Vector3(gate.x, top, gate.y), new Vector3(0.35f, 0.35f, GATE_WIDTH + 0.8f));

            Area1RemoteSites.Column("Leaf_South", root, timber, terrain, gate + new Vector2(0.9f, -half - 0.1f), new Vector2(1.5f, 0.12f), 2.6f, -4f);
            Area1RemoteSites.Column("Leaf_North", root, timber, terrain, gate + new Vector2(0.9f, half + 0.1f), new Vector2(1.5f, 0.12f), 2.6f, 4f);

            Area1RemoteSites.BuildLeanTo("GuardPost", gate + new Vector2(3.2f, -1.9f), Vector2.up, terrain, timber, root); // long side along the bank
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

        /// <summary>Keeps south of the wood road, so carts and the road end stay clear.</summary>
        internal static void BuildTimberYard(Vector2 center, Terrain terrain, Material timber, Transform root)
        {
            Area1RemoteSites.BuildLogStack("Logs_0", center + new Vector2(-5f, -3f), 15f, terrain, timber, root);
            Area1RemoteSites.BuildLogStack("Logs_1", center + new Vector2(1f, -5f), 80f, terrain, timber, root);
            Area1RemoteSites.BuildSawPit(center + new Vector2(5f, -1f), terrain, timber, root);
            Area1RemoteSites.BuildLeanTo("ToolShelter", center + new Vector2(-8f, 2f), new Vector2(0.7f, 0.7f), terrain, timber, root);
            BuildCart("Cart_0", center + new Vector2(-1f, 2.5f), 30f, terrain, timber, root);
            BuildCart("Cart_1", center + new Vector2(8f, 3f), -40f, terrain, timber, root);
        }

        private static void BuildCart(string name, Vector2 at, float yaw, Terrain terrain, Material timber, Transform parent)
        {
            var cart = new GameObject(name).transform;
            cart.SetParent(parent, false);
            cart.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), Quaternion.Euler(0f, yaw, 0f));
            Area1RemoteSites.Local("Bed", cart, timber, new Vector3(0f, 0.7f, 0f), new Vector3(1.2f, 0.5f, 2f));
            Area1RemoteSites.Local("Wheel_L", cart, timber, new Vector3(-0.7f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.8f));
            Area1RemoteSites.Local("Wheel_R", cart, timber, new Vector3(0.7f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.8f));
            Area1RemoteSites.Local("Handle", cart, timber, new Vector3(0f, 0.6f, 1.6f), new Vector3(0.9f, 0.08f, 1.4f));
        }

        /// <summary>Keeps south of the quarry road.</summary>
        internal static void BuildWorkshopYard(Vector2 center, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Vector2 shopAt = center + new Vector2(1f, -4f);
            var shop = new GameObject("Workshop").transform;
            shop.SetParent(root, false);
            shop.position = new Vector3(shopAt.x, Area1BlockoutBuilder.Ground(terrain, shopAt), shopAt.y);
            foreach (float x in new[] { -3.3f, 3.3f })
                foreach (float z in new[] { -1.8f, 1.8f })
                    Area1RemoteSites.Local($"Post_{x:0}_{z:0}", shop, timber, new Vector3(x, 1.25f, z), new Vector3(0.2f, 3f, 0.2f));
            Area1RemoteSites.Local("Roof", shop, timber, new Vector3(0f, 2.82f, 0f), new Vector3(7.4f, 0.14f, 4.6f));
            Area1RemoteSites.Local("Bench", shop, timber, new Vector3(0f, 0.8f, -1.2f), new Vector3(5f, 0.15f, 0.8f));
            Area1RemoteSites.Local("Bench_Legs", shop, timber, new Vector3(0f, 0.35f, -1.2f), new Vector3(4.4f, 0.8f, 0.5f));

            Area1RemoteSites.BuildStoneStack("Stones_0", center + new Vector2(-7f, -3f), terrain, stone, root);
            Area1RemoteSites.BuildStoneStack("Stones_1", center + new Vector2(8f, -2f), terrain, stone, root);
            Area1RemoteSites.BuildStoneStack("Stones_2", center + new Vector2(9f, -5f), terrain, stone, root);
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
