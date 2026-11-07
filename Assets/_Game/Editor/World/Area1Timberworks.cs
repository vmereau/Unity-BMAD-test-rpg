using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// The graybox yard inside the wood opening. Logs come in at the tally, are sawn, season under the long shed and go to the
    /// carpenters, while offcuts feed the clamp and its charcoal feeds the forge. The Company store stands at the lane's fort road
    /// end. <see cref="Area1SitesBuilder"/> owns the root and validates the layout first.
    /// </summary>
    internal static class Area1Timberworks
    {
        private const float BURY = 0.3f;
        private const float WALL = 0.4f;
        private const float WALL_HEIGHT = 3f;
        private const float DOOR_WIDTH = 1.4f;
        private const float DOOR_HEIGHT = 2.3f;
        private const float ROOF_PITCH = 25f;
        private const float EAVE = 0.4f;
        private const float POST = 0.25f;
        private const int GABLE_TIERS = 4;

        private const float FORGE_HALF_WIDTH = 4.5f;
        private const float FORGE_HALF_DEPTH = 3.5f;
        private const float FORGE_ROOM_WEST = 1.5f; // everything east of this is Edric's room
        private const float CHIMNEY_TOP = 5.5f;     // clears the ridge at about 4.6

        private const float SHED_HALF_WIDTH = 7f;
        private const float SHED_HALF_DEPTH = 3f;
        private const int SHED_BAYS = 4;

        private const float LEAN_TO_RISE = 0.8f;      // high side towards the shop
        private const float LEAN_TO_RUN = 3f;
        private const float LEAN_TO_POST_X = 1.4f;
        private const float LEAN_TO_MIDDLE = 2.5f;    // roof height at the lean-to's centre
        private const float LEAN_TO_BURY = 0.6f;      // the ground falls away towards the fort road

        private const float SHOP_HALF_WIDTH = 6f;
        private const float SHOP_HALF_DEPTH = 4f;

        private const float STORE_WIDTH = 9f;
        private const float STORE_DEPTH = 6f;

        private static readonly Vector2 SawOffset = new Vector2(4.8f, -2f);
        private static readonly Vector2 SawRoofSize = new Vector2(3.6f, 7.6f);

        /// <summary>Stays south of the wood road. The sawpit keeps clear of the lane to its east.</summary>
        internal static void BuildReceivingYard(Vector2 center, Terrain terrain, Material timber, Transform root)
        {
            Area1RemoteSites.BuildLogStack("Logs_0", center + new Vector2(-1f, -4.5f), 15f, terrain, timber, root);
            Area1RemoteSites.BuildLogStack("Logs_1", center + new Vector2(3.5f, -7.5f), 80f, terrain, timber, root);
            Area1RemoteSites.BuildLeanTo("ToolShelter", center + new Vector2(-4f, 0f), new Vector2(0.7f, 0.7f), terrain, timber, root);
            Area1CampInterior.BuildCart("Cart_0", center + new Vector2(-0.5f, 0.5f), 30f, terrain, timber, root);
            Area1CampInterior.BuildCart("Cart_1", center + new Vector2(2f, 1.5f), -40f, terrain, timber, root);
            Area1RemoteSites.Column("TallyBoard", root, timber, terrain, center + new Vector2(-2.5f, 2.2f), new Vector2(1.6f, 0.2f), 2f, 0f);

            Vector2 sawAt = center + SawOffset;
            Area1RemoteSites.BuildSawPit(sawAt, terrain, timber, root);
            Transform roof = Area1Stoneworks.Group("SawRoof", root, sawAt, 0f, terrain);
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                    Post($"Post_{x:0}_{z:0}", roof, timber, x * (SawRoofSize.x * 0.5f - 0.2f), z * (SawRoofSize.y * 0.5f - 0.2f), 2.6f);
            Area1RemoteSites.Local("Roof", roof, timber, new Vector3(0f, 2.65f, 0f), new Vector3(SawRoofSize.x, 0.1f, SawRoofSize.y));
        }

        /// <summary>Open to the lane on the south. The hearth backs on the north wall, and the room at the east end is Edric's.</summary>
        internal static void BuildForge(Vector2 center, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Transform forge = Area1Stoneworks.Group("Forge", root, center, 0f, terrain);
            float wallY = (WALL_HEIGHT - BURY) * 0.5f;
            float wallSize = WALL_HEIGHT + BURY;
            float roomMiddle = (FORGE_ROOM_WEST + FORGE_HALF_WIDTH) * 0.5f;
            float roomWidth = FORGE_HALF_WIDTH - FORGE_ROOM_WEST;

            Area1RemoteSites.Local("Wall_Back", forge, stone, new Vector3(0f, wallY, FORGE_HALF_DEPTH - 0.25f), new Vector3(FORGE_HALF_WIDTH * 2f, wallSize, 0.5f));
            Area1RemoteSites.Local("Wall_East", forge, timber, new Vector3(FORGE_HALF_WIDTH - WALL * 0.5f, wallY, 0f), new Vector3(WALL, wallSize, FORGE_HALF_DEPTH * 2f));
            Area1RemoteSites.Local("Wall_Room_South", forge, timber, new Vector3(roomMiddle, wallY, -FORGE_HALF_DEPTH + WALL * 0.5f), new Vector3(roomWidth, wallSize, WALL));
            DoorWall("Wall_Room_West", forge, timber, FORGE_ROOM_WEST, 0f, FORGE_HALF_DEPTH);

            foreach (float x in new[] { -FORGE_HALF_WIDTH + 0.2f, -1.4f })
                Post($"Post_{x:0}", forge, timber, x, -FORGE_HALF_DEPTH + 0.2f, WALL_HEIGHT);

            Area1Stoneworks.GableRoof(forge, timber, new Vector3(0f, WALL_HEIGHT, 0f), FORGE_HALF_WIDTH * 2f + 0.8f, FORGE_HALF_DEPTH, EAVE, ROOF_PITCH);
            Gable("Gable_Room", forge, timber, FORGE_ROOM_WEST, FORGE_HALF_DEPTH);
            Gable("Gable_East", forge, timber, FORGE_HALF_WIDTH - WALL * 0.5f, FORGE_HALF_DEPTH);

            Area1RemoteSites.Local("Hearth", forge, stone, new Vector3(-2f, 0.45f, 2.4f), new Vector3(2.6f, 1.2f, 1.4f));
            Area1RemoteSites.Local("Chimney", forge, stone, new Vector3(-2f, (1f + CHIMNEY_TOP) * 0.5f, 2.6f), new Vector3(1.2f, CHIMNEY_TOP - 1f, 1.0f));
            Area1RemoteSites.Local("QuenchTrough", forge, timber, new Vector3(-3.7f, 0.25f, 1.6f), new Vector3(0.8f, 0.5f, 1.8f));
            Area1RemoteSites.Local("CharcoalBin", forge, timber, new Vector3(0.4f, 0.4f, 2.7f), new Vector3(1.4f, 0.8f, 0.8f));
            Area1RemoteSites.Local("Bedframe", forge, timber, new Vector3(3.2f, 0.1f, 2.2f), new Vector3(1.8f, 0.8f, 1f));
        }

        /// <summary>Open-sided, so the wind dries the planks. The aisle down the middle leaves a way through for a handcart.</summary>
        internal static void BuildSeasoningShed(Vector2 center, Terrain terrain, Material timber, Transform root)
        {
            Transform shed = Area1Stoneworks.Group("SeasoningShed", root, center, 0f, terrain);
            float firstPost = SHED_HALF_WIDTH - 0.2f;
            for (int i = 0; i <= SHED_BAYS; i++)
                foreach (float z in new[] { -1f, 1f })
                    Post($"Post_{i}_{z:0}", shed, timber, Mathf.Lerp(-firstPost, firstPost, i / (float)SHED_BAYS), z * (SHED_HALF_DEPTH - 0.2f), WALL_HEIGHT);
            Area1Stoneworks.GableRoof(shed, timber, new Vector3(0f, WALL_HEIGHT, 0f), SHED_HALF_WIDTH * 2f + 0.8f, SHED_HALF_DEPTH, EAVE, ROOF_PITCH);

            foreach (int i in new[] { 0, 1, 2 })
                foreach (float z in new[] { -1f, 1f })
                    Area1RemoteSites.Local($"Planks_{i}_{z:0}", shed, timber, new Vector3(-4.2f + i * 4.2f, 0.55f - BURY * 0.5f, z * 1.75f), new Vector3(3.6f, 1.1f + BURY, 1.5f));
        }

        /// <summary>A room to the west, entered through the open bay that faces the lane, with the cooper's lean-to on the east end.</summary>
        internal static void BuildCarpentersShop(Vector2 center, Terrain terrain, Material timber, Transform root)
        {
            Transform shop = Area1Stoneworks.Group("CarpentersShop", root, center, 0f, terrain);
            float wallY = (WALL_HEIGHT - BURY) * 0.5f;
            float wallSize = WALL_HEIGHT + BURY;

            Area1RemoteSites.Local("Wall_Back", shop, timber, new Vector3(0f, wallY, -SHOP_HALF_DEPTH + WALL * 0.5f), new Vector3(SHOP_HALF_WIDTH * 2f, wallSize, WALL));
            Area1RemoteSites.Local("Wall_West", shop, timber, new Vector3(-SHOP_HALF_WIDTH + WALL * 0.5f, wallY, 0f), new Vector3(WALL, wallSize, SHOP_HALF_DEPTH * 2f));
            Area1RemoteSites.Local("Wall_Front", shop, timber, new Vector3(-SHOP_HALF_WIDTH * 0.5f, wallY, SHOP_HALF_DEPTH - WALL * 0.5f), new Vector3(SHOP_HALF_WIDTH, wallSize, WALL));
            DoorWall("Wall_Room_East", shop, timber, 0f, 1.8f, SHOP_HALF_DEPTH);

            foreach (float x in new[] { 3f, SHOP_HALF_WIDTH - 0.2f })
                Post($"Post_{x:0}", shop, timber, x, SHOP_HALF_DEPTH - 0.2f, WALL_HEIGHT);

            Area1Stoneworks.GableRoof(shop, timber, new Vector3(0f, WALL_HEIGHT, 0f), SHOP_HALF_WIDTH * 2f + 0.8f, SHOP_HALF_DEPTH, EAVE, ROOF_PITCH);
            Gable("Gable_West", shop, timber, -SHOP_HALF_WIDTH + WALL * 0.5f, SHOP_HALF_DEPTH);
            Gable("Gable_Room", shop, timber, 0f, SHOP_HALF_DEPTH);

            Area1RemoteSites.Local("Workbench", shop, timber, new Vector3(3f, 0.4f, -2f), new Vector3(3.4f, 0.8f, 0.9f));
            Area1RemoteSites.Local("Stakes", shop, timber, new Vector3(4.6f, 0.3f, 1.4f), new Vector3(1.6f, 0.6f, 2.4f));
            Area1RemoteSites.Local("RoomBench", shop, timber, new Vector3(-3f, 0.4f, -3f), new Vector3(3.6f, 0.8f, 0.9f));

            Vector2 cooperAt = center + new Vector2(SHOP_HALF_WIDTH + 1.6f, 0f);
            Transform cooper = Area1Stoneworks.Group("Cooper", root, cooperAt, 0f, terrain);
            float slope = LEAN_TO_RISE / LEAN_TO_RUN;
            foreach (float x in new[] { -LEAN_TO_POST_X, LEAN_TO_POST_X })
                foreach (float z in new[] { -1.8f, 1.8f })
                {
                    float top = LEAN_TO_MIDDLE - x * slope;
                    Area1RemoteSites.Local($"Post_{x:0}_{z:0}", cooper, timber, new Vector3(x, (top - LEAN_TO_BURY) * 0.5f, z), new Vector3(POST, top + LEAN_TO_BURY, POST));
                }
            Transform roof = Area1RemoteSites.Local("Roof", cooper, timber, new Vector3(0f, LEAN_TO_MIDDLE, 0f), new Vector3(3.4f, 0.1f, 4f));
            roof.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan(slope) * Mathf.Rad2Deg);
            foreach (int i in new[] { 0, 1, 2 })
                Area1RemoteSites.Column($"Barrel_{i}", root, timber, terrain, cooperAt + new Vector2(0.3f, -1.4f + i * 1.2f), new Vector2(0.8f, 0.8f), 1f, i * 20f);
        }

        /// <summary>Stepped earth for the blockout, with the wood waiting to be burned stacked beside it.</summary>
        internal static void BuildCharcoalClamp(Vector2 center, Terrain terrain, Material timber, Material stone, Transform root)
        {
            Transform clamp = Area1Stoneworks.Group("CharcoalClamp", root, center, 0f, terrain);
            Area1RemoteSites.Local("Mound_0", clamp, stone, new Vector3(0f, 0.3f, 0f), new Vector3(5f, 1.2f, 5f)).localRotation = Quaternion.identity;
            Area1RemoteSites.Local("Mound_1", clamp, stone, new Vector3(0f, 0.3f, 0f), new Vector3(5f, 1.2f, 5f)).localRotation = Quaternion.Euler(0f, 45f, 0f);
            Area1RemoteSites.Local("Mound_2", clamp, stone, new Vector3(0f, 1f, 0f), new Vector3(3.6f, 0.8f, 3.6f)).localRotation = Quaternion.Euler(0f, 22f, 0f);
            Area1RemoteSites.Local("Mound_3", clamp, stone, new Vector3(0f, 1.6f, 0f), new Vector3(2f, 0.6f, 2f)).localRotation = Quaternion.Euler(0f, 67f, 0f);
            Area1RemoteSites.BuildLogStack("Logs_Clamp", center + new Vector2(7f, 1.5f), 90f, terrain, timber, root);
        }

        internal static void BuildCompanyStore(Vector2 center, Terrain terrain, Material timber, Material stone, Transform root)
        {
            var store = new GameObject("CompanyStore").transform;
            store.SetParent(root, false);
            Area1CampInterior.BuildHouseShell(center, STORE_WIDTH, STORE_DEPTH, Quaternion.identity, "West", terrain, timber, store);
            Area1RemoteSites.Local("Counter", store, timber, new Vector3(1.8f, 0.5f, 0f), new Vector3(3.6f, 1f, 0.7f));
            Area1RemoteSites.Local("Shelf_Back", store, timber, new Vector3(1.8f, 1.1f, -2.4f), new Vector3(3.6f, 2.2f, 0.5f));
            Area1RemoteSites.Local("Shelf_East", store, timber, new Vector3(3.9f, 1.1f, 0f), new Vector3(0.5f, 2.2f, 3f));
            Area1RemoteSites.Local("Strongbox", store, stone, new Vector3(-3.4f, 0.4f, -2.2f), new Vector3(1.2f, 0.8f, 0.8f));
        }

        private static void Post(string name, Transform parent, Material timber, float x, float z, float height)
        {
            Area1RemoteSites.Local(name, parent, timber, new Vector3(x, (height - BURY) * 0.5f, z), new Vector3(POST, height + BURY, POST));
        }

        /// <summary>A wall across the building at <paramref name="x"/>, with a door centred on <paramref name="doorZ"/>.</summary>
        internal static void DoorWall(string name, Transform parent, Material timber, float x, float doorZ, float halfDepth)
        {
            float wallY = (WALL_HEIGHT - BURY) * 0.5f;
            float wallSize = WALL_HEIGHT + BURY;
            float south = doorZ - DOOR_WIDTH * 0.5f;
            float north = doorZ + DOOR_WIDTH * 0.5f;
            Area1RemoteSites.Local($"{name}_S", parent, timber, new Vector3(x, wallY, (south - halfDepth) * 0.5f), new Vector3(WALL, wallSize, south + halfDepth));
            Area1RemoteSites.Local($"{name}_N", parent, timber, new Vector3(x, wallY, (north + halfDepth) * 0.5f), new Vector3(WALL, wallSize, halfDepth - north));
            Area1RemoteSites.Local($"{name}_Header", parent, timber, new Vector3(x, (DOOR_HEIGHT + WALL_HEIGHT) * 0.5f, doorZ), new Vector3(WALL, WALL_HEIGHT - DOOR_HEIGHT, DOOR_WIDTH));
        }

        /// <summary>Stepped, because these rooms are entered and an open gable leaves sky at the end.</summary>
        private static void Gable(string name, Transform parent, Material timber, float x, float halfDepth)
        {
            float rise = halfDepth * Mathf.Tan(ROOF_PITCH * Mathf.Deg2Rad);
            float tier = rise / GABLE_TIERS;
            for (int i = 0; i < GABLE_TIERS; i++)
                Area1RemoteSites.Local($"{name}_{i}", parent, timber, new Vector3(x, WALL_HEIGHT + tier * (i + 0.5f), 0f),
                    new Vector3(WALL, tier, halfDepth * 2f * (1f - (i + 0.5f) / GABLE_TIERS)));
        }
    }
}
