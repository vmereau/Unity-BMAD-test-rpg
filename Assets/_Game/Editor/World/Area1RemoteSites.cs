using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// The graybox shapes of the sites away from the camp. <see cref="Area1SitesBuilder"/> owns the roots and validates the
    /// layout first; each site here is a set of plain boxes that art can replace one root at a time.
    /// </summary>
    internal static class Area1RemoteSites
    {
        private const int SEED = 2024;
        private const float BURY = 1f;           // boxes start below the ground; each samples one point, so this covers gentle ground only

        private const float TOWER_HEIGHT = 18f;  // the palisade hides y 40–44 here, so only the top 9–13 m reads from the tents
        private const float PIER_HEIGHT = 13f;
        private const float LINTEL_HEIGHT = 2f;
        private const float STUB_HEIGHT = 14f;
        private const float LANE = 2.5f;         // half-width kept open behind the arch
        private const float PROP_BURY = 0.25f;   // small props, on gentle ground
        private static readonly Vector2 TentsEye = new Vector2(15f, 28f);     // the arch faces it, so the opening reads from the tents
        private static readonly Vector2 ArchOffset = new Vector2(0f, 2f);     // turned, the piers would crowd the path point in front

        private const float SCAFFOLD_BAY = 3f;
        private const float SCAFFOLD_DEPTH = 1.6f;
        private const float SCAFFOLD_LEVEL = 2.5f;

        private const float CHAMBER_WIDTH = 10f;
        private const float CHAMBER_DEPTH = 8f;
        private const float CHAMBER_HEIGHT = 4f;
        private const float WALL = 1f;
        private const float MOUTH_WIDTH = 3f;
        private const float MOUTH_HEIGHT = 3.2f;
        private const float MOUTH_LENGTH = 6f;

        internal static void BuildRuins(Vector2 arch, Terrain terrain, Material stone, Transform root)
        {
            Column("Tower_West", root, stone, terrain, arch + new Vector2(-8f, 0f), new Vector2(4f, 4f), TOWER_HEIGHT, 8f);
            Column("Tower_East", root, stone, terrain, arch + new Vector2(7f, 1f), new Vector2(4f, 4f), TOWER_HEIGHT, -6f);
            Column("Stub_East", root, stone, terrain, arch + new Vector2(13f, 0f), new Vector2(3f, 3.5f), STUB_HEIGHT, 12f);

            // Square to the tents' line, or the 3 m deep piers would close the opening from there.
            Vector2 archAt = arch + ArchOffset;
            Vector2 facing = (archAt - TentsEye).normalized;
            Vector2 across = new Vector2(facing.y, -facing.x);
            Quaternion archYaw = Area1BlockoutBuilder.Yaw(facing);
            Vector2 pierA = archAt - across * 3.5f, pierB = archAt + across * 3.5f;
            float pierTop = Mathf.Max(Area1BlockoutBuilder.Ground(terrain, pierA), Area1BlockoutBuilder.Ground(terrain, pierB)) + PIER_HEIGHT;
            Pier("Pier_West", pierA, pierTop, archYaw, terrain, stone, root);
            Pier("Pier_East", pierB, pierTop, archYaw, terrain, stone, root);
            Transform lintel = Area1BlockoutBuilder.Box("Lintel", root, stone, new Vector3(archAt.x, pierTop + LINTEL_HEIGHT * 0.5f, archAt.y), new Vector3(10f, LINTEL_HEIGHT, 3f));
            lintel.rotation = archYaw;

            (string name, Vector2 local, Vector2 size, float height, float yaw)[] walls =
            {
                ("Wall_0", new Vector2(-12f, 5f), new Vector2(1.2f, 7f), 3.5f, 70f),
                ("Wall_1", new Vector2(-7f, 7f), new Vector2(1.2f, 5f), 2.5f, 95f),
                ("Wall_2", new Vector2(7f, 6f), new Vector2(1.2f, 6f), 3f, 85f),
                ("Wall_3", new Vector2(15f, 5f), new Vector2(1.2f, 5f), 2f, 60f),
            };
            foreach ((string name, Vector2 local, Vector2 size, float height, float yaw) in walls)
                Column(name, root, stone, terrain, arch + local, size, height, yaw);

            var random = new System.Random(SEED);
            float Range(float from, float to) => from + (float)random.NextDouble() * (to - from);
            for (int i = 0; i < 12; i++)
            {
                float x = Range(-15f, 15f);
                if (Mathf.Abs(x) < LANE) x += Mathf.Sign(x) * LANE;
                float size = Range(0.6f, 1.6f);
                Vector2 at = arch + new Vector2(x, Range(3f, 10f));
                Transform block = Area1BlockoutBuilder.Box($"Rubble_{i:00}", root, stone,
                    new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at) + size * 0.2f, at.y), new Vector3(size, size * 0.7f, size * Range(0.8f, 1.5f)));
                block.rotation = Quaternion.Euler(Range(0f, 25f), Range(0f, 360f), Range(0f, 25f));
            }
        }

        /// <summary>Everything keeps to the faces, so the road end, the pad's centre and the cleft stay open.</summary>
        internal static void BuildQuarry(Vector2 center, Terrain terrain, Material timber, Material stone, Transform root)
        {
            BuildScaffold("Scaffold_South_0", center + new Vector2(-23f, -18.5f), Vector2.right, 15f, 10f, terrain, timber, root);
            BuildScaffold("Scaffold_South_1", center + new Vector2(-3f, -18.5f), Vector2.right, 15f, 7.5f, terrain, timber, root);
            BuildScaffold("Scaffold_East", center + new Vector2(26.5f, -8f), Vector2.up, 15f, 10f, terrain, timber, root);
            BuildCrane(center + new Vector2(19f, 4f), terrain, timber, root);

            Vector2[] stacks = { new Vector2(-18f, -12f), new Vector2(-10f, -11f), new Vector2(4f, -12f), new Vector2(21f, 12f) };
            for (int i = 0; i < stacks.Length; i++) BuildStoneStack($"Stones_{i}", center + stacks[i], terrain, stone, root);

            BuildSledge(center + new Vector2(-4f, -7f), 0f, terrain, timber, stone, root);
        }

        private static void BuildScaffold(string name, Vector2 start, Vector2 along, float length, float height, Terrain terrain, Material timber, Transform parent)
        {
            var scaffold = new GameObject(name).transform;
            scaffold.SetParent(parent, false);
            float ground = Area1BlockoutBuilder.Ground(terrain, start);
            scaffold.SetPositionAndRotation(new Vector3(start.x, ground, start.y), Area1BlockoutBuilder.Yaw(along));

            int bays = Mathf.RoundToInt(length / SCAFFOLD_BAY);
            float bay = length / bays;
            for (int i = 0; i <= bays; i++)
            {
                foreach (float x in new[] { 0f, SCAFFOLD_DEPTH })
                    Local($"Post_{i:00}_{x:0}", scaffold, timber, new Vector3(x, (height - BURY) * 0.5f, i * bay), new Vector3(0.2f, height + BURY, 0.2f));
            }

            int level = 0;
            for (float y = SCAFFOLD_LEVEL; y <= height; y += SCAFFOLD_LEVEL)
            {
                Local($"Deck_{level}", scaffold, timber, new Vector3(SCAFFOLD_DEPTH * 0.5f, y, length * 0.5f), new Vector3(SCAFFOLD_DEPTH, 0.08f, length));
                if (y + 1f <= height) Local($"Ledger_{level}", scaffold, timber, new Vector3(0f, y + 1f, length * 0.5f), new Vector3(0.1f, 0.1f, length));
                level++;
            }

            float brace = Mathf.Sqrt(bay * bay + SCAFFOLD_LEVEL * SCAFFOLD_LEVEL);
            float tilt = Mathf.Atan2(bay, SCAFFOLD_LEVEL) * Mathf.Rad2Deg;
            for (int i = 0; i < bays; i += 2)
            {
                Transform diagonal = Local($"Brace_{i:00}", scaffold, timber, new Vector3(-0.12f, SCAFFOLD_LEVEL * 0.5f, (i + 0.5f) * bay), new Vector3(0.1f, brace, 0.1f));
                diagonal.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            }

            Local("Ladder", scaffold, timber, new Vector3(-0.25f, (height - BURY) * 0.5f, 0.9f), new Vector3(0.1f, height + BURY, 0.6f));
        }

        private static void BuildCrane(Vector2 at, Terrain terrain, Material timber, Transform parent)
        {
            var crane = new GameObject("Crane").transform;
            crane.SetParent(parent, false);
            crane.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), Quaternion.Euler(0f, 250f, 0f));
            Local("Base", crane, timber, new Vector3(0f, 0.15f, 0f), new Vector3(3f, 0.3f, 3f));
            Local("Mast", crane, timber, new Vector3(0f, 6f, 0f), new Vector3(0.45f, 12f, 0.45f));
            Transform boom = Local("Boom", crane, timber, new Vector3(0f, 10.5f, 3.6f), new Vector3(0.3f, 0.3f, 9f));
            boom.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            Thin("Rope", crane, timber, new Vector3(0f, 8f, 8f), new Vector3(0.05f, 7f, 0.05f));
        }

        internal static void BuildSledge(Vector2 at, float yaw, Terrain terrain, Material timber, Material stone, Transform parent)
        {
            var sledge = new GameObject("Sledge").transform;
            sledge.SetParent(parent, false);
            sledge.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), Quaternion.Euler(0f, yaw, 0f));
            Local("Runner_L", sledge, timber, new Vector3(-0.6f, 0.1f, 0f), new Vector3(0.2f, 0.2f, 2.8f));
            Local("Runner_R", sledge, timber, new Vector3(0.6f, 0.1f, 0f), new Vector3(0.2f, 0.2f, 2.8f));
            Local("Bed", sledge, timber, new Vector3(0f, 0.25f, 0f), new Vector3(1.6f, 0.1f, 2.6f));
            Local("Block", sledge, stone, new Vector3(0f, 0.75f, 0f), new Vector3(1.3f, 0.9f, 2f));
        }

        internal static void BuildStoneStack(string name, Vector2 at, Terrain terrain, Material stone, Transform parent)
        {
            var stack = new GameObject(name).transform;
            stack.SetParent(parent, false);
            stack.position = new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y);
            for (int row = 0; row < 3; row++)
            {
                for (int i = 0; i < 3 - row; i++)
                    Local($"Block_{row}_{i}", stack, stone, new Vector3((i - (2 - row) * 0.5f) * 1.5f, 0.35f + row * 0.8f, 0f), new Vector3(1.4f, 0.8f, 1.1f));
            }
        }

        /// <summary>Stays within 10 m of the centre, where the pad is flat, and clear of the line in from the cleft.</summary>
        internal static void BuildRuins2(Vector2 center, Terrain terrain, Material stone, Transform root)
        {
            (string name, Vector2 local, Vector2 size, float height, float yaw)[] pieces =
            {
                ("Wall_West", new Vector2(-3.9f, -7.5f), new Vector2(1f, 4.2f), 6f, 90f),
                ("Wall_East", new Vector2(3.9f, -7.5f), new Vector2(1f, 4.2f), 4.5f, 90f),
                ("Door", new Vector2(0f, -7.6f), new Vector2(0.6f, 3.4f), 4f, 90f),
                ("Pillar_0", new Vector2(6.5f, 6f), new Vector2(1.6f, 1.6f), 7f, 15f),
                ("Pillar_1", new Vector2(8.5f, -2f), new Vector2(1.6f, 1.6f), 5f, 0f),
                ("Pillar_2", new Vector2(-6f, -4f), new Vector2(1.6f, 1.6f), 7f, 30f),
                ("Altar", new Vector2(0f, -3f), new Vector2(2.6f, 1.6f), 1f, 0f),
            };
            foreach ((string name, Vector2 local, Vector2 size, float height, float yaw) in pieces)
                Column(name, root, stone, terrain, center + local, size, height, yaw);

            Vector2 fallen = center + new Vector2(4f, 3f);
            Transform column = Area1BlockoutBuilder.Box("FallenColumn", root, stone,
                new Vector3(fallen.x, Area1BlockoutBuilder.Ground(terrain, fallen) + 0.6f, fallen.y), new Vector3(1.3f, 1.3f, 6f));
            column.rotation = Quaternion.Euler(0f, 40f, 0f);
        }

        /// <summary>Everything rings the centre, so the road end and the path to the centre stay open.</summary>
        internal static void BuildWoodCamp(Vector2 center, Terrain terrain, Material timber, Transform root)
        {
            BuildLogStack("Logs_0", center + new Vector2(-12f, 8f), 20f, terrain, timber, root);
            BuildLogStack("Logs_1", center + new Vector2(-16f, -6f), 75f, terrain, timber, root);
            BuildLogStack("Logs_2", center + new Vector2(14f, -14f), 0f, terrain, timber, root);
            BuildLeanTo("Shelter_0", center + new Vector2(-6f, 16f), Vector2.down, terrain, timber, root);
            BuildLeanTo("Shelter_1", center + new Vector2(8f, 15f), Vector2.down, terrain, timber, root);

            BuildSawPit(center + new Vector2(12f, -6f), terrain, timber, root);

            var random = new System.Random(SEED);
            float Range(float from, float to) => from + (float)random.NextDouble() * (to - from);
            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f + Range(-0.2f, 0.2f);
                float reach = Range(22f, 32f);
                Vector2 at = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                if (at.x > center.x + 26f) at.x = center.x + 26f; // short of the road end
                Column($"Stump_{i}", root, timber, terrain, at, new Vector2(0.8f, 0.8f), 0.6f, Range(0f, 90f));
            }
        }

        internal static void BuildSawPit(Vector2 at, Terrain terrain, Material timber, Transform parent)
        {
            var saw = new GameObject("SawPit").transform;
            saw.SetParent(parent, false);
            saw.position = new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y);
            Local("Trestle_0", saw, timber, new Vector3(0f, (1.2f - PROP_BURY) * 0.5f, -2.2f), new Vector3(1.6f, 1.2f + PROP_BURY, 0.25f));
            Local("Trestle_1", saw, timber, new Vector3(0f, (1.2f - PROP_BURY) * 0.5f, 2.2f), new Vector3(1.6f, 1.2f + PROP_BURY, 0.25f));
            Local("Log", saw, timber, new Vector3(0f, 1.45f, 0f), new Vector3(0.6f, 0.5f, 6f));
        }

        internal static void BuildLogStack(string name, Vector2 at, float yaw, Terrain terrain, Material timber, Transform parent)
        {
            var stack = new GameObject(name).transform;
            stack.SetParent(parent, false);
            stack.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), Quaternion.Euler(0f, yaw, 0f));
            for (int row = 0; row < 3; row++)
            {
                for (int i = 0; i < 4 - row; i++)
                    Local($"Log_{row}_{i}", stack, timber, new Vector3((i - (3 - row) * 0.5f) * 0.6f, 0.2f + row * 0.5f, 0f), new Vector3(0.5f, 0.5f, 6f));
            }
        }

        internal static void BuildHuntingCamp(Vector2 center, Terrain terrain, Material timber, Material stone, Transform root)
        {
            BuildLeanTo("Shelter", center + new Vector2(6f, 7f), new Vector2(-0.7f, -0.7f), terrain, timber, root);

            Vector2 fireAt = center + new Vector2(-3f, 4f);
            var fire = new GameObject("FireRing").transform;
            fire.SetParent(root, false);
            fire.position = new Vector3(fireAt.x, Area1BlockoutBuilder.Ground(terrain, fireAt), fireAt.y);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                Local($"Stone_{i}", fire, stone, new Vector3(Mathf.Cos(angle) * 0.8f, 0.12f, Mathf.Sin(angle) * 0.8f), new Vector3(0.35f, 0.25f, 0.35f));
            }

            BuildHideRack("HideRack_0", center + new Vector2(-8f, -3f), 20f, terrain, timber, root);
            BuildHideRack("HideRack_1", center + new Vector2(-6f, 9f), -35f, terrain, timber, root);

            Vector2 pole = center + new Vector2(10f, 0f);
            var meat = new GameObject("MeatPole").transform;
            meat.SetParent(root, false);
            meat.position = new Vector3(pole.x, Area1BlockoutBuilder.Ground(terrain, pole), pole.y);
            Local("Post_0", meat, timber, new Vector3(-1.5f, (2.2f - PROP_BURY) * 0.5f, 0f), new Vector3(0.14f, 2.2f + PROP_BURY, 0.14f));
            Local("Post_1", meat, timber, new Vector3(1.5f, (2.2f - PROP_BURY) * 0.5f, 0f), new Vector3(0.14f, 2.2f + PROP_BURY, 0.14f));
            Local("Bar", meat, timber, new Vector3(0f, 2.15f, 0f), new Vector3(3.2f, 0.1f, 0.1f));
        }

        private static void BuildHideRack(string name, Vector2 at, float yaw, Terrain terrain, Material timber, Transform parent)
        {
            var rack = new GameObject(name).transform;
            rack.SetParent(parent, false);
            rack.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), Quaternion.Euler(0f, yaw, 0f));
            Local("Post_0", rack, timber, new Vector3(-1f, (1.9f - PROP_BURY) * 0.5f, 0f), new Vector3(0.12f, 1.9f + PROP_BURY, 0.12f));
            Local("Post_1", rack, timber, new Vector3(1f, (1.9f - PROP_BURY) * 0.5f, 0f), new Vector3(0.12f, 1.9f + PROP_BURY, 0.12f));
            Local("Hide", rack, timber, new Vector3(0f, 1.15f, 0f), new Vector3(1.8f, 1.2f, 0.04f));
        }

        internal static void BuildLeanTo(string name, Vector2 at, Vector2 facing, Terrain terrain, Material timber, Transform parent)
        {
            var shelter = new GameObject(name).transform;
            shelter.SetParent(parent, false);
            shelter.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), Area1BlockoutBuilder.Yaw(facing));
            Local("PostA", shelter, timber, new Vector3(-1.6f, 0.65f, 0.8f), new Vector3(0.15f, 1.8f, 0.15f));
            Local("PostB", shelter, timber, new Vector3(1.6f, 0.65f, 0.8f), new Vector3(0.15f, 1.8f, 0.15f));
            Local("PostC", shelter, timber, new Vector3(-1.6f, 0.3f, -0.8f), new Vector3(0.15f, 1.1f, 0.15f));
            Local("PostD", shelter, timber, new Vector3(1.6f, 0.3f, -0.8f), new Vector3(0.15f, 1.1f, 0.15f));
            Transform roof = Local("Roof", shelter, timber, new Vector3(0f, 1.2f, 0f), new Vector3(3.6f, 0.1f, 2.2f));
            roof.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            Local("Bedroll", shelter, timber, new Vector3(0f, 0.1f, -0.2f), new Vector3(0.8f, 0.2f, 1.9f));
        }

        /// <summary>
        /// A box chamber sits on the ground, with no terrain holes, and rocks wrap it. The rocks stay outside the walls even at
        /// their worst yaw, so the walls alone set the interior. Local +Z runs out of the mouth, towards the camp.
        /// </summary>
        internal static void BuildCave(Vector2 center, Terrain terrain, Material stone, GameObject rockTall, GameObject rockLow, Transform root)
        {
            float ground = Area1BlockoutBuilder.Ground(terrain, center);
            root.SetPositionAndRotation(new Vector3(center.x, ground, center.y), Area1BlockoutBuilder.Yaw(-center));

            float halfWidth = CHAMBER_WIDTH * 0.5f, halfDepth = CHAMBER_DEPTH * 0.5f;
            float wallHeight = CHAMBER_HEIGHT + WALL + BURY;
            float wallY = wallHeight * 0.5f - BURY;
            float frontPiece = halfWidth + WALL - MOUTH_WIDTH * 0.5f;
            float frontX = MOUTH_WIDTH * 0.5f + frontPiece * 0.5f;
            float frontZ = halfDepth + WALL * 0.5f;
            float mouthWallX = (MOUTH_WIDTH + WALL) * 0.5f;
            float mouthZ = halfDepth + MOUTH_LENGTH * 0.5f;

            var chamber = new GameObject("Chamber").transform;
            chamber.SetParent(root, false);
            Local("Wall_Back", chamber, stone, new Vector3(0f, wallY, -frontZ), new Vector3(CHAMBER_WIDTH + WALL * 2f, wallHeight, WALL));
            Local("Wall_West", chamber, stone, new Vector3(-halfWidth - WALL * 0.5f, wallY, 0f), new Vector3(WALL, wallHeight, CHAMBER_DEPTH));
            Local("Wall_East", chamber, stone, new Vector3(halfWidth + WALL * 0.5f, wallY, 0f), new Vector3(WALL, wallHeight, CHAMBER_DEPTH));
            Local("Wall_Front_L", chamber, stone, new Vector3(-frontX, wallY, frontZ), new Vector3(frontPiece, wallHeight, WALL));
            Local("Wall_Front_R", chamber, stone, new Vector3(frontX, wallY, frontZ), new Vector3(frontPiece, wallHeight, WALL));
            Local("Wall_Header", chamber, stone, new Vector3(0f, (MOUTH_HEIGHT + CHAMBER_HEIGHT + WALL) * 0.5f, frontZ),
                new Vector3(MOUTH_WIDTH, CHAMBER_HEIGHT + WALL - MOUTH_HEIGHT, WALL));
            Local("Ceiling", chamber, stone, new Vector3(0f, CHAMBER_HEIGHT + WALL * 0.5f, 0f), new Vector3(CHAMBER_WIDTH + WALL * 2f, WALL, CHAMBER_DEPTH + WALL * 2f));

            float mouthWallHeight = MOUTH_HEIGHT + WALL + BURY;
            Local("Mouth_West", chamber, stone, new Vector3(-mouthWallX, mouthWallHeight * 0.5f - BURY, mouthZ), new Vector3(WALL, mouthWallHeight, MOUTH_LENGTH));
            Local("Mouth_East", chamber, stone, new Vector3(mouthWallX, mouthWallHeight * 0.5f - BURY, mouthZ), new Vector3(WALL, mouthWallHeight, MOUTH_LENGTH));
            Local("Mouth_Roof", chamber, stone, new Vector3(0f, MOUTH_HEIGHT + WALL * 0.5f, mouthZ), new Vector3(MOUTH_WIDTH + WALL * 2f, WALL, MOUTH_LENGTH));

            var random = new System.Random(SEED);
            float Range(float from, float to) => from + (float)random.NextDouble() * (to - from);
            int index = 0;
            void Rock(Vector3 local, GameObject prefab, Vector3 scale)
            {
                Vector3 world = root.TransformPoint(local);
                Vector2 flat = new Vector2(world.x, world.z);
                world.y = Area1BlockoutBuilder.Ground(terrain, flat) + local.y;
                Area1SitesBuilder.PlaceRock(prefab, $"Rock_{index++:00}", root, world, Range(0f, 360f), scale);
            }

            var tall = new Vector3(1.4f, 1f, 1.3f);
            for (float x = -8.5f; x <= 8.6f; x += 3.4f) Rock(new Vector3(x, -0.5f, -8.5f), rockTall, tall);
            for (int row = 0; row < 4; row++)
            {
                float z = -5f + row * 3.2f;
                Rock(new Vector3(-9f, -0.5f, z), rockTall, tall);
                Rock(new Vector3(9f, -0.5f, z), rockTall, tall);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Rock(new Vector3(side * 6.5f, -0.5f, 8.5f), rockTall, tall);
                Rock(new Vector3(side * 9.5f, -0.5f, 7.5f), rockTall, tall);
                Rock(new Vector3(side * 5f, 0.4f, 11.5f), rockLow, Vector3.one);
            }

            var cover = new Vector3(3f, 1.6f, 3f);
            foreach (Vector3 top in new[]
                     {
                         new Vector3(-4f, 6f, -2f), new Vector3(0f, 6f, -2.5f), new Vector3(4f, 6f, -2f), new Vector3(-3.5f, 6f, 2.5f),
                         new Vector3(1f, 6f, 2f), new Vector3(4.5f, 6f, 3f), new Vector3(0f, 6f, 7f),
                     })
            {
                Rock(top, rockLow, cover);
            }
        }

        private static void Pier(string name, Vector2 at, float top, Quaternion yaw, Terrain terrain, Material stone, Transform parent)
        {
            float ground = Area1BlockoutBuilder.Ground(terrain, at);
            Transform pier = Area1BlockoutBuilder.Box(name, parent, stone, new Vector3(at.x, (top + ground - BURY) * 0.5f, at.y), new Vector3(3f, top - ground + BURY, 3f));
            pier.rotation = yaw;
        }

        internal static void Column(string name, Transform parent, Material material, Terrain terrain, Vector2 at, Vector2 footprint, float height, float yaw)
        {
            float ground = Area1BlockoutBuilder.Ground(terrain, at);
            Transform box = Area1BlockoutBuilder.Box(name, parent, material, new Vector3(at.x, ground + (height - BURY) * 0.5f, at.y),
                new Vector3(footprint.x, height + BURY, footprint.y));
            box.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        internal static Transform Local(string name, Transform parent, Material material, Vector3 local, Vector3 size)
        {
            Transform box = Area1BlockoutBuilder.Box(name, parent, material, Vector3.zero, size);
            box.localPosition = local;
            box.localRotation = Quaternion.identity;
            return box;
        }

        internal static Transform Thin(string name, Transform parent, Material material, Vector3 local, Vector3 size)
        {
            Transform box = Local(name, parent, material, local, size);
            Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }
    }
}
