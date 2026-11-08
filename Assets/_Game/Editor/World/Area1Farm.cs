using Unity.AI.Navigation;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// The graybox Company farm on the plain behind the fort hill. The field gang comes out through the ramp gate, past the manager's
    /// house, to a yard ringed by the tithe barn, threshing floor, ox mill and byre, with strip fields beyond. Nothing here has
    /// gameplay: the barn is a closed room for later theft quests. <see cref="Area1SitesBuilder"/> owns the root and validates the
    /// layout first.
    /// </summary>
    internal static class Area1Farm
    {
        /// <summary>What a field grows. Sets the row height, or haycocks for the meadow.</summary>
        internal enum Crop { Wheat, Barley, Oats, Hay, Garden }

        private const float BURY = 0.3f;
        private const float WALL = 0.4f;
        private const float POST = 0.25f;
        private const int GABLE_TIERS = 4;
        private const int NOT_WALKABLE_AREA = 1;

        private const float HOUSE_DOOR_X = -2f;      // towards the ramp gate end
        private const float HOUSE_RAMP = 1.5f;       // flat ground: a short step, clear of the track

        private const float BARN_WALL = 4.5f;
        private const float BARN_PLINTH = 0.8f;
        private const float BARN_PITCH = 35f;
        private const float BARN_DOOR_WIDTH = 4f;    // a loaded cart's width
        private const float BARN_DOOR_HEIGHT = 3.6f;

        private const float THRESH_POST = 3f;
        private const float THRESH_PITCH = 20f;

        private const int MILL_POSTS = 8;
        private const float MILL_EAVE = 3.2f;
        private const float MILL_SWEEP = 3.6f;       // the ox walks a ring inside the posts
        private const float MILL_SWEEP_HEIGHT = 1.3f;

        private const float BYRE_BACK = 3.2f;        // the roof falls towards the open front
        private const float BYRE_FRONT = 2.5f;
        private const int BYRE_STALLS = 4;

        private const float PADDOCK_RAIL = 1.1f;
        private const float PADDOCK_POST_SPACING = 3f;
        private const float PADDOCK_NAV_MARGIN = 0.4f;
        private const float PADDOCK_NAV_HEIGHT = 4f;

        private const float FENCE_HEIGHT = 0.9f;     // a jump clears it; the gaps are the way in
        private const float FENCE_THICKNESS = 0.12f;
        private const float FENCE_GAP = 3f;
        private const float FENCE_PIECE = 4f;        // short pieces follow the ground
        private const float ROW_INSET = 1f;
        private const float ROW_SPACING = 1.4f;
        private const float ROW_PIECE = 6f;
        private const float HAYCOCK_SPACING = 4f;

        /// <summary>The Company reeve's house: one room, with the farm's books on a desk and a hearth at the far end.</summary>
        internal static void BuildManagerHouse(Area1Garrison.Plot plot, Terrain terrain, Material timber, Material stone, Transform root)
        {
            var house = new GameObject("ManagerHouse").transform;
            house.SetParent(root, false);
            Area1CampInterior.BuildHouseShell(plot.Center, plot.Width, plot.Depth, Quaternion.Euler(0f, plot.Yaw, 0f), "West", terrain, timber, house, HOUSE_DOOR_X, HOUSE_RAMP);

            float halfWidth = plot.Width * 0.5f, halfDepth = plot.Depth * 0.5f;
            Area1RemoteSites.Local("Bed", house, timber, new Vector3(halfWidth - 1.6f, 0.3f, -halfDepth + 1.2f), new Vector3(2f, 0.6f, 1.1f));
            Area1RemoteSites.Local("Chest", house, timber, new Vector3(halfWidth - 3.2f, 0.3f, -halfDepth + 0.7f), new Vector3(1f, 0.6f, 0.6f));
            Area1RemoteSites.Local("Table", house, timber, new Vector3(0.5f, 0.4f, 0f), new Vector3(1.8f, 0.8f, 1f));
            Area1RemoteSites.Local("Desk", house, timber, new Vector3(-halfWidth + 1.2f, 0.4f, -halfDepth + 0.9f), new Vector3(1.4f, 0.8f, 0.8f));
            Area1RemoteSites.Local("Shelf_Tallies", house, timber, new Vector3(-halfWidth + 0.6f, 1.1f, 0.8f), new Vector3(0.4f, 2.2f, 1.6f));
            Area1RemoteSites.Local("Hearth", house, stone, new Vector3(halfWidth - 0.8f, 0.6f, 1.2f), new Vector3(1f, 1.2f, 1.4f));
            float chimneyTop = 3f + halfDepth * Mathf.Tan(30f * Mathf.Deg2Rad) + 0.7f;
            Area1RemoteSites.Local("Chimney", house, stone, new Vector3(halfWidth - 0.8f, (1.2f + chimneyTop) * 0.5f, 1.2f), new Vector3(0.7f, chimneyTop - 1.2f, 0.7f));
        }

        /// <summary>Stone plinth, tall timber walls and a steep roof, closed on every side. The cart doors on local +Z are shut and solid.</summary>
        internal static void BuildTitheBarn(Area1Garrison.Plot plot, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Transform barn = Area1Stoneworks.Group("TitheBarn", root, plot.Center, plot.Yaw, terrain);
            float halfWidth = plot.Width * 0.5f, halfDepth = plot.Depth * 0.5f;

            foreach (float side in new[] { -1f, 1f })
            {
                Area1RemoteSites.Local(side < 0f ? "Plinth_Back" : "Plinth_Front", barn, stone, new Vector3(0f, (BARN_PLINTH - BURY) * 0.5f, side * (halfDepth - WALL * 0.5f)), new Vector3(plot.Width + 0.1f, BARN_PLINTH + BURY, WALL + 0.1f));
                Area1RemoteSites.Local(side < 0f ? "Plinth_West" : "Plinth_East", barn, stone, new Vector3(side * (halfWidth - WALL * 0.5f), (BARN_PLINTH - BURY) * 0.5f, 0f), new Vector3(WALL + 0.1f, BARN_PLINTH + BURY, plot.Depth + 0.1f));
                Area1RemoteSites.Local(side < 0f ? "Wall_Back" : "Wall_Front", barn, timber, new Vector3(0f, (BARN_PLINTH + BARN_WALL) * 0.5f, side * (halfDepth - WALL * 0.5f)), new Vector3(plot.Width, BARN_WALL - BARN_PLINTH, WALL));
                Area1RemoteSites.Local(side < 0f ? "Wall_West" : "Wall_East", barn, timber, new Vector3(side * (halfWidth - WALL * 0.5f), (BARN_PLINTH + BARN_WALL) * 0.5f, 0f), new Vector3(WALL, BARN_WALL - BARN_PLINTH, plot.Depth));
                Tiers(side < 0f ? "Gable_West" : "Gable_East", barn, timber, side * (halfWidth - WALL * 0.5f), BARN_WALL, plot.Depth, BARN_PITCH);
            }

            Area1Stoneworks.GableRoof(barn, timber, new Vector3(0f, BARN_WALL, 0f), plot.Width + 0.8f, halfDepth, 0.5f, BARN_PITCH);

            float doorZ = halfDepth + 0.09f; // in front of the plinth's face at +0.05, so neither fights the other
            foreach (float side in new[] { -1f, 1f })
                Area1RemoteSites.Thin(side < 0f ? "Door_West" : "Door_East", barn, timber, new Vector3(side * BARN_DOOR_WIDTH * 0.25f, BARN_DOOR_HEIGHT * 0.5f, doorZ), new Vector3(BARN_DOOR_WIDTH * 0.5f - 0.05f, BARN_DOOR_HEIGHT, 0.06f));
            Area1RemoteSites.Thin("Door_Bar", barn, stone, new Vector3(0f, 1.6f, doorZ + 0.06f), new Vector3(BARN_DOOR_WIDTH + 0.4f, 0.15f, 0.08f));
            Area1RemoteSites.Thin("Door_Lintel", barn, timber, new Vector3(0f, BARN_DOOR_HEIGHT + 0.15f, doorZ), new Vector3(BARN_DOOR_WIDTH + 0.6f, 0.3f, 0.1f));

            Area1CampInterior.BuildCart("Cart", Outside(plot, -halfWidth + 2f, halfDepth + 2.5f), plot.Yaw + 90f, terrain, timber, root);
            for (int i = 0; i < 3; i++)
                Area1RemoteSites.Column($"Sack_{i}", root, timber, terrain, Outside(plot, halfWidth - 1.2f - i * 0.8f, halfDepth + 0.8f), new Vector2(0.6f, 0.5f), 0.7f, i * 20f);
        }

        /// <summary>A stone floor under an open roof, sheaves stacked at one end and flails leaning on a post.</summary>
        internal static void BuildThreshingFloor(Area1Garrison.Plot plot, Terrain terrain, Material timber, Material stone, Material crop, Transform root)
        {
            Transform floor = Area1Stoneworks.Group("ThreshingFloor", root, plot.Center, plot.Yaw, terrain);
            float halfWidth = plot.Width * 0.5f, halfDepth = plot.Depth * 0.5f;

            Area1RemoteSites.Local("Floor", floor, stone, new Vector3(0f, -0.1f, 0f), new Vector3(plot.Width, 0.4f, plot.Depth));
            foreach (float x in new[] { -halfWidth + 0.3f, 0f, halfWidth - 0.3f })
                foreach (float z in new[] { -halfDepth + 0.3f, halfDepth - 0.3f })
                    Post($"Post_{x:0}_{z:0}", floor, timber, x, z, THRESH_POST, POST);
            Area1Stoneworks.GableRoof(floor, timber, new Vector3(0f, THRESH_POST, 0f), plot.Width + 0.8f, halfDepth, 0.4f, THRESH_PITCH);

            for (int i = 0; i < 4; i++)
                Area1RemoteSites.Thin($"Sheaf_{i}", floor, crop, new Vector3(halfWidth - 1f, 0.5f, -halfDepth + 1f + i * 1.2f), new Vector3(1.2f, 0.9f, 1f));
            for (int i = 0; i < 3; i++)
            {
                Transform flail = Area1RemoteSites.Thin($"Flail_{i}", floor, timber, new Vector3(-halfWidth + 0.55f, 0.85f, -halfDepth + 0.6f + i * 0.25f), new Vector3(0.06f, 1.8f, 0.06f));
                flail.localRotation = Quaternion.Euler(0f, 0f, -12f);
            }
        }

        /// <summary>
        /// An open round shed. The ox walks a ring inside the posts, pulling the sweep that turns the shaft and the stone at the centre.
        /// <paramref name="plot"/> width is the diameter.
        /// </summary>
        internal static void BuildOxMill(Area1Garrison.Plot plot, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Transform mill = Area1Stoneworks.Group("OxMill", root, plot.Center, plot.Yaw, terrain);
            float radius = plot.Width * 0.5f - 0.2f;

            for (int i = 0; i < MILL_POSTS; i++)
            {
                float angle = (i + 0.5f) * 360f / MILL_POSTS * Mathf.Deg2Rad;
                Post($"Post_{i}", mill, timber, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, MILL_EAVE, POST);
            }

            // Two squares turned 45 degrees read as an octagon; three shrinking pairs read as a cone.
            for (int tier = 0; tier < 3; tier++)
            {
                float size = (radius * 2f + 1f) * (1f - tier * 0.3f);
                foreach (float turn in new[] { 0f, 45f })
                    Area1RemoteSites.Local($"Roof_{tier}_{turn:0}", mill, timber, new Vector3(0f, MILL_EAVE + 0.15f + tier * 0.5f, 0f), new Vector3(size, 0.3f, size)).localRotation = Quaternion.Euler(0f, turn, 0f);
            }

            Area1RemoteSites.Local("Stone_Bed", mill, stone, new Vector3(0f, 0.3f, 0f), new Vector3(1.8f, 0.6f, 1.8f));
            Area1RemoteSites.Local("Stone_Runner", mill, stone, new Vector3(0f, 0.75f, 0f), new Vector3(1.5f, 0.3f, 1.5f)).localRotation = Quaternion.Euler(0f, 30f, 0f);
            Area1RemoteSites.Local("Shaft", mill, timber, new Vector3(0f, MILL_EAVE * 0.5f, 0f), new Vector3(0.3f, MILL_EAVE, 0.3f));
            Area1RemoteSites.Thin("Sweep", mill, timber, new Vector3(MILL_SWEEP * 0.5f, MILL_SWEEP_HEIGHT, 0f), new Vector3(MILL_SWEEP, 0.15f, 0.15f));
            Area1RemoteSites.Local("Ox", mill, timber, new Vector3(MILL_SWEEP, 0.75f, -0.4f), new Vector3(0.9f, 1.2f, 2.2f)); // walks the ring, side-on to the sweep
            foreach (int i in new[] { 0, 1 })
                Area1RemoteSites.Local($"MealBin_{i}", mill, timber, new Vector3(-1.6f, 0.45f, -0.7f + i * 1.4f), new Vector3(0.8f, 0.9f, 1f));
        }

        /// <summary>Back and end walls, a lean-to roof falling to the open front on local +Z, and a stall for each ox.</summary>
        internal static void BuildByre(Area1Garrison.Plot plot, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Transform byre = Area1Stoneworks.Group("OxByre", root, plot.Center, plot.Yaw, terrain);
            float halfWidth = plot.Width * 0.5f, halfDepth = plot.Depth * 0.5f;

            Area1RemoteSites.Local("Wall_Back", byre, timber, new Vector3(0f, (BYRE_BACK - BURY) * 0.5f, -halfDepth + WALL * 0.5f), new Vector3(plot.Width, BYRE_BACK + BURY, WALL));
            foreach (float side in new[] { -1f, 1f })
                Area1RemoteSites.Local(side < 0f ? "Wall_West" : "Wall_East", byre, timber, new Vector3(side * (halfWidth - WALL * 0.5f), (BYRE_FRONT - BURY) * 0.5f, 0f), new Vector3(WALL, BYRE_FRONT + BURY, plot.Depth));
            for (float x = -halfWidth + 0.3f; x <= halfWidth - 0.29f; x += (plot.Width - 0.6f) / BYRE_STALLS)
                Post($"Post_{x:0}", byre, timber, x, halfDepth - 0.2f, BYRE_FRONT, POST);

            float pitch = Mathf.Atan2(BYRE_BACK - BYRE_FRONT, plot.Depth) * Mathf.Rad2Deg;
            float slope = plot.Depth / Mathf.Cos(pitch * Mathf.Deg2Rad) + 0.8f;
            Area1RemoteSites.Local("Roof", byre, timber, new Vector3(0f, (BYRE_BACK + BYRE_FRONT) * 0.5f + 0.1f, 0f), new Vector3(plot.Width + 0.6f, 0.2f, slope)).localRotation = Quaternion.Euler(pitch, 0f, 0f);

            float stall = (plot.Width - WALL * 2f) / BYRE_STALLS;
            Area1RemoteSites.Local("Manger", byre, timber, new Vector3(0f, 0.4f, -halfDepth + WALL + 0.35f), new Vector3(plot.Width - WALL * 2f, 0.8f, 0.6f));
            for (int i = 0; i < BYRE_STALLS; i++)
            {
                float x = -halfWidth + WALL + stall * (i + 0.5f);
                if (i > 0)
                    Area1RemoteSites.Local($"Stall_{i}", byre, timber, new Vector3(x - stall * 0.5f, 0.7f, -halfDepth + 1.6f), new Vector3(0.12f, 1.4f, 2.6f));
                Area1RemoteSites.Local($"Ox_{i}", byre, timber, new Vector3(x, 0.75f, -halfDepth + WALL + 0.7f + 1.2f), new Vector3(0.9f, 1.2f, 2.2f)); // head just clear of the manger
            }
        }

        /// <summary>Railed all round, with the cows and calves inside. A bake can't see the rails, so a volume keeps agents out.</summary>
        internal static void BuildPaddock(Area1Garrison.Plot plot, Terrain terrain, Material timber, Transform root)
        {
            Transform paddock = Area1Stoneworks.Group("OxPaddock", root, plot.Center, plot.Yaw, terrain);
            float halfWidth = plot.Width * 0.5f, halfDepth = plot.Depth * 0.5f;

            foreach (float side in new[] { -1f, 1f })
            {
                RailSide(side < 0f ? "South" : "North", paddock, timber, new Vector3(-halfWidth, 0f, side * halfDepth), new Vector3(halfWidth, 0f, side * halfDepth), true);
                RailSide(side < 0f ? "West" : "East", paddock, timber, new Vector3(side * halfWidth, 0f, -halfDepth), new Vector3(side * halfWidth, 0f, halfDepth), false); // the corners are the long sides' posts
            }

            Area1RemoteSites.Local("Cow_0", paddock, timber, new Vector3(-4f, 0.7f, -1f), new Vector3(2f, 1.1f, 0.8f));
            Area1RemoteSites.Local("Cow_1", paddock, timber, new Vector3(2.5f, 0.7f, 1.2f), new Vector3(0.8f, 1.1f, 2f));
            Area1RemoteSites.Local("Calf_0", paddock, timber, new Vector3(-2.2f, 0.45f, -1.6f), new Vector3(1.2f, 0.7f, 0.5f));
            Area1RemoteSites.Local("Calf_1", paddock, timber, new Vector3(4f, 0.45f, 1.8f), new Vector3(0.5f, 0.7f, 1.2f));
            Area1RemoteSites.Local("Trough", paddock, timber, new Vector3(0f, 0.25f, halfDepth - 0.8f), new Vector3(2.4f, 0.5f, 0.6f));

            var volume = new GameObject("NotWalkable_Paddock").transform;
            volume.SetParent(paddock, false);
            var notWalkable = volume.gameObject.AddComponent<NavMeshModifierVolume>();
            notWalkable.center = Vector3.zero;
            notWalkable.size = new Vector3(plot.Width + PADDOCK_NAV_MARGIN * 2f, PADDOCK_NAV_HEIGHT, plot.Depth + PADDOCK_NAV_MARGIN * 2f);
            notWalkable.area = NOT_WALKABLE_AREA;
        }

        /// <summary>Three stacks and an open hay shed: fodder for both herds.</summary>
        internal static void BuildHayYard(Vector2 at, Terrain terrain, Material timber, Material crop, Transform root)
        {
            Transform yard = Area1Stoneworks.Group("HayYard", root, at, 0f, terrain);
            for (int i = 0; i < 3; i++)
                Area1RemoteSites.Local($"Stack_{i}", yard, crop, new Vector3(-3f + i * 3f, 1.1f, 2.5f), new Vector3(2.4f, 2.8f, 2.4f)).localRotation = Quaternion.Euler(0f, i * 25f, 0f);

            foreach (float x in new[] { -2.2f, 2.2f })
                foreach (float z in new[] { -1.6f, 0.4f })
                    Post($"Post_{x:0}_{z:0}", yard, timber, x, z, 3f, POST);
            Area1RemoteSites.Local("Roof", yard, timber, new Vector3(0f, 3.1f, -0.6f), new Vector3(5.2f, 0.2f, 3f));
            Area1RemoteSites.Local("Hay", yard, crop, new Vector3(0f, 0.8f, -0.6f), new Vector3(4f, 1.6f, 1.8f));
        }

        internal static void BuildDungHeap(Vector2 at, Terrain terrain, Material timber, Transform root)
        {
            Transform heap = Area1Stoneworks.Group("DungHeap", root, at, 20f, terrain);
            Area1RemoteSites.Local("Heap", heap, timber, new Vector3(0f, 0.2f, 0f), new Vector3(3.2f, 1f, 2.6f));
            Area1RemoteSites.Local("Heap_Top", heap, timber, new Vector3(0.2f, 0.8f, 0f), new Vector3(1.8f, 0.4f, 1.6f));
            Area1RemoteSites.Thin("Fork", heap, timber, new Vector3(1.4f, 1f, 0.8f), new Vector3(0.06f, 1.6f, 0.06f)).localRotation = Quaternion.Euler(20f, 0f, 0f);
        }

        internal static void BuildWell(Vector2 at, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Transform well = Area1Stoneworks.Group("Well", root, at, 0f, terrain);
            Area1RemoteSites.Local("Ring", well, stone, new Vector3(0f, 0.25f, 0f), new Vector3(1.4f, 1.1f, 1.4f));
            foreach (int side in new[] { -1, 1 })
                Post($"Post_{side}", well, timber, side * 0.8f, 0f, 2.2f, 0.15f);
            Area1RemoteSites.Thin("Windlass", well, timber, new Vector3(0f, 1.9f, 0f), new Vector3(1.6f, 0.12f, 0.12f));
            Area1RemoteSites.Local("Trough", well, stone, new Vector3(0f, 0.2f, 1.6f), new Vector3(2.2f, 0.55f, 0.7f));
        }

        /// <summary>Where the gang is counted out in the morning and the tools are counted back at dusk.</summary>
        internal static void BuildTally(Vector2 at, Terrain terrain, Material timber, Transform root)
        {
            Transform tally = Area1Stoneworks.Group("FieldTally", root, at, 90f, terrain);
            foreach (float x in new[] { -0.7f, 0.7f })
                Post($"Board_Post_{x:0}", tally, timber, x, 0f, 2f, 0.15f);
            Area1RemoteSites.Thin("Board", tally, timber, new Vector3(0f, 1.4f, 0.1f), new Vector3(1.6f, 0.9f, 0.06f));

            foreach (float x in new[] { 1.5f, 3.5f })
                Post($"Rack_Post_{x:0}", tally, timber, x, 0f, 1.6f, 0.12f);
            Area1RemoteSites.Thin("Rack_Bar", tally, timber, new Vector3(2.5f, 1.4f, 0f), new Vector3(2.2f, 0.08f, 0.08f));
            for (int i = 0; i < 5; i++)
                Area1RemoteSites.Thin($"Tool_{i}", tally, timber, new Vector3(1.8f + i * 0.35f, 0.8f, 0.12f), new Vector3(0.05f, 1.5f, 0.05f)).localRotation = Quaternion.Euler(-10f, 0f, 0f);
            Area1RemoteSites.Local("WaterBarrel", tally, timber, new Vector3(-1.6f, 0.45f, 0.4f), new Vector3(0.7f, 0.9f, 0.7f));
        }

        /// <summary>
        /// A low wattle fence with one gap, then crop rows along the field's length. The gap is on <paramref name="gate"/> (a unit
        /// axis vector) when set, otherwise on the side facing <paramref name="gapToward"/>.
        /// Crops have no colliders and stay out of the bake, so the gang and agents walk through them.
        /// </summary>
        internal static void BuildField(string name, Vector2 center, Vector2 size, Crop crop, Vector2? gate, Vector2 gapToward, Terrain terrain, Material timber, Material cropMaterial, Transform root)
        {
            var field = new GameObject(name).transform;
            field.SetParent(root, false);
            Vector2 half = size * 0.5f;

            // The side whose outward normal points most at the target, scaled so a long field still gets its gap on the near side.
            Vector2 toward = gapToward - center;
            bool eastWest = Mathf.Abs(toward.x) / half.x >= Mathf.Abs(toward.y) / half.y;
            Vector2 gapSide = gate ?? (eastWest ? new Vector2(Mathf.Sign(toward.x), 0f) : new Vector2(0f, Mathf.Sign(toward.y)));

            Vector2[] corners = { center + new Vector2(-half.x, -half.y), center + new Vector2(half.x, -half.y), center + new Vector2(half.x, half.y), center + new Vector2(-half.x, half.y) };
            var fence = new GameObject("Fence").transform;
            fence.SetParent(field, false);
            int piece = 0;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i], b = corners[(i + 1) % 4];
                Vector2 middle = (a + b) * 0.5f;
                Vector2 outward = (middle - center);
                outward = Mathf.Abs(outward.x) * half.y > Mathf.Abs(outward.y) * half.x ? new Vector2(Mathf.Sign(outward.x), 0f) : new Vector2(0f, Mathf.Sign(outward.y));
                if (outward == gapSide)
                {
                    Vector2 along = (b - a).normalized;
                    FenceRun(fence, timber, terrain, a, middle - along * FENCE_GAP * 0.5f, ref piece);
                    FenceRun(fence, timber, terrain, middle + along * FENCE_GAP * 0.5f, b, ref piece);
                }
                else FenceRun(fence, timber, terrain, a, b, ref piece);
            }

            var crops = new GameObject("Crops").transform;
            crops.SetParent(field, false);
            crops.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // applies to the children too

            Vector2 inner = size - Vector2.one * ROW_INSET * 2f;
            bool rowsAlongX = inner.x >= inner.y;
            float length = rowsAlongX ? inner.x : inner.y, across = rowsAlongX ? inner.y : inner.x;

            if (crop == Crop.Hay)
            {
                int index = 0;
                for (float u = -length * 0.5f + HAYCOCK_SPACING * 0.5f; u < length * 0.5f; u += HAYCOCK_SPACING)
                    for (float v = -across * 0.5f + HAYCOCK_SPACING * 0.5f; v < across * 0.5f; v += HAYCOCK_SPACING)
                    {
                        Vector2 at = center + (rowsAlongX ? new Vector2(u, v) : new Vector2(v, u));
                        float ground = Area1BlockoutBuilder.Ground(terrain, at);
                        Area1RemoteSites.Thin($"Haycock_{index++}", crops, cropMaterial, Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f)).position = new Vector3(at.x, ground + 0.4f, at.y);
                    }
                return;
            }

            float height = crop == Crop.Wheat ? 1f : crop == Crop.Garden ? 0.35f : 0.9f;
            float width = crop == Crop.Garden ? 0.5f : 0.7f;
            int rows = Mathf.FloorToInt(across / ROW_SPACING) + 1;
            int pieces = Mathf.CeilToInt(length / ROW_PIECE);
            float pieceLength = length / pieces;
            for (int r = 0; r < rows; r++)
            {
                float v = -across * 0.5f + (rows == 1 ? across * 0.5f : across * r / (rows - 1));
                for (int p = 0; p < pieces; p++)
                {
                    float u = -length * 0.5f + pieceLength * (p + 0.5f);
                    Vector2 at = center + (rowsAlongX ? new Vector2(u, v) : new Vector2(v, u));
                    float ground = Area1BlockoutBuilder.Ground(terrain, at);
                    Vector3 rowSize = rowsAlongX ? new Vector3(pieceLength - 0.1f, height + BURY, width) : new Vector3(width, height + BURY, pieceLength - 0.1f);
                    Area1RemoteSites.Thin($"Row_{r}_{p}", crops, cropMaterial, Vector3.zero, rowSize).position = new Vector3(at.x, ground + (height - BURY) * 0.5f, at.y);
                }
            }
        }

        /// <summary>
        /// A plank ramp from the cart ramp's edge down to the flat ground, because the graded ramp's side drops 1.5 m in the rail gap.
        /// <paramref name="top"/> sits on the lip. The planks start at the highest ground along the line, so no part of them dips under the lip.
        /// </summary>
        internal static void BuildStep(Vector2 top, Vector2 foot, float width, Terrain terrain, Material timber, Transform root)
        {
            float high = float.NegativeInfinity;
            for (int i = 0; i <= 8; i++)
                high = Mathf.Max(high, Area1BlockoutBuilder.Ground(terrain, Vector2.Lerp(top, foot, i / 8f)));
            var a = new Vector3(top.x, high, top.y);
            var b = new Vector3(foot.x, Area1BlockoutBuilder.Ground(terrain, foot), foot.y);
            const float thickness = 0.2f;
            Transform step = Area1BlockoutBuilder.Box("FarmStep", root, timber, (a + b) * 0.5f - Vector3.up * thickness * 0.5f, new Vector3(width, thickness, Vector3.Distance(a, b)));
            step.rotation = Quaternion.LookRotation(b - a);
        }

        private static void FenceRun(Transform parent, Material timber, Terrain terrain, Vector2 a, Vector2 b, ref int piece)
        {
            float length = Vector2.Distance(a, b);
            int count = Mathf.Max(1, Mathf.CeilToInt(length / FENCE_PIECE));
            Vector2 direction = (b - a) / length;
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                Vector2 at = Vector2.Lerp(a, b, (i + 0.5f) / count);
                Area1RemoteSites.Column($"Wattle_{piece++:000}", parent, timber, terrain, at, new Vector2(FENCE_THICKNESS, length / count), FENCE_HEIGHT, yaw);
            }
        }

        private static void RailSide(string name, Transform parent, Material timber, Vector3 from, Vector3 to, bool corners)
        {
            float length = Vector3.Distance(from, to);
            int bays = Mathf.CeilToInt(length / PADDOCK_POST_SPACING);
            for (int i = corners ? 0 : 1; i <= (corners ? bays : bays - 1); i++)
            {
                Vector3 at = Vector3.Lerp(from, to, i / (float)bays);
                Post($"Post_{name}_{i}", parent, timber, at.x, at.z, PADDOCK_RAIL + 0.1f, 0.18f);
            }

            Vector3 middle = (from + to) * 0.5f;
            bool alongX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.z - from.z);
            foreach (float height in new[] { 0.55f, 1f })
                Area1RemoteSites.Local($"Rail_{name}_{height:0.0}", parent, timber, new Vector3(middle.x, height, middle.z), alongX ? new Vector3(length, 0.1f, 0.08f) : new Vector3(0.08f, 0.1f, length));
        }

        private static Vector2 Outside(Area1Garrison.Plot plot, float x, float z)
        {
            Vector3 local = Quaternion.Euler(0f, plot.Yaw, 0f) * new Vector3(x, 0f, z);
            return plot.Center + new Vector2(local.x, local.z);
        }

        private static void Post(string name, Transform parent, Material timber, float x, float z, float height, float width)
        {
            Area1RemoteSites.Local(name, parent, timber, new Vector3(x, (height - BURY) * 0.5f, z), new Vector3(width, height + BURY, width));
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
