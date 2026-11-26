using System.Collections.Generic;
using Game.AI;
using Game.Core;
using Game.Inventory;
using Game.NPC;
using Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// <c>Tools/World/Settle Home Yard Owners</c>: gives Tam, Pim and Gerrit, and Bren their huts
    /// (<see cref="HomeYardHomes"/>): an owned bed each (Pim's and Gerrit's existing ones move in), an owned box with a
    /// little loot, the shell's restricted area switched on for its owners, and Sleep blocks in Tam's and Bren's
    /// routines. An authoring command on hand-owned objects (it uses <c>GameObject.Find</c>), rerunnable: the same poses
    /// and values, no duplicates. Checks everything before changing anything; never saves, never bakes.
    /// </summary>
    internal static class HomeYardOwners
    {
        private const string TAG = "[HomeYardOwners]";
        private const string ZONE = "Area1_Blockout/TentCamp/PlayersZone";
        private const string BED = "Assets/_Game/Prefabs/World/Beds/Bed_WorkerPallet.prefab";
        private const string CONTAINER = "Assets/_Game/Prefabs/World/Containers/Base_Container.prefab";
        private const string BOX_MODEL = "SM_TallSquareWoodBoxClosed";
        private const string ITEMS = "t:ItemSO ";
        // Hobb's box: the kit box's pivot sits off its middle, so the model is offset to centre it on the collider.
        private static readonly Vector3 BoxModelOffset = new Vector3(-0.59f, -0.05f, 1.03f);
        private const float BOX_MODEL_SCALE = 0.8f;
        // The bed prefab's NPC approach stands beside its +Z side; a bed on the +X wall is reached from its −Z side.
        private static readonly Vector3 ApproachOnMinusZ = new Vector3(-0.01f, 0.23f, -1.08f);
        // The prefab's own approach pose (Hobb's bed): beside the +Z side, facing the bed.
        private static readonly Vector3 ApproachOnPlusZ = new Vector3(-0.01f, 0.23f, 1.08f);
        private const float APPROACH_YAW = 179.48f;
        private static readonly Vector3 SleepAnchor = new Vector3(0.2f, -0.48f, 0.09f);

        /// <summary>The bed's point named <paramref name="name"/>: the one it references, a child of that name, or a new one.</summary>
        private static Transform Point(Transform bed, SerializedProperty field, string name, Vector3 local, float yaw)
        {
            var point = field.objectReferenceValue as Transform;
            if (point == null) point = bed.Find(name);
            if (point == null)
            {
                point = new GameObject(name).transform;
                point.SetParent(bed, false);
                point.localPosition = local;
                point.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
            field.objectReferenceValue = point;
            return point;
        }

        [MenuItem("Tools/World/Settle Home Yard Owners")]
        private static void Menu() => Run();

        internal static bool Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                GameLog.Error(TAG, "Leave Play mode and close Prefab Mode first; nothing changed.");
                return false;
            }
            if (!Gather(out Inputs inputs, out string problem))
            {
                GameLog.Error(TAG, problem + "; nothing changed.");
                return false;
            }

            var beds = new Dictionary<string, Bed>();
            foreach (HomeYardHomes.Home home in HomeYardHomes.All)
            {
                Transform hut = inputs.Huts[home.Key];
                foreach (HomeYardHomes.BedSpot spot in home.Beds)
                    beds[spot.Name ?? spot.ExistingPath] = SettleBed(inputs, hut, spot);
                SettleBox(inputs, hut, home);
                OpenRestrictedArea(inputs, hut, home);
            }
            foreach ((string npcPath, HomeYardHomes.Block[] blocks) in HomeYardHomes.Routines)
                WriteRoutine(inputs.Routines[npcPath], blocks, inputs.Spots, beds);

            EditorSceneManager.MarkSceneDirty(inputs.Zone.gameObject.scene);
            GameLog.Info(TAG, "Home yard owners settled. Rebake the NavMesh, then check every bed's NPC approach on it.");
            return true;
        }

        private sealed class Inputs
        {
            internal Transform Zone;
            internal GameObject BedPrefab, ContainerPrefab;
            internal readonly Dictionary<string, Transform> Huts = new Dictionary<string, Transform>();
            internal readonly Dictionary<string, NPCEntity> Entities = new Dictionary<string, NPCEntity>();
            internal readonly Dictionary<string, ItemSO> Items = new Dictionary<string, ItemSO>();
            internal readonly Dictionary<string, Transform> ExistingBeds = new Dictionary<string, Transform>();
            internal readonly Dictionary<string, NpcRoutine> Routines = new Dictionary<string, NpcRoutine>();
            internal readonly Dictionary<string, Transform> Spots = new Dictionary<string, Transform>();
        }

        /// <summary>Resolves every input; false with the first missing one named.</summary>
        private static bool Gather(out Inputs inputs, out string problem)
        {
            inputs = new Inputs();
            problem = null;
            GameObject zone = GameObject.Find(ZONE);
            if (zone == null) { problem = $"{ZONE} not found"; return false; }
            inputs.Zone = zone.transform;
            inputs.BedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BED);
            inputs.ContainerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CONTAINER);
            if (inputs.BedPrefab == null) { problem = $"Missing {BED}"; return false; }
            if (inputs.ContainerPrefab == null) { problem = $"Missing {CONTAINER}"; return false; }

            foreach (HomeYardHomes.Home home in HomeYardHomes.All)
            {
                string layout = HomeYardHomes.Problem(home);
                if (layout != null) { problem = "Layout: " + layout; return false; }
                Transform hut = inputs.Zone.Find(home.Hut);
                if (hut == null || !WorkerHutKitBuilder.TryGetVariant(hut.gameObject, out WorkerHutKitBuilder.Variant shell)
                    || hut.lossyScale != Vector3.one)
                { problem = $"{ZONE}/{home.Hut} is not an unscaled shell hut"; return false; }
                if (shell.Mirrored != home.Mirrored || shell.Long)
                { problem = $"{home.Hut} is {shell.Name}: the layout is written for a 3 × 3 m shell {(home.Mirrored ? "with" : "without")} a mirrored doorway"; return false; }
                if (hut.Find(WorkerHutKitBuilder.RESTRICTED_AREA)?.GetComponent<RestrictedArea>() == null)
                { problem = $"{home.Hut} has no {WorkerHutKitBuilder.RESTRICTED_AREA}"; return false; }
                inputs.Huts[home.Key] = hut;
                foreach (string owner in home.Owners)
                    if (!Entity(inputs, owner)) { problem = $"Missing {owner}"; return false; }
                foreach (HomeYardHomes.BedSpot bed in home.Beds)
                {
                    if (!Entity(inputs, bed.Owner)) { problem = $"Missing {bed.Owner}"; return false; }
                    if (bed.ExistingPath == null) continue;
                    GameObject existing = GameObject.Find(bed.ExistingPath);
                    if (existing == null || existing.GetComponent<Bed>() == null) { problem = $"Bed {bed.ExistingPath} not found"; return false; }
                    inputs.ExistingBeds[bed.ExistingPath] = existing.transform;
                }
                foreach ((string item, int _) in home.Loot)
                    if (!Item(inputs, item)) { problem = $"Item {item} not found"; return false; }
            }
            foreach ((string npcPath, HomeYardHomes.Block[] blocks) in HomeYardHomes.Routines)
            {
                if (!HomeYardHomes.CoversTheDay(blocks)) { problem = $"{npcPath}'s routine leaves hours without a block"; return false; }
                NpcRoutine routine = GameObject.Find(npcPath)?.GetComponent<NpcRoutine>();
                if (routine == null) { problem = $"{npcPath} has no NpcRoutine"; return false; }
                inputs.Routines[npcPath] = routine;
                foreach (HomeYardHomes.Block block in blocks)
                {
                    if (block.Spot != null && !inputs.Spots.ContainsKey(block.Spot))
                    {
                        GameObject spot = GameObject.Find(block.Spot);
                        if (spot == null) { problem = $"Spot {block.Spot} not found"; return false; }
                        inputs.Spots[block.Spot] = spot.transform;
                    }
                    if (block.Activity == NpcActivity.Sleep && !BedIsInTable(block.Bed)) { problem = $"Bed {block.Bed} is not in the homes table"; return false; }
                }
            }
            return true;
        }

        private static bool BedIsInTable(string name)
        {
            foreach (HomeYardHomes.Home home in HomeYardHomes.All)
                foreach (HomeYardHomes.BedSpot bed in home.Beds)
                    if (bed.Name == name) return true;
            return false;
        }

        private static bool Entity(Inputs inputs, string path)
        {
            if (inputs.Entities.ContainsKey(path)) return true;
            var entity = AssetDatabase.LoadAssetAtPath<NPCEntity>(path);
            if (entity == null) return false;
            inputs.Entities[path] = entity;
            return true;
        }

        private static bool Item(Inputs inputs, string name)
        {
            if (inputs.Items.ContainsKey(name)) return true;
            foreach (string guid in AssetDatabase.FindAssets(ITEMS + name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;
                var item = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
                if (item == null) continue;
                inputs.Items[name] = item;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ beds

        private static Bed SettleBed(Inputs inputs, Transform hut, HomeYardHomes.BedSpot spot)
        {
            Transform bed;
            if (spot.ExistingPath != null) bed = inputs.ExistingBeds[spot.ExistingPath];
            else
            {
                bed = inputs.Zone.Find(spot.Name);
                if (bed == null)
                {
                    bed = ((GameObject)PrefabUtility.InstantiatePrefab(inputs.BedPrefab, inputs.Zone)).transform;
                    bed.name = spot.Name;
                }
            }
            // On the floor line (the shell has no floor: its origin stands on the ground), head to the back.
            bed.SetPositionAndRotation(hut.TransformPoint(spot.Local), hut.rotation * Quaternion.Euler(0f, 90f, 0f));

            var component = bed.GetComponent<Bed>();
            var so = new SerializedObject(component);
            so.FindProperty("_nameTag").stringValue = spot.NameTag;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetOwner(bed.gameObject, inputs.Entities[spot.Owner]);

            // The prefab has neither point: every bed in the scene carries them as added children (Hobb's, the tithe
            // barn's, Tob's), with these poses.
            var points = new SerializedObject(component);
            Transform approach = Point(bed, points.FindProperty("_npcApproach"), "NpcApproach", ApproachOnPlusZ, APPROACH_YAW);
            Point(bed, points.FindProperty("_npcSleepAnchor"), "NpcSleepAnchor", SleepAnchor, 270f);
            points.ApplyModifiedPropertiesWithoutUndo();
            if (approach != null)
            {
                // Written outright, not reverted: the tithe-barn beds are not linked to the prefab.
                approach.localPosition = spot.OnPlusXWall ? ApproachOnMinusZ : ApproachOnPlusZ;
                approach.localRotation = Quaternion.Euler(0f, spot.OnPlusXWall ? APPROACH_YAW - 180f : APPROACH_YAW, 0f);
            }
            else GameLog.Warn(TAG, $"{bed.name} has no NPC approach point: its owner cannot reach it.");
            return component;
        }

        /// <summary>The component, added when missing (Unity's destroyed / missing objects are not null for <c>??</c>).</summary>
        private static T Ensure<T>(GameObject target) where T : Component =>
            target.TryGetComponent(out T existing) ? existing : target.AddComponent<T>();

        private static void SetOwner(GameObject target, NPCEntity owner)
        {
            var ownership = Ensure<Ownership>(target);
            var so = new SerializedObject(ownership);
            so.FindProperty("_owner").objectReferenceValue = owner;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ boxes

        /// <summary>As <c>HobbsBox</c>: a container with its chest visual off and a kit box as its model.</summary>
        private static void SettleBox(Inputs inputs, Transform hut, HomeYardHomes.Home home)
        {
            Transform box = inputs.Zone.Find(home.BoxName);
            if (box == null)
            {
                box = ((GameObject)PrefabUtility.InstantiatePrefab(inputs.ContainerPrefab, inputs.Zone)).transform;
                box.name = home.BoxName;
            }
            box.SetPositionAndRotation(hut.TransformPoint(home.BoxLocal), hut.rotation);

            Transform visual = box.Find("Visual");
            if (visual != null)
            {
                var vso = new SerializedObject(visual.gameObject);
                vso.FindProperty("m_IsActive").boolValue = false;
                vso.ApplyModifiedPropertiesWithoutUndo();
            }
            Transform model = box.Find("BoxModel");
            if (model == null)
            {
                model = WorkerHutKitBuilder.Piece(BOX_MODEL, box, BoxModelOffset);
                model.name = "BoxModel";
            }
            model.localPosition = BoxModelOffset;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one * BOX_MODEL_SCALE;

            var collider = Ensure<BoxCollider>(box.gameObject);
            collider.center = new Vector3(0f, HomeYardHomes.BOX_SIZE * 0.5f - 0.01f, 0f);
            collider.size = new Vector3(HomeYardHomes.BOX_SIZE, HomeYardHomes.BOX_SIZE - 0.02f, HomeYardHomes.BOX_SIZE);

            var interactable = new SerializedObject(box.GetComponent<ContainerInteractable>());
            interactable.FindProperty("_interactPrompt").stringValue = "Open";
            interactable.FindProperty("_nameTag").stringValue = home.BoxTag;
            interactable.ApplyModifiedPropertiesWithoutUndo();

            var inventory = new SerializedObject(box.GetComponent<InventorySystem>());
            SerializedProperty items = inventory.FindProperty("_startingItems");
            items.arraySize = home.Loot.Length;
            for (int i = 0; i < home.Loot.Length; i++)
            {
                SerializedProperty entry = items.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("item").objectReferenceValue = inputs.Items[home.Loot[i].item];
                entry.FindPropertyRelative("count").intValue = home.Loot[i].count;
            }
            inventory.ApplyModifiedPropertiesWithoutUndo();

            SetOwner(box.gameObject, inputs.Entities[home.Owners[0]]);

            var highlight = Ensure<InteractionHighlight>(box.gameObject);
            var hso = new SerializedObject(highlight);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            SerializedProperty targets = hso.FindProperty("_targets");
            targets.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) targets.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            hso.ApplyModifiedPropertiesWithoutUndo();

            if (box.GetComponent<SaveableObject>() == null) box.gameObject.AddComponent<SaveableObject>();
            var persistent = Ensure<PersistentID>(box.gameObject);
            var pso = new SerializedObject(persistent);
            pso.FindProperty("_killedFact").objectReferenceValue = SaveKey(home);
            pso.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The box's save key: a <see cref="KilledFact"/> asset, made once and reused on every rerun.</summary>
        private static KilledFact SaveKey(HomeYardHomes.Home home)
        {
            var fact = AssetDatabase.LoadAssetAtPath<KilledFact>(home.KilledFactPath);
            if (fact != null) return fact;
            string folder = System.IO.Path.GetDirectoryName(home.KilledFactPath)?.Replace('\\', '/');
            if (folder != null && !AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/'), System.IO.Path.GetFileName(folder));
            fact = ScriptableObject.CreateInstance<KilledFact>().Init(home.BoxSaveKey);
            AssetDatabase.CreateAsset(fact, home.KilledFactPath);
            AssetDatabase.SaveAssetIfDirty(fact);
            return fact;
        }

        // ------------------------------------------------------------------ restricted areas

        /// <summary>
        /// Switched on through <see cref="SerializedObject"/> (a scene override that a hut rebuild keeps, Huts/CLAUDE.md →
        /// Restricted Area), owners listed, all day, one warning: Hobb's settings.
        /// </summary>
        private static void OpenRestrictedArea(Inputs inputs, Transform hut, HomeYardHomes.Home home)
        {
            Transform child = hut.Find(WorkerHutKitBuilder.RESTRICTED_AREA);
            var go = new SerializedObject(child.gameObject);
            go.FindProperty("m_IsActive").boolValue = true;
            go.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(child.GetComponent<RestrictedArea>());
            SerializedProperty owners = so.FindProperty("_ownerEntities");
            owners.arraySize = home.Owners.Length;
            for (int i = 0; i < home.Owners.Length; i++)
                owners.GetArrayElementAtIndex(i).objectReferenceValue = inputs.Entities[home.Owners[i]];
            so.FindProperty("_ownerFactions").arraySize = 0;
            so.FindProperty("_useHours").boolValue = false;
            so.FindProperty("_warningsBeforeFight").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ routines

        private static void WriteRoutine(NpcRoutine routine, HomeYardHomes.Block[] blocks, Dictionary<string, Transform> spots,
            Dictionary<string, Bed> beds)
        {
            var so = new SerializedObject(routine);
            SerializedProperty list = so.FindProperty("_blocks");
            list.arraySize = blocks.Length;
            for (int i = 0; i < blocks.Length; i++)
            {
                HomeYardHomes.Block block = blocks[i];
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("label").stringValue = block.Label;
                entry.FindPropertyRelative("startHour").intValue = block.Start;
                entry.FindPropertyRelative("endHour").intValue = block.End;
                entry.FindPropertyRelative("activity").enumValueIndex = (int)block.Activity;
                entry.FindPropertyRelative("spot").objectReferenceValue = block.Spot != null ? spots[block.Spot] : null;
                entry.FindPropertyRelative("waypoints").arraySize = 0;
                entry.FindPropertyRelative("bed").objectReferenceValue =
                    block.Activity == NpcActivity.Sleep && beds.TryGetValue(block.Bed, out Bed bed) ? bed : null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
