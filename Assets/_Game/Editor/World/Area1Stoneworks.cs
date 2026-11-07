using Unity.AI.Navigation;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// The graybox yard inside the quarry opening, from the Camp carry entrance art. Stone comes in through the opening, is weighed
    /// and cut here, then goes up the hoists or on through the inner gate. Living space backs on the inner palisade; work sits
    /// south of the road. <see cref="Area1SitesBuilder"/> owns the root and validates the layout first.
    /// </summary>
    internal static class Area1Stoneworks
    {
        private const float LODGE_HALF_WIDTH = 6f;
        private const float LODGE_HALF_DEPTH = 4f;
        private const float LODGE_WALL = 0.5f;
        private const float LODGE_HEIGHT = 3f;
        private const float LODGE_ROOM_EAST = -0.5f; // the room's east wall; everything east of it is the open cutting bay
        private const float LODGE_LOW_WALL = 1.2f;   // the unfinished east side
        private const float LODGE_PITCH = 30f;
        private const float LODGE_DOOR_WIDTH = 1.4f;
        private const float LODGE_DOOR_HEIGHT = 2.3f;
        private const float LODGE_DOOR_Z = 1.8f;     // towards the road, clear of the banker
        private const float BAY_ROOF_BACK = 3.05f;   // resting on the back wall
        private const float BAY_ROOF_FRONT = 2.7f;
        private const float BURY = 0.3f;

        // Measured against the 5 m palisade: a higher ridge shortens the gap a jump from a roof has to clear.
        private const float HUT_WIDTH = 4f;
        private const float HUT_DEPTH = 3.5f;
        private const float HUT_WALL = 1.95f;
        private const float HUT_PITCH = 25f;
        private const float HUT_EAVE = 0.2f;
        private const float ROOF_THICKNESS = 0.1f;
        private const int HUTS = 5;
        private const float HUT_SPACING = 5f;

        private const float CRANE_HEIGHT = 6f;
        private const float CRANE_ARM = 4f;
        private const float LOAD_DROP = 2.6f;  // below the arm, over head height, so nothing hangs in the track
        private const float RIM_FRAME_HEIGHT = 2.6f;
        private const float RIM_FRAME_HALF_WIDTH = 0.8f;
        private const float ROPE = 0.06f;

        private const float PEN_LENGTH = 14f;
        private const float PEN_WIDTH = 4.8f;
        private const float PEN_RAIL_HEIGHT = 1f;
        private const float PEN_POST_SPACING = 3.5f;
        private const float PEN_NAV_HEIGHT = 3f;
        private const float PEN_NAV_MARGIN = 0.35f;   // Not Walkable areas aren't eroded, so this keeps an agent off the rails
        private const int NOT_WALKABLE_AREA = 1;

        // In the east-north plane from the row's centre. All of it stays north of the road's 4 m lane.
        private static readonly Vector2 ToolStoreOffset = new Vector2(13.9f, -1f); // clears the last hut's eave
        private static readonly Vector2 BeamOffset = new Vector2(6.5f, -4.2f);
        private static readonly Vector2 SledgeOffset = new Vector2(1.5f, -3.7f);
        private const float SLEDGE_YAW = 96f; // the road's bearing here, so it parks along the road

        private static readonly Vector2 TroughOffset = new Vector2(-2.9f, -0.7f);
        private static readonly Vector2 BoardOffset = new Vector2(0f, -2.6f);

        private static readonly Vector2[] RubbleBlocks =
        {
            new Vector2(0f, 0f), new Vector2(1.6f, 1f), new Vector2(-1.5f, 1.2f), new Vector2(1f, -1.4f),
            new Vector2(-1.2f, -1.5f), new Vector2(2.5f, -0.4f), new Vector2(-2.6f, 0f), new Vector2(0.2f, 2.3f),
            new Vector2(0.1f, -2.5f), new Vector2(2.3f, 1.8f), new Vector2(-2.4f, 1.9f), new Vector2(-2.2f, -2f),
        };

        internal static void BuildLodge(Vector2 center, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Transform lodge = Group("Lodge", root, center, 0f, terrain);
            float wallY = (LODGE_HEIGHT - BURY) * 0.5f;
            float wallSize = LODGE_HEIGHT + BURY;
            float roomWidth = LODGE_ROOM_EAST + LODGE_HALF_WIDTH;
            float roomMiddle = (LODGE_ROOM_EAST - LODGE_HALF_WIDTH) * 0.5f;

            Area1RemoteSites.Local("Wall_South", lodge, stone, new Vector3(0f, wallY, -LODGE_HALF_DEPTH + LODGE_WALL * 0.5f), new Vector3(LODGE_HALF_WIDTH * 2f, wallSize, LODGE_WALL));
            Area1RemoteSites.Local("Wall_West", lodge, stone, new Vector3(-LODGE_HALF_WIDTH + LODGE_WALL * 0.5f, wallY, 0f), new Vector3(LODGE_WALL, wallSize, LODGE_HALF_DEPTH * 2f));
            Area1RemoteSites.Local("Wall_Front", lodge, stone, new Vector3(roomMiddle, wallY, LODGE_HALF_DEPTH - LODGE_WALL * 0.5f), new Vector3(roomWidth, wallSize, LODGE_WALL));
            float doorSouth = LODGE_DOOR_Z - LODGE_DOOR_WIDTH * 0.5f;
            float doorNorth = LODGE_DOOR_Z + LODGE_DOOR_WIDTH * 0.5f;
            Area1RemoteSites.Local("Wall_Room_East_S", lodge, stone, new Vector3(LODGE_ROOM_EAST, wallY, (doorSouth - LODGE_HALF_DEPTH) * 0.5f), new Vector3(LODGE_WALL, wallSize, doorSouth + LODGE_HALF_DEPTH));
            Area1RemoteSites.Local("Wall_Room_East_N", lodge, stone, new Vector3(LODGE_ROOM_EAST, wallY, (doorNorth + LODGE_HALF_DEPTH) * 0.5f), new Vector3(LODGE_WALL, wallSize, LODGE_HALF_DEPTH - doorNorth));
            Area1RemoteSites.Local("Wall_Room_East_Header", lodge, stone, new Vector3(LODGE_ROOM_EAST, (LODGE_DOOR_HEIGHT + LODGE_HEIGHT) * 0.5f, LODGE_DOOR_Z), new Vector3(LODGE_WALL, LODGE_HEIGHT - LODGE_DOOR_HEIGHT, LODGE_DOOR_WIDTH));
            Area1RemoteSites.Local("Wall_East_Low", lodge, stone, new Vector3(LODGE_HALF_WIDTH - LODGE_WALL * 0.5f, (LODGE_LOW_WALL - BURY) * 0.5f, -1.25f),
                new Vector3(LODGE_WALL, LODGE_LOW_WALL + BURY, 5.5f));

            GableRoof(lodge, timber, new Vector3(roomMiddle, LODGE_HEIGHT, 0f), roomWidth + 0.8f, LODGE_HALF_DEPTH, HUT_EAVE * 2f, LODGE_PITCH);
            foreach (float x in new[] { -LODGE_HALF_WIDTH + LODGE_WALL * 0.5f, LODGE_ROOM_EAST })
                for (int tier = 0; tier < 4; tier++)
                    Area1RemoteSites.Local($"Gable_{x:0}_{tier}", lodge, stone, new Vector3(x, LODGE_HEIGHT + 0.25f + tier * 0.5f, 0f), new Vector3(LODGE_WALL, 0.5f, 7f - tier * 1.8f));

            // Open bay: a plain lean-to roof on two posts, with the banker in the middle.
            float bayMiddle = (LODGE_ROOM_EAST + LODGE_HALF_WIDTH) * 0.5f;
            float postZ = LODGE_HALF_DEPTH - 0.2f;
            float tilt = Mathf.Atan2(BAY_ROOF_BACK - BAY_ROOF_FRONT, LODGE_HALF_DEPTH * 2f) * Mathf.Rad2Deg;
            float postTop = Mathf.Lerp(BAY_ROOF_BACK, BAY_ROOF_FRONT, (postZ + LODGE_HALF_DEPTH) / (LODGE_HALF_DEPTH * 2f));
            foreach (float x in new[] { bayMiddle, LODGE_HALF_WIDTH - 0.2f })
                Area1RemoteSites.Local($"Bay_Post_{x:0}", lodge, timber, new Vector3(x, (postTop - BURY) * 0.5f, postZ), new Vector3(0.25f, postTop + BURY, 0.25f));
            Transform bayRoof = Area1RemoteSites.Local("Bay_Roof", lodge, timber, new Vector3(bayMiddle + 0.25f, (BAY_ROOF_BACK + BAY_ROOF_FRONT) * 0.5f, 0f),
                new Vector3(LODGE_HALF_WIDTH - LODGE_ROOM_EAST + 0.5f, ROOF_THICKNESS, LODGE_HALF_DEPTH * 2f + 0.4f));
            bayRoof.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            Area1RemoteSites.Local("Banker", lodge, timber, new Vector3(bayMiddle, 0.35f, -1.8f), new Vector3(2.6f, 0.9f, 0.9f));
            Area1RemoteSites.Local("Banker_Block", lodge, stone, new Vector3(bayMiddle, 1.1f, -1.8f), new Vector3(1.2f, 0.6f, 0.7f));

            Area1RemoteSites.BuildStoneStack("Stones_0", center + new Vector2(-9.5f, 2.5f), terrain, stone, root);
            Area1RemoteSites.BuildStoneStack("Stones_1", center + new Vector2(-9.5f, -1.5f), terrain, stone, root);
            Area1RemoteSites.BuildStoneStack("Stones_2", center + new Vector2(-2f, -5.5f), terrain, stone, root);
        }

        internal static void BuildMasonsRow(Vector2 center, Terrain terrain, Material timber, Material stone, Transform root)
        {
            for (int i = 0; i < HUTS; i++)
                BuildHut($"Hut_{i}", center + new Vector2((i - (HUTS - 1) * 0.5f) * HUT_SPACING, 0f), terrain, timber, root);

            Transform store = Group("ToolStore", root, center + ToolStoreOffset, 0f, terrain);
            Area1RemoteSites.Local("Walls", store, timber, new Vector3(0f, 0.95f, 0f), new Vector3(3f, 2.2f, 2.5f));
            Transform roof = Area1RemoteSites.Local("Roof", store, timber, new Vector3(0f, 2.14f, 0f), new Vector3(3.5f, ROOF_THICKNESS, 3f));
            roof.localRotation = Quaternion.Euler(4f, 0f, 0f);
            Area1RemoteSites.Local("Counter", store, timber, new Vector3(0f, 1.05f, -1.45f), new Vector3(1.8f, 0.1f, 0.45f));
            Area1RemoteSites.Thin("Hatch", store, timber, new Vector3(0f, 1.6f, -1.28f), new Vector3(1.6f, 0.8f, 0.05f));

            Transform beam = Group("WeighingBeam", root, center + BeamOffset, 0f, terrain);
            foreach (float x in new[] { -1.4f, 1.4f })
                Area1RemoteSites.Local($"Post_{x:0}", beam, timber, new Vector3(x, 1.2f, 0f), new Vector3(0.25f, 3f, 0.25f));
            Area1RemoteSites.Local("Crossbar", beam, timber, new Vector3(0f, 2.75f, 0f), new Vector3(3.4f, 0.2f, 0.2f));
            Area1RemoteSites.Thin("Arm", beam, timber, new Vector3(0f, 2.4f, 0f), new Vector3(2.6f, 0.12f, 0.12f));
            foreach (float x in new[] { -1f, 1f })
                Area1RemoteSites.Thin($"Hanger_{x:0}", beam, timber, new Vector3(x, 1.35f, 0f), new Vector3(ROPE, 2.05f, ROPE));
            Area1RemoteSites.Local("Pan", beam, timber, new Vector3(0f, 0.35f, 0f), new Vector3(2.2f, 0.08f, 1.2f));

            Area1RemoteSites.BuildSledge(center + SledgeOffset, SLEDGE_YAW, terrain, timber, stone, root);
        }

        internal static void BuildTallyPost(Vector2 center, Terrain terrain, Material timber, Material stone, Transform root)
        {
            BuildHut("TallyHut", center, terrain, timber, root);

            Vector2 troughAt = center + TroughOffset;
            Area1RemoteSites.Column("WashTrough", root, stone, terrain, troughAt, new Vector2(0.9f, 2.2f), 0.6f, 0f);

            Transform board = Group("TallyBoard", root, center + BoardOffset, 0f, terrain);
            foreach (float x in new[] { -1f, 1f })
                Area1RemoteSites.Local($"Post_{x:0}", board, timber, new Vector3(x, 0.8f, 0f), new Vector3(0.15f, 2.2f, 0.15f));
            Area1RemoteSites.Local("Board", board, timber, new Vector3(0f, 1.45f, 0f), new Vector3(2.2f, 1.1f, 0.08f));
        }

        /// <summary>
        /// Scenery: points are foot crane, its rim frame, outcrop crane, its rim frame. The foot crane stands in the yard and the second on the
        /// outcrop, as in the art. Ropes and loads have no colliders, and each load hangs over head height, so the side track stays clear.
        /// </summary>
        internal static void BuildHoists(Vector2[] line, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Transform hoists = new GameObject("Hoists").transform;
            hoists.SetParent(root, false);
            for (int i = 0; i + 1 < line.Length; i += 2)
            {
                Vector2 toRim = line[i + 1] - line[i];
                float yaw = Mathf.Atan2(toRim.x, toRim.y) * Mathf.Rad2Deg;
                Transform crane = Group($"Crane_{i / 2}", hoists, line[i], yaw, terrain);
                Area1RemoteSites.Local("Mast", crane, timber, new Vector3(0f, (CRANE_HEIGHT - BURY) * 0.5f, 0f), new Vector3(0.35f, CRANE_HEIGHT + BURY, 0.35f));
                Area1RemoteSites.Local("Base", crane, timber, new Vector3(0f, 0.15f, 0f), new Vector3(2.4f, 0.3f, 2.4f));
                foreach (float x in new[] { -0.9f, 0.9f })
                {
                    Transform brace = Area1RemoteSites.Local($"Brace_{x:0}", crane, timber, new Vector3(x * 0.5f, 1.5f, 0f), new Vector3(0.18f, 3.2f, 0.18f));
                    brace.localRotation = Quaternion.Euler(0f, 0f, x > 0f ? 17f : -17f);
                }

                // The arm swings away from the rim.
                Area1RemoteSites.Local("Arm", crane, timber, new Vector3(0f, CRANE_HEIGHT - 0.5f, -CRANE_ARM * 0.5f + 0.5f), new Vector3(0.25f, 0.25f, CRANE_ARM + 1f));
                float armY = CRANE_HEIGHT - 0.5f;
                float loadY = armY - LOAD_DROP;
                Area1RemoteSites.Thin("Hook_Rope", crane, timber, new Vector3(0f, (armY + loadY) * 0.5f, -CRANE_ARM + 0.2f), new Vector3(ROPE, armY - loadY, ROPE));
                Area1RemoteSites.Thin("Load", crane, stone, new Vector3(0f, loadY, -CRANE_ARM + 0.2f), new Vector3(0.7f, 0.5f, 0.7f));

                // Each post stands on its own ground: the rim falls away under a frame this wide.
                Vector2 side = new Vector2(toRim.y, -toRim.x).normalized * RIM_FRAME_HALF_WIDTH;
                float rimGround = Mathf.Max(Area1BlockoutBuilder.Ground(terrain, line[i + 1] + side), Area1BlockoutBuilder.Ground(terrain, line[i + 1] - side));
                float rimTop = rimGround + RIM_FRAME_HEIGHT;
                Transform frame = Group($"RimFrame_{i / 2}", hoists, line[i + 1], yaw, terrain);
                foreach (float sign in new[] { -1f, 1f })
                {
                    Vector2 foot = line[i + 1] + side * sign;
                    Area1RemoteSites.Column($"Post_{sign:0}", frame, timber, terrain, foot, new Vector2(0.22f, 0.22f), rimTop - Area1BlockoutBuilder.Ground(terrain, foot), yaw);
                }

                Area1BlockoutBuilder.Box("Bar", frame, timber, new Vector3(line[i + 1].x, rimTop, line[i + 1].y), new Vector3(RIM_FRAME_HALF_WIDTH * 2f + 0.4f, 0.22f, 0.22f)).rotation = Quaternion.Euler(0f, yaw, 0f);

                Vector3 mastTop = crane.TransformPoint(new Vector3(0f, CRANE_HEIGHT, 0f));
                Rope($"Rope_{i / 2}", hoists, timber, mastTop, new Vector3(line[i + 1].x, rimTop, line[i + 1].y));
            }
        }

        /// <summary>Local +X runs along the palisade and +Z faces the yard, with the south side left open to the wall.</summary>
        internal static void BuildOxPen(Vector2 center, float yaw, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Transform pen = Group("OxPen", root, center, yaw, terrain);
            float halfLength = PEN_LENGTH * 0.5f;
            float halfWidth = PEN_WIDTH * 0.5f;

            int bays = Mathf.CeilToInt(PEN_LENGTH / PEN_POST_SPACING);
            for (int i = 0; i <= bays; i++)
                Area1RemoteSites.Local($"Post_{i}", pen, timber, new Vector3(-halfLength + PEN_LENGTH * i / bays, (PEN_RAIL_HEIGHT - BURY) * 0.5f, halfWidth), new Vector3(0.18f, PEN_RAIL_HEIGHT + BURY, 0.18f));
            foreach (float x in new[] { -halfLength, halfLength })
                Area1RemoteSites.Local($"Post_Back_{x:0}", pen, timber, new Vector3(x, (PEN_RAIL_HEIGHT - BURY) * 0.5f, -halfWidth), new Vector3(0.18f, PEN_RAIL_HEIGHT + BURY, 0.18f));
            foreach (float height in new[] { 0.55f, 0.95f })
            {
                Area1RemoteSites.Local($"Rail_Front_{height:0.0}", pen, timber, new Vector3(0f, height, halfWidth), new Vector3(PEN_LENGTH, 0.1f, 0.08f));
                foreach (float x in new[] { -halfLength, halfLength })
                    Area1RemoteSites.Local($"Rail_End_{x:0}_{height:0.0}", pen, timber, new Vector3(x, height, 0f), new Vector3(0.08f, 0.1f, PEN_WIDTH));
            }

            // As in the art: the open lean-to at the crane end, the closed feed store at the opening end.
            float shelterX = -halfLength + 2.5f;
            const float roofPitch = 12f;
            const float roofY = 2f;
            const float roofZ = -0.4f;
            Transform shelter = Area1RemoteSites.Local("Shelter_Roof", pen, timber, new Vector3(shelterX, roofY, roofZ), new Vector3(4.6f, ROOF_THICKNESS, 3.8f));
            shelter.localRotation = Quaternion.Euler(-roofPitch, 0f, 0f);
            foreach (float x in new[] { shelterX - 2.1f, shelterX + 2.1f })
                foreach (float z in new[] { -2f, 1.2f })
                {
                    float top = roofY + (z - roofZ) * Mathf.Tan(roofPitch * Mathf.Deg2Rad);
                    Area1RemoteSites.Local($"Shelter_Post_{x:0}_{z:0}", pen, timber, new Vector3(x, (top - BURY) * 0.5f, z), new Vector3(0.18f, top + BURY, 0.18f));
                }

            Area1RemoteSites.Local("Feed_Store", pen, timber, new Vector3(halfLength - 1.2f, 0.75f, -0.6f), new Vector3(2f, 1.8f, 2f));
            Area1RemoteSites.Local("Trough", pen, stone, new Vector3(-1f, 0.2f, 1.6f), new Vector3(2.2f, 0.55f, 0.7f));
            for (int i = 0; i < 2; i++)
                Area1RemoteSites.Local($"Ox_{i}", pen, timber, new Vector3(-1.5f + i * 3.5f, 0.75f, -0.5f + i * 0.6f), new Vector3(2.2f, 1.2f, 0.9f));

            // A bake can't see the rails, and an agent has no reason to walk in.
            var volume = new GameObject("NotWalkable_Pen").transform;
            volume.SetParent(pen, false);
            volume.localPosition = Vector3.zero; // half below the ground sample, so a slope under the pen stays inside it
            var notWalkable = volume.gameObject.AddComponent<NavMeshModifierVolume>();
            notWalkable.center = Vector3.zero;
            notWalkable.size = new Vector3(PEN_LENGTH + PEN_NAV_MARGIN * 2f, PEN_NAV_HEIGHT, PEN_WIDTH + PEN_NAV_MARGIN * 2f);
            notWalkable.area = NOT_WALKABLE_AREA;
        }

        /// <summary>Stacks low so the heap never gives a step up the wall beside it. Fixed offsets: the same layout gives the same heap.</summary>
        internal static void BuildRubble(Vector2 center, Terrain terrain, Material stone, Transform root)
        {
            Transform heap = new GameObject("RubbleTip").transform;
            heap.SetParent(root, false);
            for (int i = 0; i < RubbleBlocks.Length; i++)
            {
                float size = 1.6f - i * 0.05f;
                float height = i < 4 ? 1.2f : 0.7f;
                Area1RemoteSites.Column($"Rock_{i}", heap, stone, terrain, center + RubbleBlocks[i], new Vector2(size, size * 0.8f), height, i * 37f);
            }
        }

        private static void BuildHut(string name, Vector2 at, Terrain terrain, Material timber, Transform root)
        {
            Transform hut = Group(name, root, at, 0f, terrain);
            Area1RemoteSites.Local("Walls", hut, timber, new Vector3(0f, (HUT_WALL - BURY) * 0.5f, 0f), new Vector3(HUT_WIDTH, HUT_WALL + BURY, HUT_DEPTH));
            GableRoof(hut, timber, new Vector3(0f, HUT_WALL, 0f), HUT_WIDTH + 0.4f, HUT_DEPTH * 0.5f, HUT_EAVE, HUT_PITCH);
            for (int tier = 0; tier < 3; tier++)
                Area1RemoteSites.Local($"Gable_{tier}", hut, timber, new Vector3(0f, HUT_WALL + 0.14f + tier * 0.28f, 0f), new Vector3(HUT_WIDTH, 0.28f, HUT_DEPTH - 0.7f - tier * 1.1f));
            Area1RemoteSites.Thin("Door", hut, timber, new Vector3(0f, 0.85f, -HUT_DEPTH * 0.5f - 0.03f), new Vector3(1f, 1.7f, 0.05f));
        }

        /// <summary>Roof panels sit on the wall tops at <paramref name="wallCentre"/>, with an eave past them that drops below the wall line.</summary>
        internal static void GableRoof(Transform parent, Material timber, Vector3 wallCentre, float length, float halfWall, float eave, float pitch)
        {
            float slopeRise = Mathf.Tan(pitch * Mathf.Deg2Rad);
            float halfSpan = halfWall + eave;
            float slope = halfSpan / Mathf.Cos(pitch * Mathf.Deg2Rad);
            float middleY = (halfWall - eave) * slopeRise * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                Transform panel = Area1RemoteSites.Local(side < 0f ? "Roof_South" : "Roof_North", parent, timber,
                    wallCentre + new Vector3(0f, middleY, side * halfSpan * 0.5f), new Vector3(length, ROOF_THICKNESS, slope));
                panel.localRotation = Quaternion.Euler(side * pitch, 0f, 0f);
            }
        }

        internal static Transform Group(string name, Transform parent, Vector2 at, float yaw, Terrain terrain)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), Quaternion.Euler(0f, yaw, 0f));
            return group;
        }

        private static void Rope(string name, Transform parent, Material material, Vector3 from, Vector3 to)
        {
            Transform rope = Area1BlockoutBuilder.Box(name, parent, material, (from + to) * 0.5f, new Vector3(ROPE, ROPE, Vector3.Distance(from, to)));
            rope.rotation = Quaternion.LookRotation(to - from);
            Object.DestroyImmediate(rope.GetComponent<Collider>());
        }
    }
}
