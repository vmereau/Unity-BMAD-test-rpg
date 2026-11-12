using System;
using System.IO;
using System.Linq;
using Game.Core;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.World
{
    /// <summary>
    /// Authoring of hand-owned mafia buildings. <see cref="Build"/> is one-time and never replaces
    /// existing content; <see cref="RebuildQuarters"/> deliberately replaces the quarters only.
    /// </summary>
    public static class Area1MafiaBlockout
    {
        private const string TAG = "[Area1MafiaBlockout]";
        private const float QUARTERS_X = 31.5f, QUARTERS_Z = 16.5f, QUARTERS_WIDTH = 4.1f, QUARTERS_DEPTH = 3f;
        // The rebuilt hall is turned 90°, so its width (local X) runs along world Z and its
        // door wall (local +Z) faces east, into the lane that leads to the mafia fire.
        private const float HALL_X = 24.925f, HALL_Z = 13.225f, HALL_WIDTH = 7.45f, HALL_DEPTH = 6.05f, HALL_EAVE = 3.4f;
        private const float HALL_DOOR_AT = -2.175f, HALL_DOOR_WIDTH = 2f, HALL_DOOR_HEIGHT = 2.4f, HALL_PITCH = 30f;
        private const string MATERIALS = "Assets/_Game/Art/Materials/";
        private static readonly string[] ROOTS =
            { "MafiaQuarters", "ArenaSpectatorPlatforms", "BossHouse", "BossGarden", "BossOverlook" };

        [MenuItem("Tools/World/Author Mafia Buildings And Overlooks")]
        public static void Build()
        {
            (Scene scene, Transform mafia, Terrain terrain) = Context();
            foreach (string name in ROOTS)
                if (mafia.Find(name) != null)
                    throw new InvalidOperationException(name + " already exists. Its hand edits are authoritative; authoring refuses to replace it.");
            Material timber = RequiredMaterial("Blockout_Timber");
            Material canvas = RequiredMaterial("Blockout_Canvas");
            Material stone = RequiredMaterial("Blockout_Stone");
            Material crop = RequiredMaterial("Blockout_Crop");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Author hand-owned mafia buildings");
            try
            {
                Transform quarters = Root(ROOTS[0], mafia, new Vector3(QUARTERS_X, 0, QUARTERS_Z));
                float quarterFloor = MaxGround(terrain, new Vector2(QUARTERS_X, QUARTERS_Z), new Vector2(QUARTERS_WIDTH, QUARTERS_DEPTH)) + 0.12f;
                quarters.position += Vector3.up * quarterFloor;
                House(quarters, timber, QUARTERS_WIDTH, QUARTERS_DEPTH, 2.8f, false, false);
                SupportFloor(quarters, timber, terrain, new Vector2(QUARTERS_WIDTH, QUARTERS_DEPTH));
                Stairs("EntrySteps", quarters, timber, terrain,
                    new Vector3(QUARTERS_X, Ground(terrain, QUARTERS_X, 19.5f), 19.5f), new Vector3(QUARTERS_X, quarterFloor, QUARTERS_Z + QUARTERS_DEPTH * 0.5f), 1.8f);

                float bossFloor = Mathf.Max(4.65f, MaxGround(terrain, new Vector2(45.5f, 12.5f), new Vector2(6, 4.6f)) + 0.16f);
                Transform boss = Root(ROOTS[2], mafia, new Vector3(45.5f, bossFloor, 12.5f));
                House(boss, timber, 6f, 4.6f, 3f, true, true);
                SupportFloor(boss, timber, terrain, new Vector2(6, 4.6f));
                Stairs("BossApproach", boss, timber, terrain,
                    new Vector3(40.4f, Ground(terrain, 40.4f, 20), 20), new Vector3(40.4f, bossFloor, 13.4f), 1.8f, true);
                WorldBox("PorchLanding", boss, timber, new Vector3(41.05f, bossFloor - 0.13f, 12.5f), new Vector3(3.1f, 0.26f, 1.8f));
                Rail(boss, timber, new Vector3(39.5f, bossFloor, 11.6f), new Vector3(42.45f, bossFloor, 11.6f), 2.2f, true);
                Rail(boss, timber, new Vector3(41.35f, bossFloor, 13.4f), new Vector3(42.45f, bossFloor, 13.4f), 1f);

                Transform garden = Root(ROOTS[3], mafia, new Vector3(51.25f, bossFloor, 13.2f));
                Deck(garden, timber, terrain, new Vector2(5.5f, 6f), false);
                Rail(garden, timber, new Vector3(48.5f, bossFloor, 10.2f), new Vector3(54, bossFloor, 10.2f), 2.2f, true);
                Rail(garden, timber, new Vector3(54, bossFloor, 10.2f), new Vector3(54, bossFloor, 16.2f), 1.2f, true);
                Rail(garden, timber, new Vector3(48.5f, bossFloor, 10.2f), new Vector3(48.5f, bossFloor, 11.55f), 1.2f, true);
                Rail(garden, timber, new Vector3(48.5f, bossFloor, 13.45f), new Vector3(48.5f, bossFloor, 16.2f), 1.2f, true);
                Rail(garden, timber, new Vector3(48.5f, bossFloor, 16.2f), new Vector3(50.25f, bossFloor, 16.2f), 1.2f, true);
                Rail(garden, timber, new Vector3(52.75f, bossFloor, 16.2f), new Vector3(54, bossFloor, 16.2f), 1.2f, true);
                Planter(garden, timber, stone, crop, new Vector3(49.3f, bossFloor, 14.6f), new Vector2(0.75f, 1.7f));
                Planter(garden, timber, stone, crop, new Vector3(52.9f, bossFloor, 11.25f), new Vector2(1.5f, 0.7f));

                Transform overlook = Root(ROOTS[4], mafia, new Vector3(51.5f, bossFloor, 18f));
                Deck(overlook, timber, terrain, new Vector2(4.8f, 3.6f), false);
                Rail(overlook, timber, new Vector3(49.1f, bossFloor, 16.2f), new Vector3(49.1f, bossFloor, 19.8f), 1f);
                Rail(overlook, timber, new Vector3(53.9f, bossFloor, 16.2f), new Vector3(53.9f, bossFloor, 19.8f), 1f);
                Rail(overlook, timber, new Vector3(49.1f, bossFloor, 19.8f), new Vector3(53.9f, bossFloor, 19.8f), 1f);
                Bench(overlook, timber, new Vector3(49.75f, bossFloor, 17.25f), 90);
                Bench(overlook, timber, new Vector3(53.25f, bossFloor, 17.25f), 90);
                WorldBox("AnnouncerStand", overlook, timber, new Vector3(51.5f, bossFloor + 0.5f, 19.05f), new Vector3(0.55f, 1f, 0.35f));
                // Shade stays behind the clear viewing edge and leaves the central passage open.
                foreach (float x in new[] { 49.3f, 53.7f })
                    foreach (float z in new[] { 16.4f, 18.1f })
                        WorldBox("CanopyPost", overlook, timber, new Vector3(x, bossFloor + 1.4f, z), new Vector3(0.12f, 2.8f, 0.12f));
                var shade = WorldBox("CanvasShade", overlook, canvas, new Vector3(51.5f, bossFloor + 2.83f, 17.25f), new Vector3(4.8f, 0.07f, 2.1f));
                NotWalkable(shade);

                Transform spectators = Root(ROOTS[1], mafia, Vector3.zero);
                Transform first = Root("RoughDeck_South", spectators, new Vector3(43.1f, 4f, 21.8f));
                Deck(first, timber, terrain, new Vector2(2.6f, 3f), true);
                Transform second = Root("RoughDeck_North", spectators, new Vector3(43.1f, 4.15f, 24.65f));
                Deck(second, timber, terrain, new Vector2(2.6f, 2.7f), true);
                Rail(spectators, timber, new Vector3(44.4f, 4, 20.3f), new Vector3(44.4f, 4, 23.3f), 1f);
                Rail(spectators, timber, new Vector3(44.4f, 4.15f, 23.3f), new Vector3(44.4f, 4.15f, 26), 1f);
                Rail(spectators, timber, new Vector3(41.8f, 4, 20.3f), new Vector3(44.4f, 4, 20.3f), 1f);
                Rail(spectators, timber, new Vector3(41.8f, 4.15f, 26), new Vector3(44.4f, 4.15f, 26), 1f);
                Stairs("PublicApproach", spectators, timber, terrain,
                    new Vector3(36.4f, Ground(terrain, 36.4f, 23.2f), 23.2f), new Vector3(41.8f, 4f, 23.2f), 2f);
                Undo.CollapseUndoOperations(undo);
                EditorSceneManager.MarkSceneDirty(scene);
                GameLog.Info(TAG, "Authored five hand-owned mafia additions. Inspect and bake navigation before saving StartingTown.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        /// <summary>
        /// Moves the existing MafiaQuarters root onto its plot and replaces its children with the
        /// plot-filling barracks/warehouse. Changes no other object and never saves or bakes.
        /// </summary>
        [MenuItem("Tools/World/Rebuild Mafia Quarters")]
        public static void RebuildQuarters()
        {
            (Scene scene, Transform mafia, Terrain terrain) = Context();
            Transform quarters = mafia.Find(ROOTS[0])
                ?? throw new InvalidOperationException("MafiaQuarters is missing; this command only rebuilds the existing root.");
            Material timber = RequiredMaterial("Blockout_Timber");
            Material canvas = RequiredMaterial("Blockout_Canvas");
            float floor = MaxGround(terrain, new Vector2(HALL_X, HALL_Z), new Vector2(HALL_DEPTH, HALL_WIDTH)) + 0.12f;

            // The plot is hand-arranged: stop rather than build into a neighbour that moved.
            Physics.SyncTransforms();
            foreach (Collider hit in Physics.OverlapBox(new Vector3(HALL_X, floor + HALL_EAVE * 0.5f, HALL_Z),
                         new Vector3(HALL_DEPTH * 0.5f, HALL_EAVE * 0.5f - 0.2f, HALL_WIDTH * 0.5f),
                         Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(quarters) && !(hit is TerrainCollider))
                    throw new InvalidOperationException("The quarters plot is not clear: " + hit.name + " under " +
                        (hit.transform.parent != null ? hit.transform.parent.name : "the scene root") + " stands in the footprint.");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild mafia quarters");
            bool cleared = false;
            try
            {
                for (int i = quarters.childCount - 1; i >= 0; i--)
                    Undo.DestroyObjectImmediate(quarters.GetChild(i).gameObject);
                cleared = true;
                Undo.RecordObject(quarters, "Move mafia quarters");
                quarters.SetPositionAndRotation(new Vector3(HALL_X, floor, HALL_Z), Quaternion.Euler(0, 90, 0));
                quarters.localScale = Vector3.one;
                Hall(quarters, timber, canvas, terrain);
                foreach (Transform child in quarters)
                    Undo.RegisterCreatedObjectUndo(child.gameObject, "Rebuild mafia quarters");
                Undo.CollapseUndoOperations(undo);
                EditorSceneManager.MarkSceneDirty(scene);
                GameLog.Info(TAG, "Rebuilt MafiaQuarters as the plot-filling hall. Inspect and bake navigation before saving StartingTown.");
            }
            catch
            {
                // New geometry is only registered with Undo once the hall is complete, so a
                // half-built hall has to be removed by hand before the old children come back.
                if (cleared)
                    for (int i = quarters.childCount - 1; i >= 0; i--)
                        UnityEngine.Object.DestroyImmediate(quarters.GetChild(i).gameObject);
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        private static (Scene scene, Transform mafia, Terrain terrain) Context()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Author the mafia blockout in edit mode after compilation.");
            Scene scene = SceneManager.GetSceneByPath("Assets/_Game/Scenes/StartingTown.unity");
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Load StartingTown before authoring the mafia blockout.");
            Transform camp = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "Area1_Blockout")?.transform;
            Transform mafia = camp?.Find("TentCamp/MafiaZone");
            Terrain terrain = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>(true)).SingleOrDefault();
            if (mafia == null || terrain == null)
                throw new InvalidOperationException("Expected the hand-owned MafiaZone and one terrain in StartingTown.");
            foreach (string file in new[] { "layout.json", "layout.seed.json" })
            {
                string path = Path.Combine(Application.dataPath, "../docs/World/Areas/area-001/", file);
                JObject layout = JObject.Parse(File.ReadAllText(path));
                JObject ownership = ((JArray)layout["features"]).OfType<JObject>()
                    .SingleOrDefault(f => (string)f["id"] == Area1WorkerSettlement.HAND_OWNED);
                if ((bool?)ownership?["enabled"] != true)
                    throw new InvalidOperationException("Keep settlement-hand-owned.enabled true in " + file + ".");
            }
            return (scene, mafia, terrain);
        }

        /// <summary>Child group that shares its parent's frame. Undo is registered by the caller.</summary>
        private static Transform Group(string name, Transform parent, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            return go.transform;
        }

        private static void Hall(Transform root, Material timber, Material canvas, Terrain terrain)
        {
            float hx = HALL_WIDTH * 0.5f, hz = HALL_DEPTH * 0.5f;
            Box("Floor", root, timber, new Vector3(0, -0.12f, 0), new Vector3(HALL_WIDTH, 0.24f, HALL_DEPTH));
            foreach (float x in new[] { -hx, hx })
                Area1WorkerHut.PlankWall("SideWall", root, timber, new Vector3(x, HALL_EAVE * 0.5f, 0), HALL_DEPTH, HALL_EAVE, x > 0 ? 90 : 270);
            // Yaws turn each wall's plank relief outwards, where the neighbours see it.
            Area1WorkerHut.PlankWall("RearWall", root, timber, new Vector3(0, HALL_EAVE * 0.5f, -hz), HALL_WIDTH, HALL_EAVE, 180);

            // The only way in. It sits off-centre so it opens north of worker-hut-25's side wall.
            float doorMin = HALL_DOOR_AT - HALL_DOOR_WIDTH * 0.5f, doorMax = HALL_DOOR_AT + HALL_DOOR_WIDTH * 0.5f;
            Transform front = Group("FireFacingEntrance", root, new Vector3(0, 0, hz));
            Area1WorkerHut.PlankWall("DoorSide", front, timber, new Vector3((doorMin - hx) * 0.5f, HALL_EAVE * 0.5f, 0), doorMin + hx, HALL_EAVE, 0);
            Area1WorkerHut.PlankWall("DoorSide", front, timber, new Vector3((doorMax + hx) * 0.5f, HALL_EAVE * 0.5f, 0), hx - doorMax, HALL_EAVE, 0);
            Box("DoorLintel", front, timber, new Vector3(HALL_DOOR_AT, (HALL_DOOR_HEIGHT + HALL_EAVE) * 0.5f, 0),
                new Vector3(HALL_DOOR_WIDTH, HALL_EAVE - HALL_DOOR_HEIGHT, 0.18f));
            foreach (float x in new[] { doorMin - 0.11f, doorMax + 0.11f })
                Box("DoorPost", front, timber, new Vector3(x, HALL_EAVE * 0.5f, 0), new Vector3(0.22f, HALL_EAVE, 0.26f));
            Vector3 step = root.TransformPoint(new Vector3(HALL_DOOR_AT, 0, hz + 0.37f));
            float stepBottom = Ground(terrain, step.x, step.z) - 0.15f, stepTop = root.position.y - 0.02f;
            if (stepTop - stepBottom > 0.43f)
                throw new InvalidOperationException("The lane outside the quarters door lies too far below the floor for a single doorstep.");
            if (stepTop > stepBottom)
                WorldBox("Doorstep", root, timber, new Vector3(step.x, (stepTop + stepBottom) * 0.5f, step.z),
                    new Vector3(HALL_DOOR_WIDTH + 0.3f, stepTop - stepBottom, 0.6f));

            // Gable ends face the fire and the Player's Zone; the eaves run along the palisade.
            float rise = hx * Mathf.Tan(HALL_PITCH * Mathf.Deg2Rad);
            foreach (float side in new[] { -1f, 1f })
            {
                Transform roof = Box("BoardedRoof", root, timber, new Vector3(side * hx * 0.5f, HALL_EAVE + rise * 0.5f, 0),
                    new Vector3(hx / Mathf.Cos(HALL_PITCH * Mathf.Deg2Rad) + 0.2f, 0.16f, HALL_DEPTH + 0.2f));
                roof.localRotation = Quaternion.Euler(0, 0, -side * HALL_PITCH);
                NotWalkable(roof);
            }
            const int TIERS = 8;
            for (int tier = 0; tier < TIERS; tier++)
                foreach (float z in new[] { -hz, hz })
                    Box("ClosedGable", root, timber, new Vector3(0, HALL_EAVE + rise / TIERS * (tier + 0.5f), z),
                        new Vector3(HALL_WIDTH * (1f - (tier + 0.75f) / TIERS), rise / TIERS, 0.14f));
            Box("RidgeBeam", root, timber, new Vector3(0, HALL_EAVE + rise - 0.2f, 0), new Vector3(0.2f, 0.24f, HALL_DEPTH));
            foreach (float z in new[] { -1f, 1f })
            {
                Box("TieBeam", root, timber, new Vector3(0, HALL_EAVE - 0.11f, z), new Vector3(HALL_WIDTH, 0.22f, 0.2f));
                Box("KingPost", root, timber, new Vector3(0, HALL_EAVE + rise * 0.5f - 0.1f, z), new Vector3(0.16f, rise - 0.2f, 0.16f));
                foreach (float x in new[] { -hx + 0.17f, hx - 0.17f })
                    Box("WallPost", root, timber, new Vector3(x, HALL_EAVE * 0.5f, z), new Vector3(0.2f, HALL_EAVE, 0.2f));
            }
            foreach (float x in new[] { -hx, hx })
                foreach (float z in new[] { -hz, hz })
                    Box("CornerFrame", root, timber, new Vector3(x, HALL_EAVE * 0.5f, z), new Vector3(0.24f, HALL_EAVE, 0.24f));
            SupportFloor(root, timber, terrain, new Vector2(HALL_WIDTH, HALL_DEPTH));
            // worker-hut-11 stands askew, leaving a wedge up to 1 m wide along the rear wall that
            // a capsule can enter past the hut's north corner. Board it there, down into the
            // ground; every other gap around the hall is under 0.5 m.
            Area1WorkerHut.PlankWall("GapBoards", root, timber, new Vector3(-0.725f, 0.9f, -hz - 0.6f), 1.1f, 2.6f, 270);

            // Barracks along the rear wall, crates at the palisade end, barrels beside the door.
            // Everything stays out of the lane from the door, and nothing by the palisade wall
            // rises above 1.2 m.
            Transform props = Group("Furnishings", root, Vector3.zero);
            NotWalkable(props);
            foreach (float x in new[] { -2.6f, -0.5f, 1.6f })
                Bunk(props, timber, canvas, new Vector3(x, 0, -hz + 0.58f));
            Box("MessTable", props, timber, new Vector3(0.3f, 0.71f, 0.5f), new Vector3(1.8f, 0.08f, 0.8f));
            foreach (float x in new[] { -0.45f, 1.05f })
                Box("TableTrestle", props, timber, new Vector3(x, 0.335f, 0.5f), new Vector3(0.12f, 0.67f, 0.7f));
            foreach (float z in new[] { -0.2f, 1.2f })
                Box("MessBench", props, timber, new Vector3(0.3f, 0.225f, z), new Vector3(1.6f, 0.45f, 0.3f));
            foreach (float z in new[] { -1.3f, -0.2f, 0.9f, 2f })
            {
                Box("StoreCrate", props, timber, new Vector3(hx - 0.75f, 0.4f, z), new Vector3(0.9f, 0.8f, 0.9f));
                if (z < 0)
                    Box("StoreCrate", props, timber, new Vector3(hx - 0.7f, 0.975f, z + 0.1f), new Vector3(0.6f, 0.35f, 0.6f));
            }
            foreach (float x in new[] { 0.5f, 1.2f, 1.9f })
                Box("StoreBarrel", props, timber, new Vector3(x, 0.425f, hz - 0.45f), new Vector3(0.55f, 0.85f, 0.55f));
            Area1RemoteSites.Thin("SackPile", props, canvas, new Vector3(hx - 1.83f, 0.2f, 1.7f), new Vector3(0.8f, 0.4f, 1f));
        }

        private static void Bunk(Transform parent, Material timber, Material canvas, Vector3 floor)
        {
            Transform bunk = Group("BunkFrame", parent, floor);
            foreach (float x in new[] { -0.9f, 0.9f })
                foreach (float z in new[] { -0.36f, 0.36f })
                    Box("BunkPost", bunk, timber, new Vector3(x, 0.85f, z), new Vector3(0.1f, 1.7f, 0.1f));
            foreach (float y in new[] { 0.4f, 1.3f })
            {
                Box("BunkBoards", bunk, timber, new Vector3(0, y, 0), new Vector3(1.9f, 0.1f, 0.82f));
                Area1RemoteSites.Thin("Bedding", bunk, canvas, new Vector3(0, y + 0.08f, 0), new Vector3(1.7f, 0.06f, 0.7f));
            }
        }

        private static Material RequiredMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>(MATERIALS + name + ".mat")
            ?? throw new InvalidOperationException("Missing blockout material " + name);

        private static Transform Root(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go.transform;
        }

        private static Transform Box(string name, Transform parent, Material material, Vector3 at, Vector3 size) =>
            Area1RemoteSites.Local(name, parent, material, at, size);

        private static Transform WorldBox(string name, Transform parent, Material material, Vector3 at, Vector3 size) =>
            Box(name, parent, material, parent.InverseTransformPoint(at), size);

        private static void NotWalkable(Transform t)
        {
            var modifier = t.gameObject.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = 1;
        }

        private static float Ground(Terrain terrain, float x, float z) => Area1BlockoutBuilder.Ground(terrain, new Vector2(x, z));

        private static float MaxGround(Terrain terrain, Vector2 at, Vector2 size)
        {
            float highest = float.NegativeInfinity;
            for (int x = 0; x <= 4; x++)
                for (int z = 0; z <= 4; z++)
                    highest = Mathf.Max(highest, Ground(terrain, at.x + size.x * (x / 4f - 0.5f), at.y + size.y * (z / 4f - 0.5f)));
            return highest;
        }

        private static void House(Transform root, Material timber, float width, float depth, float eave, bool boss, bool endDoors)
        {
            Box("Floor", root, timber, new Vector3(0, -0.12f, 0), new Vector3(width, 0.24f, depth));
            float hx = width * 0.5f, hz = depth * 0.5f;
            if (endDoors)
            {
                foreach (float x in new[] { -hx, hx })
                {
                    Transform wall = Root(x < 0 ? "FrontDoorWall" : "RearGardenDoorWall", root, root.TransformPoint(new Vector3(x, 0, 0)));
                    wall.localRotation = Quaternion.Euler(0, 90, 0);
                    DoorWall(wall, timber, depth, eave, 1.8f, 2.4f);
                }
                foreach (float z in new[] { -hz, hz })
                    Area1WorkerHut.PlankWall("SideWall", root, timber, new Vector3(0, eave * 0.5f, z), width, eave, 0);
            }
            else
            {
                foreach (float x in new[] { -hx, hx })
                    Area1WorkerHut.PlankWall("SideWall", root, timber, new Vector3(x, eave * 0.5f, 0), depth, eave, 90);
                Area1WorkerHut.PlankWall("RearWall", root, timber, new Vector3(0, eave * 0.5f, -hz), width, eave, 0);
                Transform wall = Root("SquareFacingEntrance", root, root.TransformPoint(new Vector3(0, 0, hz)));
                DoorWall(wall, timber, width, eave, 1.8f, 2.3f);
            }
            float pitch = boss ? 30 : 34;
            // Ridge along X for the boss house; along Z for the rough quarters.
            float span = boss ? depth : width;
            float length = (boss ? width : depth) + 0.35f;
            float rise = span * 0.5f * Mathf.Tan(pitch * Mathf.Deg2Rad);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 at = boss ? new Vector3(0, eave + rise * 0.5f, side * span * 0.25f) : new Vector3(side * span * 0.25f, eave + rise * 0.5f, 0);
                Vector3 size = boss ? new Vector3(length, 0.16f, span * 0.5f / Mathf.Cos(pitch * Mathf.Deg2Rad) + 0.2f)
                    : new Vector3(span * 0.5f / Mathf.Cos(pitch * Mathf.Deg2Rad) + 0.2f, 0.14f, length);
                Transform roof = Box("BoardedRoof", root, timber, at, size);
                roof.localRotation = boss ? Quaternion.Euler(side * pitch, 0, 0) : Quaternion.Euler(0, 0, -side * pitch);
                NotWalkable(roof);
            }
            // Closed stepped gables avoid sky showing through the enterable interior.
            for (int tier = 0; tier < 6; tier++)
            {
                float h = rise / 6f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 at = boss ? new Vector3(side * hx, eave + h * (tier + 0.5f), 0) : new Vector3(0, eave + h * (tier + 0.5f), side * hz);
                    Vector3 size = boss ? new Vector3(0.14f, h, depth * (1f - (tier + 0.5f) / 6f)) : new Vector3(width * (1f - (tier + 0.5f) / 6f), h, 0.14f);
                    Box("ClosedGable", root, timber, at, size);
                }
            }
            foreach (float x in new[] { -hx, hx })
                foreach (float z in new[] { -hz, hz })
                    Box("CornerFrame", root, timber, new Vector3(x, eave * 0.5f, z), new Vector3(boss ? 0.22f : 0.16f, eave, boss ? 0.22f : 0.16f));
            if (boss)
                foreach (float z in new[] { -hz - 0.08f, hz + 0.08f })
                {
                    Box("SillBeam", root, timber, new Vector3(0, 0.2f, z), new Vector3(width, 0.22f, 0.18f));
                    Box("EaveBeam", root, timber, new Vector3(0, eave - 0.08f, z), new Vector3(width + 0.3f, 0.22f, 0.2f));
                }
        }

        private static void DoorWall(Transform wall, Material timber, float width, float height, float doorWidth, float doorHeight)
        {
            float piece = (width - doorWidth) * 0.5f;
            foreach (float side in new[] { -1f, 1f })
                Area1WorkerHut.PlankWall("DoorSide", wall, timber, new Vector3(side * (doorWidth + piece) * 0.5f, height * 0.5f, 0), piece, height, 0);
            Box("DoorLintel", wall, timber, new Vector3(0, (doorHeight + height) * 0.5f, 0), new Vector3(doorWidth, height - doorHeight, 0.18f));
        }

        private static void SupportFloor(Transform root, Material timber, Terrain terrain, Vector2 size)
        {
            foreach (float x in new[] { -size.x * 0.5f + 0.1f, size.x * 0.5f - 0.1f })
                foreach (float z in new[] { -size.y * 0.5f + 0.1f, size.y * 0.5f - 0.1f })
                {
                    Vector3 p = root.TransformPoint(new Vector3(x, 0, z));
                    float bottom = Ground(terrain, p.x, p.z) - 0.2f;
                    float h = Mathf.Max(0.2f, p.y - bottom);
                    WorldBox("FoundationPost", root, timber, new Vector3(p.x, p.y - h * 0.5f, p.z), new Vector3(0.24f, h, 0.24f));
                }
            if (root.name == "BossHouse" || root.name == "MafiaQuarters")
            {
                // Close the foundations; otherwise the high bank-side floor creates an unintended room underneath.
                foreach (float z in new[] { -size.y * 0.5f, size.y * 0.5f })
                    FoundationSkirt(root, timber, terrain, new Vector3(0, 0, z), new Vector2(size.x, 0.12f));
                foreach (float x in new[] { -size.x * 0.5f, size.x * 0.5f })
                    FoundationSkirt(root, timber, terrain, new Vector3(x, 0, 0), new Vector2(0.12f, size.y));
            }
        }

        private static void FoundationSkirt(Transform root, Material timber, Terrain terrain, Vector3 center, Vector2 size)
        {
            Vector3 world = root.TransformPoint(center);
            float bottom = float.PositiveInfinity;
            for (int i = 0; i <= 8; i++)
            {
                // Sample along the skirt in the root's frame: the rebuilt quarters are turned 90°.
                Vector3 p = root.TransformPoint(center + new Vector3(size.x > size.y ? size.x * (i / 8f - 0.5f) : 0, 0,
                    size.y >= size.x ? size.y * (i / 8f - 0.5f) : 0));
                bottom = Mathf.Min(bottom, Ground(terrain, p.x, p.z) - 0.12f);
            }
            float top = root.position.y - 0.24f;
            if (top > bottom)
                WorldBox("FoundationSkirt", root, timber, new Vector3(world.x, (top + bottom) * 0.5f, world.z), new Vector3(size.x, top - bottom, size.y));
        }

        private static void Deck(Transform root, Material timber, Terrain terrain, Vector2 size, bool rough)
        {
            Box("StructuralDeck", root, timber, new Vector3(0, -0.14f, 0), new Vector3(size.x, 0.28f, size.y));
            int boards = Mathf.CeilToInt(size.y / 0.3f);
            for (int i = 0; i < boards; i++)
                Area1RemoteSites.Thin("DeckBoard", root, timber, new Vector3(0, 0.012f, -size.y * 0.5f + (i + 0.5f) * size.y / boards),
                    new Vector3(size.x + (rough && i % 3 == 0 ? 0.1f : 0), 0.024f, size.y / boards - 0.018f));
            SupportFloor(root, timber, terrain, size);
            foreach (float z in new[] { -size.y * 0.5f + 0.3f, size.y * 0.5f - 0.3f })
                Box("UnderBeam", root, timber, new Vector3(0, -0.36f, z), new Vector3(size.x + 0.15f, 0.3f, 0.2f));
        }

        private static void Stairs(string name, Transform parent, Material timber, Terrain terrain, Vector3 foot, Vector3 top, float width, bool enclosed = false)
        {
            Transform stairs = Root(name, parent, foot);
            Vector3 run = top - foot;
            Vector3 horizontal = new Vector3(run.x, 0, run.z);
            stairs.rotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
            int count = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0, run.y) / 0.24f));
            float tread = horizontal.magnitude / count;
            for (int i = 0; i < count; i++)
            {
                float upper = foot.y + run.y * (i + 1f) / count;
                Vector3 p = stairs.TransformPoint(new Vector3(0, 0, (i + 0.5f) * tread));
                float bottom = Mathf.Min(foot.y - 0.15f, Ground(terrain, p.x, p.z) - 0.15f);
                Box("SolidTread_" + i, stairs, timber, new Vector3(0, (upper + bottom) * 0.5f - foot.y, (i + 0.5f) * tread),
                    new Vector3(width, upper - bottom, tread + 0.015f));
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 offset = stairs.right * side * width * 0.5f;
                // The raised boss approach sits beside the Company boundary. A continuous screen
                // prevents its rails becoming intermediate jump steps towards the palisade.
                Rail(parent, timber, foot + offset, top + offset, enclosed ? 2.2f : 0.9f, enclosed);
            }
        }

        private static void Rail(Transform parent, Material timber, Vector3 a, Vector3 b, float height, bool privacy = false)
        {
            Vector3 delta = b - a;
            float length = new Vector2(delta.x, delta.z).magnitude;
            var rail = Root(privacy ? "PrivacyScreen" : "SafetyRail", parent, a);
            // Posts stay vertical even on stairs. Tilting the whole rail leans its upper
            // end across the landing, where a capsule hits it despite a complete baked path.
            rail.rotation = Quaternion.LookRotation(new Vector3(delta.x, 0, delta.z), Vector3.up);
            int posts = Mathf.Max(1, Mathf.CeilToInt(length / 1.5f));
            for (int i = 0; i <= posts; i++)
                Box("RailPost", rail, timber, new Vector3(0, delta.y * i / posts + height * 0.5f, length * i / posts), new Vector3(0.13f, height, 0.13f));
            if (privacy)
            {
                for (int i = 0; i < posts; i++)
                {
                    float low = Mathf.Min(delta.y * i / posts, delta.y * (i + 1f) / posts);
                    float high = Mathf.Max(delta.y * i / posts, delta.y * (i + 1f) / posts) + height;
                    Box("ScreenBoards", rail, timber, new Vector3(0, (low + high) * 0.5f, length * (i + 0.5f) / posts),
                        new Vector3(0.12f, high - low, length / posts));
                }
            }
            else
                foreach (float y in new[] { 0.4f, height - 0.07f })
                {
                    Transform beam = Box("RailBeam", rail, timber, new Vector3(0, delta.y * 0.5f + y, length * 0.5f), new Vector3(0.1f, 0.12f, delta.magnitude));
                    beam.localRotation = Quaternion.LookRotation(new Vector3(0, delta.y, length), Vector3.up);
                }
            var volume = rail.gameObject.AddComponent<NavMeshModifierVolume>();
            volume.area = 1;
            volume.center = new Vector3(0, (delta.y + height) * 0.5f, length * 0.5f);
            volume.size = new Vector3(0.16f, Mathf.Abs(delta.y) + height, length);
        }

        private static void Bench(Transform parent, Material timber, Vector3 floor, float yaw)
        {
            Transform bench = Root("BossSeating", parent, floor);
            bench.rotation = Quaternion.Euler(0, yaw, 0);
            Box("Seat", bench, timber, new Vector3(0, 0.48f, 0), new Vector3(1.45f, 0.12f, 0.5f));
            foreach (float x in new[] { -0.52f, 0.52f })
                Box("Leg", bench, timber, new Vector3(x, 0.22f, 0), new Vector3(0.16f, 0.44f, 0.4f));
            Box("Backrest", bench, timber, new Vector3(0, 0.83f, -0.22f), new Vector3(1.45f, 0.5f, 0.1f));
            NotWalkable(bench);
        }

        private static void Planter(Transform parent, Material timber, Material stone, Material crop, Vector3 floor, Vector2 size)
        {
            WorldBox("RaisedGardenBed", parent, timber, floor + Vector3.up * 0.22f, new Vector3(size.x, 0.44f, size.y));
            WorldBox("Soil", parent, stone, floor + Vector3.up * 0.45f, new Vector3(size.x - 0.1f, 0.04f, size.y - 0.1f));
            WorldBox("GardenPlanting", parent, crop, floor + Vector3.up * 0.53f, new Vector3(size.x - 0.22f, 0.12f, size.y - 0.22f));
        }
    }
}

