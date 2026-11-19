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
        private const string BED_MODEL = "Assets/_Game/Art/Environment/Camp/models/worker bed/WorkerBed_Model.prefab";
        private const string CARDS_MATERIAL = "M_Camp_SackCloth.mat";
        private const string DOOR_FLAP = "Assets/_Game/Art/Environment/Camp/models/door flap/DoorFlap.fbx";

        // World positions of the graybox groups this pass replaces.
        private static readonly Vector2 FirePoint = new Vector2(5.55f, 38.95f);
        private static readonly Vector2 TrunkPoint = new Vector2(4.19f, 41.96f);
        private static readonly Vector2 CardsPoint = new Vector2(3.30f, 39.15f);
        private static readonly string[] GrayboxGroups = { "Campfire", "RepairTrunk", "CardGame" };

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
            foreach (string asset in new[] { BED_MODEL, DOOR_FLAP, GLOW, MATERIALS + CARDS_MATERIAL })
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
            Prop("SM_Crate_01", hut, new Vector3(1.31f, 0f, -1.22f), 4f, 0.45f, "CrateTable", solid: true);
            Vector3 top = new Vector3(1.07f, 0.47f, -1f);
            Prop("SM_Candle_01c", hut, top + new Vector3(0.1f, 0f, -0.08f), 0f, 1f, "Candle");
            Prop("SM_Bowl_01", hut, top + new Vector3(-0.1f, 0f, 0.06f), 30f, 1f, "Bowl");
            Prop("SM_Sack_01", hut, new Vector3(0.45f, 0f, -1.08f), 70f, 1f, "Sack");
            Prop("SM_burlapSackA_a1", hut, new Vector3(1.05f, 0.2f, -0.35f), 80f, 0.8f, "Sack");
            Transform rope = Prop("SM_Rope_Bundle_01", hut, new Vector3(1.36f, 1.45f, 0.35f), 0f, 0.6f, "HangingRope");
            rope.localRotation = Quaternion.Euler(0f, 90f, 90f);
            Transform peg = Prop("SM_Damaged_Plank_A_01", hut, new Vector3(1.36f, 1.72f, 0.55f), 90f, 1f, "PegRail");
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
            Transform bed = Model(BED_MODEL, hut, new Vector3(-0.89f, 0f, -0.26f), 90f, "HobbBed");
            if (bed == null) GameLog.Warn(TAG, $"Missing {BED_MODEL}");
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
            Transform rope = Prop("SM_Rope_Bundle_01", hut, new Vector3(1.36f, 1.5f, 0.1f), 0f, 0.6f, "HangingRope");
            rope.localRotation = Quaternion.Euler(0f, 90f, 90f);
            Flap(hut);
        }

        // The arrival escort's two lanes pass 1.6 m either side of the fire (ArrivalSequence/HutLanes) and the card
        // game's logs already narrow the west one, so the fire gets one stool to the north and nothing to the south.
        // The wood pile stands east of the lanes.
        private static Transform DressFire(Transform fire, DayNightConfigSO dayNight)
        {
            Prop("SM_FireCamp_01b", fire, Vector3.zero, 40f, 0.85f, "FireRing", solid: true);
            // The ring nests the kit's own fire and smoke particles, which read as thin smoke in URP and stay on all
            // day. The flame itself is a night-only quad, like the hunter's fire.
            Transform billboard = new GameObject("Glow").transform;
            billboard.SetParent(fire, false);
            billboard.localPosition = new Vector3(0f, 0.45f, 0f);
            Renderer flame = GlowQuad("Flame", billboard, new Vector3(0.55f, 0.8f, 1f));
            Prop("SM_WoodStool_01a", fire, new Vector3(-0.65f, 0f, 1.35f), 0f, 1f, "Stool", solid: true);
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

        /// <summary>The linen door flap, tied back to the side of the doorway.</summary>
        private static void Flap(Transform hut)
        {
            float doorCentre = (WorkerHutKitBuilder.DOOR_MIN_X + WorkerHutKitBuilder.DOOR_MAX_X) * 0.5f;
            Transform flap = Model(DOOR_FLAP, hut, new Vector3(doorCentre, 0f, WorkerHutKitBuilder.HOME_HALF_LENGTH), 0f, "DoorFlap");
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
