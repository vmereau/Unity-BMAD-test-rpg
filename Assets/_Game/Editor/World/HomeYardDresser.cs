using Game.Core;
using Game.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Art pass for the player's home yard in StartingTown: props inside the player's, Hobb's and the owned huts
    /// (<see cref="HomeYardHomes"/>: Tam, Pim and Gerrit, Bren), the shared fire with its kettle, the wood, work,
    /// evening (card game and lantern) and water corners, and the night lights. Beds, boxes and restricted areas of
    /// the owned huts are hand-owned and set by <see cref="HomeYardOwners"/>. Everything here goes under
    /// <c>PlayersZone/HomeYardDressing</c>; rerunning rebuilds that group and discards hand edits inside it.
    /// </summary>
    internal static class HomeYardDresser
    {
        private const string TAG = "[HomeYardDresser]";
        private const string ZONE = "Area1_Blockout/TentCamp/PlayersZone";
        private const string GROUP = "HomeYardDressing";
        private const string DAY_NIGHT = "Assets/_Game/Data/Config/DayNightConfig.asset";
        private const string MATERIALS = "Assets/_Game/Art/Environment/Camp/Materials/";
        private const string CARDS_MATERIAL = "M_Camp_SackCloth.mat";
        private const string DOOR_FLAP = "Assets/_Game/Art/Environment/Camp/models/door flap/DoorFlap.fbx";
        private const string MODELS = "Assets/_Game/Art/Environment/Camp/models/";
        private const string FLOOR_BED = "Assets/_Game/Prefabs/World/Beds/Bed_FloorPallet.prefab";
        private const string CLOTH_BUNDLE = MODELS + "cloth bundle/ClothBundle.prefab";
        private const string SPADE = MODELS + "wooden spade/WoodenSpade.fbx";
        private const string SHIRT = MODELS + "drying shirt/DryingShirt.fbx";
        private const string TARP_MATERIAL = "M_Camp_Tarp.mat";
        private const string PATCH_MATERIAL = "M_Camp_SackCloth.mat";
        private const string ROPE_MATERIAL = "M_Camp_Rope.mat";
        private const string LANES = "ArrivalSequence/HutLanes";
        private const float LANE_CLEARANCE = 0.7f;
        // Owned huts' belongings (spec-area-1-home-yard-huts-and-yard; built by belongings/Source~/build_belongings.py).
        // Each hangs or lies from its origin; for the wall pieces local +Z points away from the wall, into the room.
        private const string BELONGINGS = MODELS + "belongings/";
        private const string FLAIL = BELONGINGS + "Flail.fbx";
        private const string TALLY = BELONGINGS + "TallyBoard.fbx";
        private const string ROLLED_BLANKET = BELONGINGS + "RolledBlanket.fbx";
        private const string STONE_BLOCK = BELONGINGS + "StoneBlock.fbx";
        private const string RAG = BELONGINGS + "Rag.fbx";
        // Cheap carpentry from the same script: the kit's planks cost 3,300-3,900 triangles each.
        private const string PLANK = BELONGINGS + "Plank.fbx";
        private const string BOARDS = BELONGINGS + "Boards.fbx";
        private const string PEG_RAIL = BELONGINGS + "PegRail.fbx";
        // Light kit crate for every crate table (1,336 triangles; SM_Crate_01 / _02 are about 300k each at LOD0).
        private const string CRATE = "SM_Crate_B_01";

        // World positions of the yard's groups (HOME_YARD_CONCEPTS.md → Build brief).
        private static readonly Vector2 FirePoint = new Vector2(5.55f, 38.95f);
        private static readonly Vector2 CardsPoint = new Vector2(1.7f, 37.9f);
        private static readonly Vector2 WoodPoint = new Vector2(9.6f, 38.9f);
        private static readonly Vector2 WaterPoint = new Vector2(0.45f, 41.05f);
        // Near worker-tarp-north (3) (5.56, 47.11), on flat open ground east of the player's door, open side to the south
        // (the fire). Moved 1.26 m west of the graybox: there, the north-west wall of the hut then called hut-07 (now
        // the shell worker-hut; from (5.62, 45.63) to
        // (7.45, 47.88)) cut through the bay's east half.
        private static readonly Vector2 EastBayPoint = new Vector2(4.3f, 47.11f);
        // The path of boards from the two doors toward the fire, east of the doors' shared pocket.
        private static readonly Vector2 BoardsPoint = new Vector2(2.6f, 43.65f);
        // worker-tarp-north (1) sat in the wedge between Hobb's hut and hut-02 and overlapped the kit hut: the
        // lean-to replaces it; (3) becomes the two-bed bay.
        private static readonly string[] GrayboxGroups =
        {
            "Campfire", "RepairTrunk", "CardGame", "worker-tarp-north (1)", "worker-tarp-north (3)",
        };
        // East yard graybox tarps (WorkerHutRollout.ReplacedTarps): each is switched off only once its replacement
        // is built. The eaves tarp replaces the first, the ramp shelter the other two.
        // East yard anchors: the tarps hang from these huts' eaves and from the warehouse's camp ramp.
        private const string EAST_HUT = "worker-hut";
        private const string PORCH_HUT = "worker-hut-06";
        private const string SOUTH_HUT = "worker-hut-05";
        private const string LEAN_TO_HUT = "worker-hut-02";
        private const string RAMP = "Area1_Blockout/Warehouse/Ramp_Camp";
        private const float ROOF_PITCH = 28f; // the shells' roof (WorkerHutKitBuilder / build_worker_hut.py)

        private const string GLOW = "Assets/_Game/Art/Materials/Blockout_FireGlow.mat";

        // Tuned in Play at 23:00 against the H3 concept: a small warm pool, the rest of the yard dark but readable.
        private static readonly Color FireColor = new Color(1f, 0.66f, 0.38f);
        private static readonly Color FlameGlow = new Color(2.2f, 0.95f, 0.3f, 1f);
        private const float FIRE_INTENSITY = 7f;
        private const float FIRE_RANGE = 10f;
        private const float CANDLE_INTENSITY = 2.4f; // the candle stands in the far corner, 1.5 m from the bed
        private const float CANDLE_RANGE = 4.5f;
        private const float LANTERN_INTENSITY = 3.2f; // the card game's lantern: the evening corner, 4 m from the fire
        private const float LANTERN_RANGE = 6f;

        private static Terrain _terrain;

        [MenuItem("Tools/World/Dress Home Yard")]
        private static void Dress()
        {
            GameObject zoneObject = GameObject.Find(ZONE);
            bool exists = zoneObject != null && zoneObject.transform.Find(GROUP) != null;
            if (exists && !EditorUtility.DisplayDialog("Dress Home Yard",
                    $"{GROUP} already exists. Rebuilding it discards every hand edit inside it and cannot be undone.",
                    "Rebuild", "Cancel"))
                return;
            Run();
        }

        /// <summary>Rebuilds the dressing group without asking. Checks every input before it deletes anything.</summary>
        internal static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                GameLog.Error(TAG, "Leave Play mode and close Prefab Mode first; nothing changed.");
                return;
            }

            GameObject zoneObject = GameObject.Find(ZONE);
            var dayNight = AssetDatabase.LoadAssetAtPath<DayNightConfigSO>(DAY_NIGHT);
            var home = AssetDatabase.LoadAssetAtPath<GameObject>(WorkerHutKitBuilder.HOME_PATH);
            Transform playerHut = zoneObject != null ? zoneObject.transform.Find("worker-player") : null;
            Transform mateHut = zoneObject != null ? zoneObject.transform.Find("worker-mate") : null;
            _terrain = zoneObject != null ? SceneTerrain(zoneObject.scene) : null;
            if (playerHut == null || mateHut == null || _terrain == null || dayNight == null || home == null)
            {
                GameLog.Error(TAG, $"Need {ZONE} with worker-player and worker-mate, a terrain in that scene, {DAY_NIGHT} and {WorkerHutKitBuilder.HOME_PATH}; nothing changed.");
                return;
            }
            // Every prop position below is written for the 3 × 3 m home hut.
            if (!IsHomeHut(playerHut, home) || !IsHomeHut(mateHut, home))
            {
                GameLog.Error(TAG, "worker-player and worker-mate must be unscaled WorkerHut_Home instances; nothing changed.");
                return;
            }
            foreach (string asset in new[]
                     {
                         DOOR_FLAP, GLOW, MATERIALS + CARDS_MATERIAL, FLOOR_BED, CLOTH_BUNDLE, SPADE, SHIRT,
                         WorkerHutKitBuilder.ROOF_POLE, MATERIALS + TARP_MATERIAL, MATERIALS + PATCH_MATERIAL,
                         MATERIALS + ROPE_MATERIAL, FLAIL, TALLY, ROLLED_BLANKET, STONE_BLOCK, RAG, PLANK, BOARDS, PEG_RAIL,
                     })
                if (AssetDatabase.LoadMainAssetAtPath(asset) == null)
                {
                    GameLog.Error(TAG, $"Missing {asset}; nothing changed.");
                    return;
                }
            // The owned huts: interiors are written for 3 × 3 m shells with the doorway on the table's side.
            var homeHuts = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (HomeYardHomes.Home owned in HomeYardHomes.All)
            {
                Transform hut = zoneObject.transform.Find(owned.Hut);
                string layout = HomeYardHomes.Problem(owned);
                if (layout != null || hut == null || hut.lossyScale != Vector3.one
                    || !WorkerHutKitBuilder.TryGetVariant(hut.gameObject, out WorkerHutKitBuilder.Variant shell)
                    || shell.Mirrored != owned.Mirrored || shell.Long)
                {
                    GameLog.Error(TAG, layout ?? $"{ZONE}/{owned.Hut} must be an unscaled 3 × 3 m shell {(owned.Mirrored ? "with" : "without")} a mirrored doorway; nothing changed.");
                    return;
                }
                homeHuts[owned.Key] = hut;
            }

            WorkerHutKitBuilder.ResetKit();
            Transform zone = zoneObject.transform;
            Transform old = zone.Find(GROUP);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform group = new GameObject(GROUP).transform;
            group.SetParent(zone, false);

            // The graybox stand-ins stay in the scene, switched off, so the old layout can still be compared.
            foreach (string name in GrayboxGroups)
            {
                Transform graybox = zone.Find(name);
                if (graybox != null) graybox.gameObject.SetActive(false);
            }

            DressPlayerHut(Child("PlayerHut", group, playerHut), dayNight);
            DressMateHut(Child("MateHut", group, mateHut));
            Transform fire = DressFire(WorldChild("Fire", group, FirePoint, 0f), dayNight);
            SettleOnTerrain(fire);
            HangKettle(fire);
            DressTamHut(Child("TamHut", group, homeHuts["Tam"]), dayNight);
            DressPimGerritHut(Child("PimGerritHut", group, homeHuts["PimGerrit"]), dayNight);
            DressBrenHut(Child("BrenHut", group, homeHuts["Bren"]), dayNight);
            SettleOnTerrain(DressDoorBoards(group, homeHuts));
            SettleOnTerrain(DressWorkCorner(Child("WorkCorner", group, homeHuts["PimGerrit"])));
            SettleOnTerrain(DressWoodCorner(WorldChild("WoodCorner", group, WoodPoint, 0f)));
            SettleOnTerrain(DressCards(WorldChild("CardGame", group, CardsPoint, 10f), dayNight));
            SettleOnTerrain(DressWater(WorldChild("WaterCorner", group, WaterPoint, 0f)));
            SettleOnTerrain(DressYard(Child("Doorstep", group, playerHut)));
            // Not settled afterwards: the bay places everything on the ground itself, so the tie points of its tarp and
            // ropes follow the poles and stakes they hang from.
            Transform eastBay = DressEastBay(WorldChild("TarpBayEast", group, EastBayPoint, 180f));
            DressDryingLine(eastBay, playerHut);
            SettleOnTerrain(DressLeanTo(Child("LeanTo", group, mateHut), zone.Find(LEAN_TO_HUT)));
            DressEastYard(group, zone);
            SettleOnTerrain(DressBoards(WorldChild("MudBoards", group, BoardsPoint, 62f)));
            CheckLanes(group, zone);

            _terrain = null;
            EditorSceneManager.MarkSceneDirty(zoneObject.scene);
            if (WorkerHutKitBuilder.MissingCount > 0)
                GameLog.Error(TAG, $"{WorkerHutKitBuilder.MissingCount} kit piece(s) missing: look for 'Missing_' objects under {GROUP}.");
            else
                GameLog.Info(TAG, "Home yard dressed. Rebake the NavMesh before play.");
        }

        private static bool IsHomeHut(Transform hut, GameObject home) =>
            PrefabUtility.GetCorrespondingObjectFromSource(hut.gameObject) == home && hut.lossyScale == Vector3.one;

        private static Terrain SceneTerrain(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
                if (terrain.gameObject.scene == scene) return terrain;
            return null;
        }

        /// <summary>
        /// Outdoor groups sample the ground once, at their origin. Each direct child keeps its authored height above the
        /// ground, but measured where it stands, so a prop a few metres away does not float or sink on a slope.
        /// </summary>
        private static void SettleOnTerrain(Transform group)
        {
            foreach (Transform child in group)
            {
                // Generated cloth and ropes are built between tie points in the group's frame: leave them there.
                if (child.name.StartsWith(CLOTH_PREFIX)) continue;
                Vector3 world = child.position;
                float above = child.localPosition.y;
                world.y = _terrain.SampleHeight(world) + _terrain.transform.position.y + above;
                child.position = world;
            }
        }

        // Hut-local frame: +Z is the doorway (x 0..1.05), −Z the rear opening, the bed runs along the −X wall.
        private static void DressPlayerHut(Transform hut, DayNightConfigSO dayNight)
        {
            // Two small overlapping patches: one large piece reads as a rectangular mat.
            Prop("SM_Hay_02a", hut, new Vector3(0.2f, 0.1f, -0.35f), 25f, 0.4f, "FloorStraw");
            Prop("SM_Hay_02a", hut, new Vector3(0.6f, 0.09f, 0.45f), 80f, 0.36f, "FloorStraw");
            // In the corner away from the bed, so the strip beside the bed stays clear to the back wall.
            Vector3 top = CrateTable(hut, new Vector3(1.0f, 0f, -1.0f), 4f, 0.8f);
            Prop("SM_Candle_01c", hut, top + new Vector3(0.1f, 0f, -0.08f), 0f, 1f, "Candle");
            Prop("SM_Bowl_01", hut, top + new Vector3(-0.1f, 0f, 0.06f), 30f, 1f, "Bowl");
            Prop("SM_Sack_01", hut, new Vector3(0.45f, 0f, -1.08f), 70f, 1f, "Sack");
            Prop("SM_burlapSackA_a1", hut, new Vector3(1.05f, 0.2f, -0.35f), 80f, 0.8f, "Sack");
            Transform rope = Prop("SM_Rope_Bundle_01", hut, new Vector3(1.25f, 1.45f, 0.35f), 0f, 0.6f, "HangingRope");
            rope.localRotation = Quaternion.Euler(0f, 90f, 90f);
            Transform peg = Prop("SM_Damaged_Plank_A_01", hut, new Vector3(1.26f, 1.72f, 0.55f), 90f, 1f, "PegRail");
            peg.localRotation = Quaternion.Euler(90f, 90f, 0f);
            Flap(hut);

            Transform flame = Prop("PS_CandleFlame", hut, top + new Vector3(0.1f, 0.3f, -0.08f), 0f, 1f, "CandleFlame");
            // The flame only shows while the candle is lit; ClockNightLight switches the renderer with the light.
            foreach (Renderer renderer in flame.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            NightLight(flame, "CandleLight", new Vector3(0f, 0.12f, 0.12f), CANDLE_INTENSITY, CANDLE_RANGE, LightShadows.None,
                dayNight, flame.GetComponentsInChildren<Renderer>(), null, Color.white);
        }

        // Hobb has been here longer: more belongings, a seat, his own cooking things.
        private static void DressMateHut(Transform hut)
        {
            // His bed is a real Bed he owns and sleeps in (PlayersZone/HobbsBed, hand-owned, spec-npc-routine-sleep) —
            // along the −X wall at hut-local (−0.89, 0, −0.26), 90°. Keep props off that strip.
            Prop("SM_Hay_02a", hut, new Vector3(0.15f, 0.1f, -0.2f), -35f, 0.4f, "FloorStraw");
            Prop("SM_Hay_02a", hut, new Vector3(0.55f, 0.09f, 0.6f), 60f, 0.36f, "FloorStraw");
            CrateTable(hut, new Vector3(0.58f, 0f, -0.98f), -6f, 0.85f);
            Prop("SM_KettlePot_01", hut, new Vector3(0.45f, 0.5f, -1.02f), 40f, 1f, "Kettle");
            Prop("SM_Mug", hut, new Vector3(0.68f, 0.5f, -0.9f), 0f, 1f, "Mug");
            Prop("SM_Candle_01a", hut, new Vector3(0.3f, 0.5f, -0.85f), 0f, 1f, "Candle");
            Prop("SM_WoodStool_01a", hut, new Vector3(0.45f, 0f, -0.3f), 25f, 1f, "Stool", solid: true);
            Prop("SM_burlapSackSetA_a1", hut, new Vector3(0.95f, 0f, 0.55f), 15f, 0.75f, "Sacks", solid: true);
            Prop("SM_bucketA_a1", hut, new Vector3(1.05f, 0f, -0.45f), 0f, 0.55f, "Bucket");
            Prop("SM_firewoodA_a1", hut, new Vector3(-0.8f, 0f, 1.05f), 90f, 0.6f, "Kindling");
            Transform rope = Prop("SM_Rope_Bundle_01", hut, new Vector3(1.25f, 1.5f, 0.1f), 0f, 0.6f, "HangingRope");
            rope.localRotation = Quaternion.Euler(0f, 90f, 90f);
            Flap(hut);
        }

        // The arrival escort's two lanes pass 1.6 m either side of the fire (ArrivalSequence/HutLanes) and the card
        // game's logs already narrow the west one, so the fire gets a short bench to the north, between the lanes, a
        // log seat east of the east lane, and nothing to the south. The wood pile stands east of the lanes.
        private static Transform DressFire(Transform fire, DayNightConfigSO dayNight)
        {
            Prop("SM_FireCamp_01b", fire, Vector3.zero, 40f, 0.85f, "FireRing", solid: true);
            // The ring nests the kit's own fire and smoke particles, which read as thin smoke in URP and stay on all
            // day. The flame itself is a night-only quad, like the hunter's fire.
            Transform billboard = new GameObject("Glow").transform;
            billboard.SetParent(fire, false);
            billboard.localPosition = new Vector3(0f, 0.45f, 0f);
            Renderer flame = GlowQuad("Flame", billboard, new Vector3(0.55f, 0.8f, 1f));
            // SM_Bench runs along its Z (2.2 m); at 0.6 it is a 1.3 m plank bench along X, 0.9 m clear of each lane.
            Prop("SM_Bench", fire, new Vector3(-0.6f, 0f, 1.45f), 90f, 0.6f, "Bench", solid: true);
            // SM_Log_Wide_B's pivot is at one end of its 1.7 m Z axis: this log runs north from here, facing the fire.
            Prop("SM_Log_Wide_B", fire, new Vector3(2.25f, 0f, 0.25f), 0f, 0.6f, "LogSeat", solid: true);
            NightLight(fire, "FireLight", new Vector3(0f, 0.9f, 0f), FIRE_INTENSITY, FIRE_RANGE, LightShadows.Soft, dayNight,
                flame != null ? new[] { flame } : null, billboard, FlameGlow);
            return fire;
        }

        /// <summary>
        /// The kettle hangs from the tripod's top on a short chain of rope, over the fire. The tripod is part of the
        /// fire ring's mesh: its top is the highest point of that mesh's bounds, over the ring's middle.
        /// </summary>
        private static void HangKettle(Transform fire)
        {
            Renderer ring = null;
            foreach (Renderer r in fire.GetComponentsInChildren<Renderer>(true))
                if (r.name.Contains("FireCamp")) { ring = r; break; }
            if (ring == null)
            {
                GameLog.Warn(TAG, "The fire ring has no tripod mesh: the kettle is not hung.");
                return;
            }
            Bounds b = ring.bounds;
            Vector3 apex = fire.InverseTransformPoint(new Vector3(b.center.x, b.max.y - 0.06f, b.center.z));
            Vector3 kettle = new Vector3(apex.x, 0.42f, apex.z);
            Prop("SM_KettlePot_01", fire, kettle, 25f, 1.1f, "Kettle");
            Cloth(fire, "KettleRope", CampClothMesh.Rope("HomeYard_KettleRope", kettle + Vector3.up * 0.33f, apex, 0.004f), false);
        }

        // ------------------------------------------------------------ owned huts (HomeYardHomes; Y4–Y6)
        // Hut frame: +Z is the doorway, the boards' inner faces at ±1.28. Beds and boxes are hand-owned objects placed
        // by HomeYardOwners at the table's poses; the props below keep clear of them and of the entry pocket.

        // Tam (mirrored doorway, bed on the +X wall): rags drying on a peg rail, walking stick, crate table, two sacks.
        private static void DressTamHut(Transform hut, DayNightConfigSO dayNight)
        {
            Straw(hut, new Vector3(-0.3f, 0.1f, -0.3f), 25f, new Vector3(-0.45f, 0.09f, 0.45f), 80f);
            Vector3 top = CrateTable(hut, new Vector3(-0.78f, 0f, -0.98f), 4f, 0.85f);
            Candle(hut, top + new Vector3(0.12f, 0f, -0.08f), dayNight);
            Prop("SM_Bowl_01", hut, top + new Vector3(-0.1f, 0f, 0.08f), 30f, 1f, "Bowl");
            Prop("SM_Sack_01", hut, new Vector3(-1.02f, 0f, -0.25f), 40f, 1f, "Sack");
            Prop("SM_Sack_01", hut, new Vector3(-1.02f, 0f, 0.22f), 110f, 0.9f, "Sack");
            Wall(PEG_RAIL, hut, new Vector3(-1.28f, 1.74f, 0.3f), -1f, "PegRail");
            Wall(RAG, hut, new Vector3(-1.2f, 1.76f, 0.06f), -1f, "Rag");
            Wall(RAG, hut, new Vector3(-1.2f, 1.76f, 0.54f), -1f, "Rag");
            Transform stick = Prop("SM_Stick_05", hut, new Vector3(0.28f, 0.72f, -1.16f), 0f, 0.75f, "WalkingStick");
            stick.localRotation = Quaternion.Euler(-6f, 0f, 4f);
        }

        // Pim and Gerrit (doorway on +X, Gerrit's bed on the −X wall, Pim's on the +X wall): the crate table between
        // the bed heads, Gerrit's flail on his wall, Pim's sack on his bed, a sack by their box, Pim's spade outside.
        private static void DressPimGerritHut(Transform hut, DayNightConfigSO dayNight)
        {
            Straw(hut, new Vector3(0f, 0.1f, -0.2f), -30f, new Vector3(0.15f, 0.09f, 0.55f), 60f);
            Vector3 top = CrateTable(hut, new Vector3(0f, 0f, -1.02f), 2f, 0.75f);
            Candle(hut, top + new Vector3(0.1f, 0f, -0.06f), dayNight);
            Prop("SM_Bowl_01", hut, top + new Vector3(-0.1f, 0f, 0.06f), 30f, 1f, "Bowl");
            Prop("SM_Sack_01", hut, new Vector3(-1.05f, 0f, 1.0f), 30f, 0.9f, "Sack");
            Prop("SM_Sack_01", hut, new Vector3(0.89f, BED_TOP, 0.4f), 20f, 0.75f, "Sack");
            Wall(FLAIL, hut, new Vector3(-1.28f, 1.55f, -0.25f), -1f, "Flail");
            Transform spade = Model(SPADE, hut, new Vector3(1.32f, 0f, 1.68f), -8f, "Spade");
            if (spade != null)
            {
                spade.localRotation = Quaternion.Euler(-15f, -8f, 0f);
                spade.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
        }

        // Bren (mirrored doorway, bed on the +X wall): Odo's blanket rolled at the bed's foot, the stone he shapes with
        // its chisel and mallet, the tally board on the wall, crate table, two sacks.
        private static void DressBrenHut(Transform hut, DayNightConfigSO dayNight)
        {
            Straw(hut, new Vector3(-0.2f, 0.1f, -0.1f), -20f, new Vector3(-0.5f, 0.09f, 0.55f), 70f);
            Vector3 top = CrateTable(hut, new Vector3(-0.78f, 0f, -0.98f), -5f, 0.85f);
            Candle(hut, top + new Vector3(0.12f, 0f, -0.1f), dayNight);
            Prop("SM_Bowl_01", hut, top + new Vector3(-0.1f, 0f, 0.08f), 30f, 1f, "Bowl");
            Transform block = Model(STONE_BLOCK, hut, new Vector3(-0.92f, 0f, -0.25f), 12f, "StoneBlock");
            if (block != null) block.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            Transform mallet = Prop("SM_Hammer", hut, new Vector3(-0.98f, 0.34f, -0.18f), 60f, 1f, "Mallet");
            mallet.localRotation = Quaternion.Euler(0f, 60f, 0f);
            Prop("SM_Sack_01", hut, new Vector3(-1.02f, 0f, 0.32f), 40f, 1f, "Sack");
            Prop("SM_Sack_01", hut, new Vector3(-1.02f, 0f, 0.74f), 110f, 0.9f, "Sack");
            Wall(TALLY, hut, new Vector3(-1.27f, 1.62f, -0.55f), -1f, "TallyBoard");
            Transform roll = Model(ROLLED_BLANKET, hut, new Vector3(0.89f, BED_TOP, 0.48f), 0f, "OdosBlanket");
            if (roll != null) roll.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        // Top of a Bed_WorkerPallet's mattress above the floor (the bed's own model).
        private const float BED_TOP = 0.43f;

        /// <summary>Two straw patches on the floor (Camp density rule).</summary>
        private static void Straw(Transform hut, Vector3 a, float yawA, Vector3 b, float yawB)
        {
            Prop("SM_Hay_02a", hut, a, yawA, 0.4f, "FloorStraw");
            Prop("SM_Hay_02a", hut, b, yawB, 0.34f, "FloorStraw");
        }

        /// <summary>
        /// A light kit crate used as a table, centred on <paramref name="centre"/> (its pivot is at a corner). Returns
        /// the middle of its top face, in the parent's frame.
        /// </summary>
        private static Vector3 CrateTable(Transform parent, Vector3 centre, float yaw, float scale, string name = "CrateTable")
        {
            Transform crate = Prop(CRATE, parent, centre, yaw, scale, name, solid: true);
            Bounds b = Centre(crate, parent, centre);
            return new Vector3(centre.x, parent.InverseTransformPoint(b.max).y, centre.z);
        }

        /// <summary>Moves a prop so its rendered middle stands over <paramref name="local"/> and its foot at local.y.</summary>
        private static Bounds Centre(Transform prop, Transform parent, Vector3 local)
        {
            Renderer[] renderers = prop.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(prop.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            Vector3 middle = parent.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
            prop.localPosition += local - middle;
            b.center += parent.TransformVector(local - middle);
            return b;
        }

        /// <summary>A candle on a table top that lights at night (the player's hut's candle, smaller room).</summary>
        private static void Candle(Transform hut, Vector3 at, DayNightConfigSO dayNight)
        {
            Prop("SM_Candle_01a", hut, at, 0f, 0.8f, "Candle");
            Transform flame = Prop("PS_CandleFlame", hut, at + new Vector3(0f, 0.18f, 0f), 0f, 0.8f, "CandleFlame");
            foreach (Renderer renderer in flame.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            NightLight(flame, "CandleLight", new Vector3(0f, 0.12f, 0.12f), CANDLE_INTENSITY, CANDLE_RANGE, LightShadows.None,
                dayNight, flame.GetComponentsInChildren<Renderer>(), null, Color.white);
        }

        /// <summary>A belonging hung on a side wall: its +Z (away from the wall) turned to face into the room.</summary>
        private static void Wall(string model, Transform hut, Vector3 at, float side, string name)
        {
            Transform piece = Model(model, hut, at, side > 0 ? -90f : 90f, name);
            if (piece != null) piece.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            else GameLog.Warn(TAG, $"Missing {model}");
        }

        /// <summary>Boards over the mud in front of each owned doorway. Walk-through.</summary>
        private static Transform DressDoorBoards(Transform group, System.Collections.Generic.Dictionary<string, Transform> huts)
        {
            Transform boards = new GameObject("DoorBoards").transform;
            boards.SetParent(group, false);
            boards.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (HomeYardHomes.Home owned in HomeYardHomes.All)
            {
                Transform hut = huts[owned.Key];
                Vector3 at = hut.TransformPoint(new Vector3((owned.DoorMinX + owned.DoorMaxX) * 0.5f, 0f, 2.05f));
                at.y = 0.03f; // the group stands at y 0: SettleOnTerrain adds the ground under each panel
                Transform panel = Model(BOARDS, boards, at, hut.eulerAngles.y + 4f, "Boards_" + owned.Key);
                if (panel != null) panel.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
            return boards;
        }

        // ------------------------------------------------------------ yard corners (Y1, Y3, Y7)

        // Pim's and Gerrit's hut frame: the repair trunk against the front wall left of the doorway (−X half), the
        // sawhorse a step out from it with a plank and a saw on it. The sawhorse runs along the wall: further out it
        // comes within 0.7 m of the escort's east lane.
        private static Transform DressWorkCorner(Transform corner)
        {
            Transform trunk = Prop("SM_WoodChest_01a", corner, Vector3.zero, 90f, 1f, "Trunk", solid: true);
            Centre(trunk, corner, new Vector3(-0.62f, 0f, 1.86f));
            Transform trestle = Prop("SM_WoodTrestles_01a", corner, Vector3.zero, 90f, 1f, "Sawhorse", solid: true);
            Bounds b = Centre(trestle, corner, new Vector3(-0.8f, 0f, 2.5f));
            float top = corner.InverseTransformPoint(b.max).y;
            Transform plank = Model(PLANK, corner, Vector3.zero, 3f, "Plank");
            if (plank != null) Centre(plank, corner, new Vector3(-0.8f, top, 2.5f));
            Transform saw = Prop("SM_Saw", corner, Vector3.zero, 75f, 1f, "Saw");
            Centre(saw, corner, new Vector3(-0.75f, top + 0.03f, 2.48f));
            return corner;
        }

        // East pocket: split firewood stacked under a loose plank lid, the chopping block with its axe in front.
        private static Transform DressWoodCorner(Transform corner)
        {
            Transform stack = Prop("SM_firewoodSetA_a1", corner, Vector3.zero, 0f, 0.62f, "Firewood", solid: true);
            Bounds b = Centre(stack, corner, new Vector3(0.35f, 0f, -0.1f));
            float top = corner.InverseTransformPoint(b.max).y;
            foreach (float x in new[] { 0.22f, 0.5f })
            {
                Transform lid = Model(PLANK, corner, Vector3.zero, 90f + x * 10f, "Lid");
                if (lid == null) continue;
                lid.localScale = new Vector3(1.4f, 1f, 1f); // 1.7 m: the stack's length
                Centre(lid, corner, new Vector3(x, top, -0.1f));
            }
            Transform block = Prop("SM_Outskirts_CuttedWoodTrunk", corner, Vector3.zero, 0f, 0.55f, "ChoppingBlock", solid: true);
            Bounds bb = Centre(block, corner, new Vector3(-0.7f, 0f, 1.05f));
            Transform axe = Prop("SM_Axe_01", corner, new Vector3(-0.68f, corner.InverseTransformPoint(bb.max).y - 0.05f, 1.1f), 0f, 1.2f, "Axe");
            axe.localRotation = Quaternion.Euler(-150f, 30f, 0f);
            return corner;
        }

        // West pocket, between the two hut fronts: a crate with cards and a mug, three stools, and the yard's lantern
        // hung from a crossbar on a post (Y7). The lantern lights the evening corner at night.
        private static Transform DressCards(Transform cards, DayNightConfigSO dayNight)
        {
            Vector3 top = CrateTable(cards, Vector3.zero, 0f, 0.85f, "CardCrate");
            GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(deck.GetComponent<Collider>());
            deck.name = "Cards";
            deck.transform.SetParent(cards, false);
            deck.transform.localPosition = top + new Vector3(0.05f, 0.006f, -0.02f);
            deck.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            deck.transform.localScale = new Vector3(0.09f, 0.012f, 0.13f);
            deck.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MATERIALS + CARDS_MATERIAL);
            Prop("SM_Mug", cards, top + new Vector3(-0.15f, 0f, 0.15f), 0f, 1f, "Mug");
            foreach ((Vector3 at, float yaw) in new[] { (new Vector3(-0.7f, 0f, -0.15f), 10f), (new Vector3(0.15f, 0f, -0.7f), 40f), (new Vector3(0.7f, 0f, 0.15f), -20f) })
                Prop("SM_WoodStool_01a", cards, at, yaw, 1f, "Stool", solid: true);

            Vector3 post = new Vector3(-0.95f, 0f, 0.62f);
            const float HEIGHT = 1.95f;
            Pole(cards, post, HEIGHT, "LanternPost");
            // A short crossbar from the post's top toward the crate; the lantern hangs from its end.
            Vector3 barEnd = post + new Vector3(0.42f, HEIGHT - 0.12f, -0.18f);
            Transform bar = Model(WorkerHutKitBuilder.ROOF_POLE, cards, (post + Vector3.up * (HEIGHT - 0.12f) + barEnd) * 0.5f, 0f, "Crossbar");
            if (bar != null)
            {
                Vector3 along = barEnd - (post + Vector3.up * (HEIGHT - 0.12f));
                bar.localRotation = Quaternion.FromToRotation(Vector3.right, along.normalized);
                bar.localScale = new Vector3((along.magnitude + 0.1f) / 3.7f, 0.6f, 0.6f);
                bar.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
            Transform lantern = Prop("SM_Lantern_Hanging", cards, barEnd + Vector3.down * 0.29f, 0f, 1f, "Lantern");
            NightLight(lantern, "LanternLight", Vector3.zero, LANTERN_INTENSITY, LANTERN_RANGE, LightShadows.None, dayNight,
                null, null, Color.white);
            return cards;
        }

        // By the doors' pocket, against hut-02's front beside its doorway: a water barrel, a wash basin on a low bench, a
        // bucket. The old doorstep bucket and sacks moved here.
        private static Transform DressWater(Transform water)
        {
            Prop("SM_Barrel_01", water, Vector3.zero, 0f, 1f, "WaterBarrel", solid: true);
            Transform bench = Prop("SM_Bench", water, Vector3.zero, 70f, 1f, "WashBench", solid: true);
            bench.localScale = new Vector3(0.6f, 0.9f, 0.42f);
            FitCollider(bench);
            Bounds b = Centre(bench, water, new Vector3(0.75f, 0f, -0.3f));
            Transform basin = Prop("SM_Bowl_01", water, Vector3.zero, 0f, 2.4f, "WashBasin");
            Centre(basin, water, new Vector3(0.75f, water.InverseTransformPoint(b.max).y, -0.3f));
            Prop("SM_bucketA_a1", water, new Vector3(0.5f, 0f, 0.35f), 0f, 0.42f, "Bucket");
            return water;
        }

        /// <summary>
        /// Replaces a kit prop's own colliders with one box round its first mesh: the kit's collider sits on a child
        /// that does not follow a squashed (non-uniform) scale, so it no longer matches the shape.
        /// </summary>
        private static void FitCollider(Transform prop)
        {
            MeshFilter filter = prop.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            foreach (Collider old in prop.GetComponentsInChildren<Collider>(true)) old.enabled = false;
            Bounds mesh = filter.sharedMesh.bounds;
            Vector3 centre = prop.InverseTransformPoint(filter.transform.TransformPoint(mesh.center));
            Vector3 size = Vector3.Scale(mesh.size, filter.transform.lossyScale);
            size = new Vector3(size.x / prop.lossyScale.x, size.y / prop.lossyScale.y, size.z / prop.lossyScale.z);
            var box = prop.gameObject.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        // Outside the player's door: boards over the mud.
        private static Transform DressYard(Transform hut)
        {
            Transform boards = Prop("SM_AssembledPlanksB_a1", hut, new Vector3(0.55f, 0.03f, 2.45f), 8f, 1f, "DoorBoards");
            boards.localScale = new Vector3(0.62f, 0.35f, 0.62f); // thinner than it is wide: boards, not a platform
            return hut;
        }

        private const string CLOTH_PREFIX = "Cloth_";
        // Pole tops at 2.28 m and a 0.12 m flap leave the open side 2.16 m high, over the 2.1 m door rule.
        private const float BAY_POLE = 2.4f;
        private const float FLAP_DROP = 0.12f;
        private static readonly Vector3 BayPoleLeft = new Vector3(-1.15f, 0f, 0.95f);
        private static readonly Vector3 BayPoleRight = new Vector3(1.15f, 0f, 0.95f);

        // Bay frame: +Z is the open front (toward the fire), the tarp falls to stakes at the back. Two floor beds side
        // by side, heads at the back (the bed's pillow is its +X, so yaw 90 points it to −Z). Every point is put on
        // the ground where it stands (OnGround), so tarp corners and rope ends meet the poles and stakes they tie to.
        private static Transform DressEastBay(Transform bay)
        {
            Vector3 poleLeft = OnGround(bay, BayPoleLeft), poleRight = OnGround(bay, BayPoleRight);
            Pole(bay, poleLeft, BAY_POLE, "FrontPole");
            Pole(bay, poleRight, BAY_POLE, "FrontPole");
            Vector3 topLeft = poleLeft + Vector3.up * (BAY_POLE - 0.12f);
            Vector3 topRight = poleRight + Vector3.up * (BAY_POLE - 0.12f);
            Vector3 backLeft = OnGround(bay, new Vector3(-1.3f, 0f, -1.35f)) + Vector3.up * 0.2f;
            Vector3 backRight = OnGround(bay, new Vector3(1.3f, 0f, -1.35f)) + Vector3.up * 0.2f;
            Cloth(bay, "Tarp", CampClothMesh.Sheet("TarpBayEast_Tarp",
                topLeft + new Vector3(-0.12f, 0f, 0f), topRight + new Vector3(0.12f, 0f, 0f),
                backRight, backLeft, 0.18f, 11, 3), true);
            // The front edge folds over the ridge rope and hangs a hand's width down.
            Cloth(bay, "TarpFlap", CampClothMesh.Sheet("TarpBayEast_Flap",
                topLeft + new Vector3(-0.12f, 0.01f, 0f), topRight + new Vector3(0.12f, 0.01f, 0f),
                topRight + new Vector3(0.13f, -FLAP_DROP, 0.06f), topLeft + new Vector3(-0.13f, -FLAP_DROP, 0.06f), 0.02f, 12, 0), true);
            // The tarp's top edge is straight (tied along the rope), so the rope hangs only a little, just above it.
            Cloth(bay, "RidgeRope", CampClothMesh.Rope("TarpBayEast_RidgeRope",
                topLeft + new Vector3(-0.05f, 0.04f, 0f), topRight + new Vector3(0.05f, 0.04f, 0f), 0.015f), false);
            // Guy ropes from the pole tops straight forward to stakes (outward, the east stake would meet hut-07's
            // corner), and the back corners tied down to stakes.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 top = side < 0 ? topLeft : topRight;
                Vector3 front = OnGround(bay, new Vector3(side * 1.2f, 0f, 1.7f));
                Pole(bay, front + Vector3.down * 0.15f, 0.45f, "Stake");
                Cloth(bay, "GuyRope", CampClothMesh.Rope($"TarpBayEast_Guy{(side < 0 ? "L" : "R")}",
                    top + Vector3.up * 0.06f, front + Vector3.up * 0.25f, 0.05f), false);
                Vector3 back = OnGround(bay, new Vector3(side * 1.45f, 0f, -1.5f));
                Pole(bay, back + Vector3.down * 0.15f, 0.42f, "Stake");
                Cloth(bay, "BackRope", CampClothMesh.Rope($"TarpBayEast_Back{(side < 0 ? "L" : "R")}",
                    side < 0 ? backLeft : backRight, back + Vector3.up * 0.22f, 0.01f), false);
            }
            Model(FLOOR_BED, bay, OnGround(bay, new Vector3(-0.58f, 0f, -0.2f)), 87f, "FloorBed");
            Model(FLOOR_BED, bay, OnGround(bay, new Vector3(0.6f, 0f, -0.1f)), 94f, "FloorBed");
            Prop("SM_Bowl_01", bay, OnGround(bay, new Vector3(0.02f, 0f, 0.75f)), 20f, 1f, "Bowl");
            Prop("SM_Cup_01", bay, OnGround(bay, new Vector3(0.16f, 0f, 0.6f)), 0f, 0.7f, "Cup");
            Model(CLOTH_BUNDLE, bay, OnGround(bay, new Vector3(-1.12f, 0f, 0.5f)), 30f, "ClothBundle");
            // Boards over the mud in front of the open side, toward the doors' path.
            Transform boards = Prop("SM_AssembledPlanksB_a1", bay, OnGround(bay, new Vector3(0f, 0.03f, 2.45f)), 3f, 1f, "Boards");
            boards.localScale = new Vector3(0.62f, 0.35f, 0.55f);
            return bay;
        }

        /// <summary><paramref name="local"/> in <paramref name="frame"/>, raised or lowered to the ground under it.</summary>
        private static Vector3 OnGround(Transform frame, Vector3 local)
        {
            Vector3 world = frame.TransformPoint(local);
            float ground = _terrain.SampleHeight(world) + _terrain.transform.position.y;
            return local + Vector3.up * (ground - frame.position.y);
        }

        // Hobb's hut frame: its +X side wall faces hut-02 across a wedge 0.8–1.9 m wide that opens toward the escort's
        // last lane point. The tarp hangs from Hobb's eave to hut-02's wall (H1), so no post stands in the wedge, and
        // the one bed lies along the wall, turned to follow the wedge, head at the narrow end. The outer edge hangs
        // from hut-02's eave; the old line of its graybox wall (x = 2.81 + 0.5 z here) is only the fallback.
        private static Transform DressLeanTo(Transform hut, Transform neighbour)
        {
            // The outer edge: on the neighbour's eave when it is a shell, else on the line of the graybox wall.
            Vector3 outerFront = new Vector3(3.24f, 1.75f, 1.0f), outerBack = new Vector3(2.19f, 1.75f, -1.1f);
            bool tied = false;
            if (neighbour != null && neighbour.gameObject.activeInHierarchy && neighbour.lossyScale == Vector3.one
                && WorkerHutKitBuilder.TryGetVariant(neighbour.gameObject, out WorkerHutKitBuilder.Variant shell)
                && EaveFacing(neighbour, hut.position, out float side))
            {
                // The same two spots along the wedge, moved onto the neighbour's eave.
                var alongs = new float[2];
                for (int i = 0; i < 2; i++)
                    alongs[i] = Mathf.Clamp(neighbour.InverseTransformPoint(hut.TransformPoint(i == 0 ? outerFront : outerBack)).z,
                        -shell.HalfLength + 0.1f, shell.HalfLength - 0.1f);
                if (Mathf.Abs(alongs[0] - alongs[1]) >= 0.5f) // closer, the edge would collapse to a point
                {
                    for (int i = 0; i < 2; i++)
                        Cloth(hut, "EaveRope", CampClothMesh.Rope($"LeanTo_Outer{(i == 0 ? "Front" : "Back")}",
                            hut.InverseTransformPoint(EaveTie(neighbour, shell, side, alongs[i])),
                            hut.InverseTransformPoint(EaveKnot(neighbour, shell, side, alongs[i])), 0.005f), false);
                    outerFront = hut.InverseTransformPoint(EaveTie(neighbour, shell, side, alongs[0]));
                    outerBack = hut.InverseTransformPoint(EaveTie(neighbour, shell, side, alongs[1]));
                    tied = true;
                }
            }
            if (!tied)
                GameLog.Warn(TAG, $"{LEAN_TO_HUT} is not a shell hut with a side wall along the wedge: the lean-to keeps its graybox tie line.");
            Cloth(hut, "Tarp", CampClothMesh.Sheet("LeanTo_Tarp",
                new Vector3(1.72f, 2.3f, -1.1f), new Vector3(1.72f, 2.3f, 1.0f),
                outerFront, outerBack, 0.28f, 23, 2), true);
            foreach (float z in new[] { -1.1f, 1.0f })
                Cloth(hut, "EaveRope", CampClothMesh.Rope($"LeanTo_Eave{(z < 0 ? "Back" : "Front")}",
                    new Vector3(1.72f, 2.3f, z), new Vector3(1.74f, 2.43f, z), 0.005f), false);
            Model(FLOOR_BED, hut, new Vector3(2.27f, 0f, 0.3f), 104f, "FloorBed");
            // Leaning on Hobb's front wall, left of his door, blade on the ground.
            Transform spade = Model(SPADE, hut, new Vector3(-0.75f, 0f, 1.68f), 8f, "Spade");
            if (spade != null)
            {
                spade.localRotation = Quaternion.Euler(-15f, 8f, 0f);
                spade.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
            return hut;
        }

        /// <summary>
        /// World point under a shell's eave where a tarp edge can hang: just inside the roof's edge and below its
        /// boards, on the <paramref name="side"/> (±1, local x) wall, <paramref name="along"/> metres along local z.
        /// </summary>
        private static Vector3 EaveTie(Transform hut, WorkerHutKitBuilder.Variant shell, float side, float along)
        {
            float reach = Mathf.Max(shell.Overhang - 0.08f, 0f);
            float boards = WorkerHutKitBuilder.EAVE - reach * Mathf.Tan(ROOF_PITCH * Mathf.Deg2Rad) + 0.02f;
            return hut.TransformPoint(new Vector3(side * (WorkerHutKitBuilder.HALF_WIDTH + reach), boards - 0.1f, along));
        }

        /// <summary>
        /// Which side wall (±1, local x) of <paramref name="hut"/> faces <paramref name="target"/>. False when the
        /// target lies off a gable instead: there is no eave on that side to hang a tarp from.
        /// </summary>
        private static bool EaveFacing(Transform hut, Vector3 target, out float side)
        {
            Vector3 local = hut.InverseTransformPoint(target);
            side = Mathf.Sign(local.x);
            return Mathf.Abs(local.x) > Mathf.Abs(local.z);
        }

        /// <summary>Where the rope from <see cref="EaveTie"/> goes: up round the roof's edge.</summary>
        private static Vector3 EaveKnot(Transform hut, WorkerHutKitBuilder.Variant shell, float side, float along) =>
            EaveTie(hut, shell, side, along) + hut.TransformDirection(new Vector3(side * 0.02f, 0.13f, 0f));

        /// <summary>
        /// The east side of the yard, in world space: a tarp strung between the eaves of <c>worker-hut</c> and
        /// hut-06 over one floor bed, and the ramp shelter, which covers the lane between huts 05 and 06 (hut-06's
        /// doorway opens under it: a porch) and the pocket between hut-06 and the warehouse ramp (two floor beds,
        /// one under the tarp, one under the ramp itself). No poles: every corner is tied to a hut or to the ramp.
        /// A missing anchor skips its tarp with an error and leaves the rest.
        /// </summary>
        private static void DressEastYard(Transform group, Transform zone)
        {
            Transform yard = new GameObject("EastYard").transform;
            yard.SetParent(group, false);
            yard.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            bool hasPorchHut = Shell(zone, PORCH_HUT, out Transform porchHut, out WorkerHutKitBuilder.Variant porchShell);
            if (hasPorchHut && Shell(zone, EAST_HUT, out Transform eastHut, out WorkerHutKitBuilder.Variant eastShell))
            {
                if (DressEavesTarp(yard, eastHut, eastShell, porchHut, porchShell)) SwitchOff(zone, WorkerHutRollout.ReplacedTarps[0]);
            }
            if (hasPorchHut && Shell(zone, SOUTH_HUT, out Transform southHut, out WorkerHutKitBuilder.Variant southShell))
            {
                if (DressRampShelter(yard, porchHut, porchShell, southHut, southShell))
                {
                    SwitchOff(zone, WorkerHutRollout.ReplacedTarps[1]);
                    SwitchOff(zone, WorkerHutRollout.ReplacedTarps[2]);
                }
            }
        }

        private static void SwitchOff(Transform zone, string name)
        {
            Transform graybox = zone.Find(name);
            if (graybox != null) graybox.gameObject.SetActive(false);
        }

        private static bool Shell(Transform zone, string name, out Transform hut, out WorkerHutKitBuilder.Variant shell)
        {
            hut = zone.Find(name);
            shell = default;
            if (hut != null && hut.gameObject.activeInHierarchy && hut.lossyScale == Vector3.one
                && WorkerHutKitBuilder.TryGetVariant(hut.gameObject, out shell)) return true;
            GameLog.Error(TAG, $"{name} is not an active, unscaled shell hut (run Tools/World/Replace Graybox Worker Huts): the tarp tied to it is skipped.");
            return false;
        }

        /// <summary>Eave to eave between two side walls that face each other, one floor bed along the second hut.</summary>
        private static bool DressEavesTarp(Transform yard, Transform first, WorkerHutKitBuilder.Variant firstShell,
            Transform second, WorkerHutKitBuilder.Variant secondShell)
        {
            if (!EaveFacing(first, second.position, out float firstSide) || !EaveFacing(second, first.position, out float secondSide))
            {
                GameLog.Error(TAG, $"{first.name} and {second.name} do not face each other with a side wall: the eaves tarp is skipped.");
                return false;
            }
            const float SPAN = 1.2f;
            Vector3 a = EaveTie(first, firstShell, firstSide, -SPAN), b = EaveTie(first, firstShell, firstSide, SPAN);
            Vector3 c = EaveTie(second, secondShell, secondSide, SPAN), d = EaveTie(second, secondShell, secondSide, -SPAN);
            bool swap = Vector3.Distance(a, d) > Vector3.Distance(a, c); // pair each corner with the nearer one
            if (swap) (c, d) = (d, c);
            Cloth(yard, "EavesTarp", CampClothMesh.Sheet("EastYard_EavesTarp", a, b, c, d, 0.2f, 31, 2), true);
            foreach (float along in new[] { -SPAN, SPAN })
            {
                string end = along < 0 ? "Back" : "Front";
                Cloth(yard, "EaveRope", CampClothMesh.Rope($"EastYard_EavesFirst{end}",
                    EaveTie(first, firstShell, firstSide, along), EaveKnot(first, firstShell, firstSide, along), 0.005f), false);
                Cloth(yard, "EaveRope", CampClothMesh.Rope($"EastYard_EavesSecond{end}",
                    EaveTie(second, secondShell, secondSide, along), EaveKnot(second, secondShell, secondSide, along), 0.005f), false);
            }
            // Along the second hut's wall, head to the back of the wedge.
            float wall = WorkerHutKitBuilder.HALF_WIDTH + 0.6f;
            HutBed(yard, second, new Vector3(secondSide * wall, 0f, 0.1f), 90f + secondSide * 3f);
            Transform bundle = Model(CLOTH_BUNDLE, yard, Grounded(second.TransformPoint(new Vector3(secondSide * (wall + 0.35f), 0f, 1.35f))),
                second.eulerAngles.y + 40f, "ClothBundle");
            if (bundle == null) GameLog.Warn(TAG, $"Missing {CLOTH_BUNDLE}");
            return true;
        }

        /// <summary>
        /// Porch: from the ends of <paramref name="porchHut"/>'s top plates (its front gable) to the eave of
        /// <paramref name="southHut"/>. Ramp bay: from the porch hut's side eave up to the edge of the warehouse ramp.
        /// </summary>
        private static bool DressRampShelter(Transform yard, Transform porchHut, WorkerHutKitBuilder.Variant porchShell,
            Transform southHut, WorkerHutKitBuilder.Variant southShell)
        {
            // The south hut's eave must face the porch hut's front gable.
            Vector3 gable = porchHut.TransformPoint(new Vector3(0f, 0f, porchShell.HalfLength));
            if (!EaveFacing(southHut, gable, out float southSide) || porchHut.InverseTransformPoint(southHut.position).z < porchShell.HalfLength)
            {
                GameLog.Error(TAG, $"{southHut.name}'s side wall does not face {porchHut.name}'s front: the ramp shelter is skipped.");
                return false;
            }
            // Porch. The tarp's edge hangs a hand outside the gable, a rope at each end round the top plate's end.
            float front = porchShell.HalfLength + 0.14f;
            Vector3 a = porchHut.TransformPoint(new Vector3(1.385f, 2.4f, front)), b = porchHut.TransformPoint(new Vector3(-1.385f, 2.4f, front));
            const float SPAN = 1.3f;
            Vector3 c = EaveTie(southHut, southShell, southSide, -SPAN), d = EaveTie(southHut, southShell, southSide, SPAN);
            bool swap = Vector3.Distance(a, d) > Vector3.Distance(a, c);
            if (swap) (c, d) = (d, c);
            Cloth(yard, "PorchTarp", CampClothMesh.Sheet("EastYard_PorchTarp", a, b, c, d, 0.2f, 37, 3), true);
            foreach (float side in new[] { -1f, 1f })
                Cloth(yard, "EaveRope", CampClothMesh.Rope($"EastYard_PorchPlate{(side < 0 ? "A" : "B")}",
                    porchHut.TransformPoint(new Vector3(side * 1.385f, 2.4f, front)),
                    porchHut.TransformPoint(new Vector3(side * 1.385f, 2.47f, porchShell.HalfLength + 0.07f)), 0.005f), false);
            foreach (float along in new[] { -SPAN, SPAN })
                Cloth(yard, "EaveRope", CampClothMesh.Rope($"EastYard_PorchSouth{(along < 0 ? "A" : "B")}",
                    EaveTie(southHut, southShell, southSide, along), EaveKnot(southHut, southShell, southSide, along), 0.005f), false);
            // Boards over the mud in the lane, in front of the porch hut's doorway. Walk-through.
            Vector3 doorstep = Grounded(porchHut.TransformPoint(new Vector3((porchShell.DoorMinX + porchShell.DoorMaxX) * 0.5f, 0f, porchShell.HalfLength + 1.0f)));
            Transform boards = Prop("SM_AssembledPlanksB_a1", yard, doorstep + Vector3.up * 0.03f, porchHut.eulerAngles.y + 86f, 1f, "Boards");
            boards.localScale = new Vector3(0.62f, 0.35f, 0.55f);

            // Ramp bay.
            GameObject ramp = GameObject.Find(RAMP);
            if (ramp == null)
            {
                GameLog.Error(TAG, $"{RAMP} not found: the ramp bay is skipped (the porch is built).");
                return true;
            }
            // Toward the nearest point of the ramp's collider, not its pivot: the ramp is 14 m long.
            Collider deck = ramp.GetComponent<Collider>();
            Vector3 nearest = deck != null ? deck.ClosestPoint(porchHut.position + Vector3.up * 2f) : ramp.transform.position;
            if (!EaveFacing(porchHut, nearest, out float rampSide))
            {
                GameLog.Error(TAG, $"{RAMP} is not beside {porchHut.name}: the ramp bay is skipped (the porch is built).");
                return true;
            }
            float[] alongs = { porchShell.HalfLength - 0.05f, 0.1f };
            var eave = new Vector3[2];
            var edge = new Vector3[2];
            var knot = new Vector3[2];
            for (int i = 0; i < 2; i++)
            {
                eave[i] = EaveTie(porchHut, porchShell, rampSide, alongs[i]);
                if (!RampEdge(ramp.transform, eave[i], out edge[i], out knot[i]))
                {
                    GameLog.Error(TAG, $"{RAMP} does not pass {porchHut.name}'s eave above it and within 4 m: the ramp bay is skipped (the porch is built).");
                    return true;
                }
            }
            for (int i = 0; i < 2; i++)
            {
                Cloth(yard, "RampRope", CampClothMesh.Rope($"EastYard_Ramp{(i == 0 ? "Front" : "Back")}", edge[i], knot[i], 0.005f), false);
                Cloth(yard, "EaveRope", CampClothMesh.Rope($"EastYard_RampEave{(i == 0 ? "Front" : "Back")}",
                    eave[i], EaveKnot(porchHut, porchShell, rampSide, alongs[i]), 0.005f), false);
            }
            Cloth(yard, "RampTarp", CampClothMesh.Sheet("EastYard_RampTarp", edge[0], edge[1], eave[1], eave[0], 0.22f, 41, 3), true);

            // Heads to the back (the rocks), feet toward the lane: one bed under the tarp, one under the ramp.
            float wall = WorkerHutKitBuilder.HALF_WIDTH + 0.6f;
            HutBed(yard, porchHut, new Vector3(rampSide * wall, 0f, 0.9f), 90f + rampSide * 4f);
            HutBed(yard, porchHut, new Vector3(rampSide * (wall + 1.05f), 0f, 1.05f), 90f - rampSide * 5f);
            Vector3 feet = porchHut.TransformPoint(new Vector3(rampSide * (wall + 0.5f), 0f, 2.15f));
            Prop("SM_Bowl_01", yard, Grounded(feet), 20f, 1f, "Bowl");
            Prop("SM_Cup_01", yard, Grounded(feet + new Vector3(0.14f, 0f, -0.15f)), 0f, 0.7f, "Cup");
            Model(CLOTH_BUNDLE, yard, Grounded(porchHut.TransformPoint(new Vector3(rampSide * (wall + 1.75f), 0f, 0.3f))),
                porchHut.eulerAngles.y - 25f, "ClothBundle");
            return true;
        }

        /// <summary>
        /// The ramp's lower edge on the side of <paramref name="from"/>, level with it along the ramp:
        /// <paramref name="under"/> just below the deck (the tarp's corner), <paramref name="over"/> on top of it
        /// (where the rope goes round). False when the ramp does not run past that point.
        /// </summary>
        private static bool RampEdge(Transform ramp, Vector3 from, out Vector3 under, out Vector3 over)
        {
            Vector3 local = ramp.InverseTransformPoint(from);
            float side = Mathf.Sign(local.x);
            under = over = Vector3.zero;
            // Unit cube: past the ramp's ends, or under the deck itself.
            if (Mathf.Abs(local.z) > 0.48f || Mathf.Abs(local.x) < 0.5f) return false;
            Vector3 lower = ramp.TransformPoint(new Vector3(side * 0.5f, -0.5f, local.z));
            Vector3 upper = ramp.TransformPoint(new Vector3(side * 0.5f, 0.5f, local.z));
            Vector3 outward = ramp.TransformDirection(new Vector3(side, 0f, 0f)).normalized;
            under = lower + outward * 0.04f + Vector3.down * 0.05f;
            over = upper - outward * 0.05f + Vector3.up * 0.02f;
            // The tarp rises from the eave to the ramp, over a short span.
            Vector3 span = under - from;
            return under.y >= from.y && new Vector2(span.x, span.z).magnitude <= 4f;
        }

        /// <summary>A floor bed at a point of a hut's frame (outside it), on the ground there.</summary>
        private static void HutBed(Transform yard, Transform hut, Vector3 local, float yaw)
        {
            Transform bed = Model(FLOOR_BED, yard, Grounded(hut.TransformPoint(local)), hut.eulerAngles.y + yaw, "FloorBed");
            if (bed == null) GameLog.Warn(TAG, $"Missing {FLOOR_BED}");
        }

        /// <summary>A world point dropped (or raised) onto the terrain.</summary>
        private static Vector3 Grounded(Vector3 world)
        {
            world.y = _terrain.SampleHeight(world) + _terrain.transform.position.y;
            return world;
        }

        // A rope from the player's hut eave corner to the bay's west pole (bay-local +X: the bay faces south), with two
        // shirts drying on it. It spans the
        // dead-end nook between the hut and the bay, away from the walk to the bay's open side.
        private static void DressDryingLine(Transform bay, Transform playerHut)
        {
            // Tied round the end of the hut's top plate, which sticks out past the front corner on the bay's side.
            Vector3 from = bay.InverseTransformPoint(playerHut.TransformPoint(new Vector3(-1.4f, 2.46f, 1.6f)));
            Vector3 to = OnGround(bay, BayPoleRight) + Vector3.up * (BAY_POLE - 0.3f);
            const float SAG = 0.14f;
            Cloth(bay, "DryingLine", CampClothMesh.Rope("TarpBayEast_DryingLine", from, to, SAG), false);
            Vector3 along = to - from;
            // The shirt's X runs along the line and its Y stays up (FromToRotation can flip it on an opposite axis).
            Vector3 horizontal = new Vector3(along.x, 0f, along.z).normalized;
            Quaternion facing = Quaternion.LookRotation(Vector3.Cross(horizontal, Vector3.up), Vector3.up);
            foreach (float t in new[] { 0.28f, 0.72f })
            {
                Vector3 at = Vector3.Lerp(from, to, t) + Vector3.down * (SAG * 4f * t * (1f - t));
                Transform shirt = Model(SHIRT, bay, at, 0f, CLOTH_PREFIX + "Shirt");
                if (shirt == null) continue;
                shirt.localRotation = facing * Quaternion.Euler(0f, t > 0.5f ? 6f : -4f, 0f);
                shirt.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
        }

        // Boards over the mud from the doors' shared pocket toward the fire. Walk-through: kept out of the bake.
        private static Transform DressBoards(Transform boards)
        {
            foreach ((Vector3 at, float yaw) in new[] { (new Vector3(-0.8f, 0.03f, 0f), 4f), (new Vector3(0.75f, 0.03f, 0.05f), -5f) })
            {
                Transform panel = Prop("SM_AssembledPlanksB_a1", boards, at, yaw, 1f, "Boards");
                panel.localScale = new Vector3(0.62f, 0.35f, 0.55f);
            }
            return boards;
        }

        /// <summary>
        /// A bark pole standing on <paramref name="foot"/>, from the roof-pole model: solid for the player (thin capsule)
        /// and, being too thin for the bake to cut, closed to agents by a Not Walkable volume around its foot.
        /// </summary>
        private static void Pole(Transform parent, Vector3 foot, float height, string name)
        {
            Transform pole = Model(WorkerHutKitBuilder.ROOF_POLE, parent, foot + Vector3.up * (height * 0.5f), 0f, name);
            if (pole == null) return;
            // The model lies along its X, 3.7 m long: stand it up and cut it to height.
            pole.localRotation = Quaternion.Euler(0f, 0f, 90f);
            pole.localScale = new Vector3(height / 3.7f, 1f, 1f);
            var capsule = pole.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 0;
            capsule.height = 3.7f;
            capsule.radius = 0.05f;

            var block = new GameObject(name + "NavBlock").transform;
            block.SetParent(parent, false);
            block.localPosition = foot;
            var volume = block.gameObject.AddComponent<NavMeshModifierVolume>();
            volume.center = new Vector3(0f, height * 0.5f, 0f);
            volume.size = new Vector3(0.3f, height, 0.3f);
            volume.area = 1; // Not Walkable
        }

        /// <summary>A generated cloth or rope mesh, saved as an asset, under <paramref name="parent"/> at its origin.</summary>
        private static void Cloth(Transform parent, string name, Mesh mesh, bool patched)
        {
            string assetName = mesh.name;
            Mesh saved = CampClothMesh.Save(mesh, assetName);
            var go = new GameObject(CLOTH_PREFIX + name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = saved;
            var renderer = go.AddComponent<MeshRenderer>();
            var canvas = AssetDatabase.LoadAssetAtPath<Material>(MATERIALS + (patched ? TARP_MATERIAL : ROPE_MATERIAL));
            renderer.sharedMaterials = patched
                ? new[] { canvas, AssetDatabase.LoadAssetAtPath<Material>(MATERIALS + PATCH_MATERIAL) }
                : new[] { canvas };
            // Cloth and rope never block: keep them out of the render-mesh bake.
            go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        /// <summary>
        /// Warns for every solid collider in the dressing that comes within <see cref="LANE_CLEARANCE"/> of the arrival
        /// escort's lanes (left and right chains, each ending at the tent mate's and the watch spot).
        /// </summary>
        private static void CheckLanes(Transform group, Transform zone)
        {
            // A prop inside a hut's room stands behind that hut's wall: the wall is what the escort passes, not the prop
            // (worker-hut-03's walls already stand 0.34–0.55 m from the east lane since the rollout).
            var rooms = new System.Collections.Generic.List<(Transform hut, float halfLength)>();
            foreach (Transform hut in zone)
                if (WorkerHutKitBuilder.TryGetVariant(hut.gameObject, out WorkerHutKitBuilder.Variant shell))
                    rooms.Add((hut, shell.HalfLength - (WorkerHutKitBuilder.HALF_WIDTH - HomeYardHomes.ROOM_HALF)));
            GameObject lanes = GameObject.Find(LANES);
            if (lanes == null)
            {
                GameLog.Warn(TAG, $"{LANES} not found; lane clearance not checked.");
                return;
            }
            // Chains by the index in each name (HutLane_L_0, _1…), not by sibling order.
            var left = new System.Collections.Generic.SortedDictionary<int, Vector3>();
            var right = new System.Collections.Generic.SortedDictionary<int, Vector3>();
            foreach (Transform point in lanes.transform)
            {
                bool isLeft = point.name.StartsWith("HutLane_L_"), isRight = point.name.StartsWith("HutLane_R_");
                if ((!isLeft && !isRight) || !int.TryParse(point.name.Substring(10), out int index)) continue;
                (isLeft ? left : right)[index] = point.position;
            }
            var leftChain = new System.Collections.Generic.List<Vector3>(left.Values);
            var rightChain = new System.Collections.Generic.List<Vector3>(right.Values);
            Transform mate = lanes.transform.Find("TentMateSpot"), watch = lanes.transform.Find("HutWatchSpot");
            if (mate != null) leftChain.Add(mate.position);
            if (watch != null) rightChain.Add(watch.position);
            int close = 0;
            foreach (Collider collider in group.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled) continue;
                Bounds b = collider.bounds;
                bool indoors = false;
                foreach ((Transform hut, float halfLength) in rooms)
                {
                    Vector3 local = hut.InverseTransformPoint(b.center);
                    if (Mathf.Abs(local.x) < HomeYardHomes.ROOM_HALF && Mathf.Abs(local.z) < halfLength) { indoors = true; break; }
                }
                if (indoors) continue;
                foreach (var chain in new[] { leftChain, rightChain })
                    for (int i = 0; i + 1 < chain.Count; i++)
                    {
                        float d = DistanceXZ(b, chain[i], chain[i + 1]);
                        if (d >= LANE_CLEARANCE) continue;
                        close++;
                        GameLog.Warn(TAG, $"'{collider.transform.parent?.name}/{collider.name}' is {d:F2} m from lane segment {i}; keep {LANE_CLEARANCE} m.");
                    }
            }
            if (close == 0) GameLog.Info(TAG, $"Lane clearance OK ({LANE_CLEARANCE} m).");
        }

        private static float DistanceXZ(Bounds b, Vector3 a, Vector3 c)
        {
            // Sample the segment every 5 cm and measure to the bounds' footprint (world AABB: rotated props read a
            // little closer than they are, which errs on the safe side).
            float best = float.MaxValue;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, c) / 0.05f));
            for (int s = 0; s <= steps; s++)
            {
                Vector3 p = Vector3.Lerp(a, c, s / (float)steps);
                float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
                float dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        // The lintel's outer face is 3 cm inside the wall centre line: the flap hangs against it.
        private const float FLAP_INSET = 0.025f;

        /// <summary>The linen door flap, tied back to the side of the doorway.</summary>
        private static void Flap(Transform hut)
        {
            float doorCentre = (WorkerHutKitBuilder.DOOR_MIN_X + WorkerHutKitBuilder.DOOR_MAX_X) * 0.5f;
            Transform flap = Model(DOOR_FLAP, hut, new Vector3(doorCentre, 0f, WorkerHutKitBuilder.HOME_HALF_LENGTH - FLAP_INSET), 0f, "DoorFlap");
            if (flap == null)
            {
                GameLog.Warn(TAG, $"Missing {DOOR_FLAP}; the doorway stays bare.");
                return;
            }
            // Cloth has no collider and must not close the doorway in the bake, which collects render meshes.
            flap.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        private static void NightLight(Transform owner, string name, Vector3 local, float intensity, float range,
            LightShadows shadows, DayNightConfigSO dayNight, Renderer[] glow, Transform billboard, Color glowColor)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(owner, false);
            lightObject.transform.localPosition = local;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = FireColor;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows;
            light.enabled = false; // ClockNightLight switches it on at dusk

            var nightLight = owner.gameObject.AddComponent<ClockNightLight>();
            var so = new SerializedObject(nightLight);
            so.FindProperty("_config").objectReferenceValue = dayNight;
            so.FindProperty("_light").objectReferenceValue = light;
            so.FindProperty("_billboard").objectReferenceValue = billboard;
            so.FindProperty("_glowColor").colorValue = glowColor;
            SerializedProperty renderers = so.FindProperty("_glowRenderers");
            renderers.arraySize = glow == null ? 0 : glow.Length;
            for (int i = 0; i < renderers.arraySize; i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue = glow[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Renderer GlowQuad(string name, Transform parent, Vector3 size)
        {
            var glow = AssetDatabase.LoadAssetAtPath<Material>(GLOW);
            if (glow == null)
            {
                GameLog.Warn(TAG, $"Missing {GLOW}; the fire has no flame.");
                return null;
            }
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localScale = size;
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false; // lit by ClockNightLight; also keeps it out of render-mesh bakes
            return renderer;
        }

        private static Transform Prop(string kitName, Transform parent, Vector3 local, float yaw, float scale, string name, bool solid = false)
        {
            Transform prop = WorkerHutKitBuilder.Piece(kitName, parent, local);
            prop.localRotation = Quaternion.Euler(0f, yaw, 0f);
            prop.localScale = Vector3.one * scale;
            WorkerHutKitBuilder.Name(prop, name);
            StripKitLights(prop);

            if (!solid)
            {
                // Scene bakes collect render meshes: a prop the player walks through must not carve the NavMesh either.
                prop.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                return prop;
            }
            Collider[] colliders = prop.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders) collider.enabled = true;
            if (colliders.Length == 0) GameLog.Warn(TAG, $"'{name}' ({kitName}) is marked solid but has no collider.");
            return prop;
        }

        /// <summary>
        /// Kit prefabs bring always-on lights that also carry a missing HDRP script (a warning on every load). The
        /// yard's lights are the clock-driven ones added here, so the prop is unpacked and its lights removed. Such a
        /// prop is no longer a prefab instance.
        /// </summary>
        private static void StripKitLights(Transform prop)
        {
            Light[] lights = prop.GetComponentsInChildren<Light>(true);
            if (lights.Length == 0) return;
            PrefabUtility.UnpackPrefabInstance(prop.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (Light light in lights)
            {
                if (light == null) continue; // already gone with a parent
                GameObject holder = light.gameObject;
                bool onlyALight = holder.transform != prop && holder.transform.childCount == 0 && holder.GetComponent<Renderer>() == null;
                if (onlyALight)
                {
                    Object.DestroyImmediate(holder);
                    continue;
                }
                Object.DestroyImmediate(light);
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(holder);
            }
        }

        private static Transform Model(string path, Transform parent, Vector3 local, float yaw, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;
            Transform model = ((GameObject)PrefabUtility.InstantiatePrefab(asset, parent)).transform;
            model.localPosition = local;
            model.localRotation = Quaternion.Euler(0f, yaw, 0f);
            model.name = name;
            return model;
        }

        /// <summary>A dressing group that shares a hut's position and rotation.</summary>
        private static Transform Child(string name, Transform group, Transform anchor)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(group, false);
            child.SetPositionAndRotation(anchor.position, anchor.rotation);
            return child;
        }

        /// <summary>A dressing group on the terrain at a world XZ point.</summary>
        private static Transform WorldChild(string name, Transform group, Vector2 at, float yaw)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(group, false);
            var point = new Vector3(at.x, 0f, at.y);
            point.y = _terrain.SampleHeight(point) + _terrain.transform.position.y;
            child.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
            return child;
        }
    }
}
