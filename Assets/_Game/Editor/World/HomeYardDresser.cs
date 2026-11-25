using Game.Core;
using Game.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Art pass for the player's home yard in StartingTown: props inside the player's and Hobb's huts, the shared
    /// fire, the repair trunk, the card game and the night lights. Everything goes under
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

        // World positions of the graybox groups this pass replaces.
        private static readonly Vector2 FirePoint = new Vector2(5.55f, 38.95f);
        private static readonly Vector2 TrunkPoint = new Vector2(4.19f, 41.96f);
        private static readonly Vector2 CardsPoint = new Vector2(3.30f, 39.15f);
        // Near worker-tarp-north (3) (5.56, 47.11), on flat open ground east of the player's door, open side to the south
        // (the fire). Moved 1.26 m west of the graybox: there, graybox hut-07's north-west wall (from (5.62, 45.63) to
        // (7.45, 47.88)) cut through the bay's east half.
        private static readonly Vector2 EastBayPoint = new Vector2(4.3f, 47.11f);
        // The path of boards from the two doors toward the fire, east of the doors' shared pocket.
        private static readonly Vector2 BoardsPoint = new Vector2(2.6f, 43.65f);
        // worker-tarp-north (1) sat in the wedge between Hobb's hut and hut-02 and overlapped the kit hut: the
        // lean-to replaces it; (3) becomes the two-bed bay.
        private static readonly string[] GrayboxGroups =
            { "Campfire", "RepairTrunk", "CardGame", "worker-tarp-north (1)", "worker-tarp-north (3)" };

        private const string GLOW = "Assets/_Game/Art/Materials/Blockout_FireGlow.mat";

        // Tuned in Play at 23:00 against the H3 concept: a small warm pool, the rest of the yard dark but readable.
        private static readonly Color FireColor = new Color(1f, 0.66f, 0.38f);
        private static readonly Color FlameGlow = new Color(2.2f, 0.95f, 0.3f, 1f);
        private const float FIRE_INTENSITY = 7f;
        private const float FIRE_RANGE = 10f;
        private const float CANDLE_INTENSITY = 2.4f; // the candle stands in the far corner, 1.5 m from the bed
        private const float CANDLE_RANGE = 4.5f;

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
                         MATERIALS + ROPE_MATERIAL,
                     })
                if (AssetDatabase.LoadMainAssetAtPath(asset) == null)
                {
                    GameLog.Error(TAG, $"Missing {asset}; nothing changed.");
                    return;
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
            SettleOnTerrain(DressFire(WorldChild("Fire", group, FirePoint, 0f), dayNight));
            SettleOnTerrain(DressTrunk(WorldChild("RepairTrunk", group, TrunkPoint, 156f)));
            SettleOnTerrain(DressCards(WorldChild("CardGame", group, CardsPoint, 0f)));
            SettleOnTerrain(DressYard(Child("Doorstep", group, playerHut)));
            // Not settled afterwards: the bay places everything on the ground itself, so the tie points of its tarp and
            // ropes follow the poles and stakes they hang from.
            Transform eastBay = DressEastBay(WorldChild("TarpBayEast", group, EastBayPoint, 180f));
            DressDryingLine(eastBay, playerHut);
            SettleOnTerrain(DressLeanTo(Child("LeanTo", group, mateHut)));
            SettleOnTerrain(DressBoards(WorldChild("MudBoards", group, BoardsPoint, 62f)));
            CheckLanes(group);

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
            // In the corner away from the bed, so the strip beside the bed stays clear to the back wall. The crate's
            // pivot is at one bottom corner, hence the offset from the table top below.
            Prop("SM_Crate_01", hut, new Vector3(1.26f, 0f, -1.22f), 4f, 0.45f, "CrateTable", solid: true);
            Vector3 top = new Vector3(1.02f, 0.47f, -1f);
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
            Prop("SM_Crate_02", hut, new Vector3(0.75f, 0f, -1.25f), -6f, 0.5f, "CrateTable", solid: true);
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
            Prop("SM_firewoodSetA_a1", fire, new Vector3(3.05f, 0f, -0.3f), 80f, 0.8f, "Firewood", solid: true);
            Prop("SM_Outskirts_CuttedWoodTrunk", fire, new Vector3(3.15f, 0f, 2.75f), 0f, 0.7f, "ChoppingBlock", solid: true);
            Transform axe = Prop("SM_Axe_01", fire, new Vector3(3.15f, 0.48f, 2.85f), 0f, 1.2f, "Axe");
            axe.localRotation = Quaternion.Euler(-150f, 30f, 0f);
            NightLight(fire, "FireLight", new Vector3(0f, 0.9f, 0f), FIRE_INTENSITY, FIRE_RANGE, LightShadows.Soft, dayNight,
                flame != null ? new[] { flame } : null, billboard, FlameGlow);
            return fire;
        }

        private static Transform DressTrunk(Transform trunk)
        {
            Prop("SM_WoodChest_01a", trunk, Vector3.zero, 90f, 1f, "Trunk", solid: true);
            Prop("SM_RockStone_01b", trunk, new Vector3(-0.9f, 0.08f, 0.3f), 30f, 0.3f, "Whetstone");
            Transform tool = Prop("SM_Axe_01", trunk, new Vector3(0.1f, 0.64f, 0f), 0f, 1f, "Hatchet");
            tool.localRotation = Quaternion.Euler(90f, 40f, 0f);
            Prop("SM_Sack_02", trunk, new Vector3(0f, 0f, -0.8f), 60f, 1f, "Sack");
            return trunk;
        }

        // Where the graybox card game stood: a crate (pivot at one bottom corner, so offset to centre it on the group)
        // with a log seat on three sides, west of the escort lane.
        private static Transform DressCards(Transform cards)
        {
            Prop("SM_Crate_02", cards, new Vector3(0.36f, 0f, -0.36f), 0f, 0.7f, "CardCrate", solid: true);
            GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(deck.GetComponent<Collider>());
            deck.name = "Cards";
            deck.transform.SetParent(cards, false);
            deck.transform.localPosition = new Vector3(0.05f, 0.71f, -0.02f);
            deck.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            deck.transform.localScale = new Vector3(0.09f, 0.012f, 0.13f);
            deck.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MATERIALS + CARDS_MATERIAL);
            Prop("SM_Log_Wide_B", cards, new Vector3(-0.42f, 0f, 0.95f), 90f, 0.6f, "LogSeat", solid: true);
            Prop("SM_Log_Wide_B", cards, new Vector3(-0.6f, 0f, -0.95f), 90f, 0.6f, "LogSeat", solid: true);
            Prop("SM_Log_Wide_B", cards, new Vector3(-0.95f, 0f, -0.4f), 0f, 0.6f, "LogSeat", solid: true);
            Prop("SM_Mug", cards, new Vector3(-0.2f, 0.71f, 0.2f), 0f, 1f, "Mug");
            return cards;
        }

        // Outside the player's door: boards over the mud, a bucket, a few sacks against the wall.
        private static Transform DressYard(Transform hut)
        {
            Transform boards = Prop("SM_AssembledPlanksB_a1", hut, new Vector3(0.55f, 0.03f, 2.45f), 8f, 1f, "DoorBoards");
            boards.localScale = new Vector3(0.62f, 0.35f, 0.62f); // thinner than it is wide: boards, not a platform
            Prop("SM_bucketA_a1", hut, new Vector3(-0.55f, 0f, 1.95f), 0f, 0.55f, "Bucket", solid: true);
            Prop("SM_burlapSackSetA_a1", hut, new Vector3(-1.05f, 0f, 2.1f), 40f, 0.7f, "Sacks", solid: true);
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
        // the one bed lies along the wall, turned to follow the wedge, head at the narrow end. hut-02 is still
        // graybox: its outer wall face is the line x = 2.81 + 0.5 z here, so a rebuilt hut-02 needs the tie points moved.
        private static Transform DressLeanTo(Transform hut)
        {
            Cloth(hut, "Tarp", CampClothMesh.Sheet("LeanTo_Tarp",
                new Vector3(1.72f, 2.3f, -1.1f), new Vector3(1.72f, 2.3f, 1.0f),
                new Vector3(3.24f, 1.75f, 1.0f), new Vector3(2.19f, 1.75f, -1.1f), 0.28f, 23, 2), true);
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
        private static void CheckLanes(Transform group)
        {
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
