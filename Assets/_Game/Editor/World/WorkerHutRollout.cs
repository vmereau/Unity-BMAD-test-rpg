using System.Collections.Generic;
using System.Text;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Swaps the graybox 3 × 3 m worker huts of the Players and Mafia zones for the board-built shells, in place:
    /// same name, parent, sibling order, x, z and yaw, seated on the ground. Which shell a plot gets is decided by
    /// <see cref="WorkerHutRolloutRules"/> from what is measured here. Huts that are already shells are left
    /// alone, so the command can be rerun; plots it cannot swap stay graybox and are listed in the report.
    /// </summary>
    internal static class WorkerHutRollout
    {
        private const string TAG = "[WorkerHutRollout]";
        private const string CAMP = "Area1_Blockout/TentCamp";
        private const string LANES = "ArrivalSequence/HutLanes";
        private static readonly string[] Zones = { "PlayersZone", "MafiaZone" };
        private const string HUT_PREFIX = "worker-";
        private const string TARP_PREFIX = "worker-tarp";
        // Graybox tarps of PlayersZone that the home yard dresser replaces: their posts must not count as blocking a
        // doorway. One list for both tools.
        internal const string REPLACED_TARPS_ZONE = "PlayersZone";
        internal static readonly string[] ReplacedTarps = { "worker-tarp-north", "worker-tarp-north (4)", "worker-tarp-north (5)" };
        // Every part a graybox box hut is made of. Any other child (the market hut's counter) is moved into the
        // shell that replaces the hut, where it stood, and listed in the report.
        private static readonly string[] GrayboxParts =
        {
            "Side-1", "Side1", "FramePost", "Back", "Lintel", "DoorSide", "PlankRoof", "RidgeBeam", "GableBrace", "PlankFloor",
        };

        // The graybox hut this command knows: walls on ±1.44 m, the door between two DoorSide panels on local +Z.
        private const float GRAYBOX_WALL = 1.44f;
        private const float GRAYBOX_TOLERANCE = 0.06f;
        private const float WALK_GAP = 0.6f;
        private const float LANE_CLEARANCE = 0.7f;
        private const float NEIGHBOUR_RANGE = 4.5f;
        private const float DOOR_STEP = 0.3f; // the player's step height: higher ground outside a doorway blocks it
        private const string DRESSING = "HomeYardDressing"; // rebuilt by the dresser round the huts: never a blocker

        private static Terrain _terrain;

        private sealed class Hut
        {
            internal Transform Transform;
            internal string Zone;
            internal bool Graybox3x3;
            internal string Variant;      // shell name once known (existing instance or chosen)
            internal Vector2 RoofSize;    // footprint of the roof it has, or will have
            internal float SeatY;
        }

        [MenuItem("Tools/World/Replace Graybox Worker Huts")]
        private static void Menu()
        {
            if (!EditorUtility.DisplayDialog("Replace Graybox Worker Huts",
                    "Swaps every graybox 3 × 3 m hut of PlayersZone and MafiaZone for a board-built shell and deletes the graybox. Huts that are already shells are left alone.",
                    "Replace", "Cancel"))
                return;
            Run();
        }

        /// <summary>Runs the swap without asking and returns the report (also logged).</summary>
        internal static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                return Fail("Leave Play mode and close Prefab Mode first; nothing changed.");
            GameObject camp = GameObject.Find(CAMP);
            Terrain terrain = camp != null ? SceneTerrain(camp.scene) : null;
            if (camp == null || terrain == null) return Fail($"Need {CAMP} and a terrain in its scene; nothing changed.");
            _terrain = terrain;
            var prefabs = new Dictionary<string, GameObject>();
            var variants = new Dictionary<string, WorkerHutKitBuilder.Variant>();
            foreach (WorkerHutKitBuilder.Variant variant in WorkerHutKitBuilder.Variants)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(variant.PrefabPath);
                if (prefab == null)
                    return Fail($"Missing {variant.PrefabPath}: run Tools/World/Build Worker Hut Kit Prefabs first; nothing changed.");
                prefabs[variant.Name] = prefab;
                variants[variant.Name] = variant;
            }

            foreach (string name in new[]
                     {
                         WorkerHutRolloutRules.HOME, WorkerHutRolloutRules.HOME_B, WorkerHutRolloutRules.HOME_MIRRORED,
                         WorkerHutRolloutRules.TIGHT, WorkerHutRolloutRules.TIGHT_MIRRORED, WorkerHutRolloutRules.TIGHT_C,
                         WorkerHutRolloutRules.TIGHT_MIRRORED_D, WorkerHutRolloutRules.LONG,
                     })
                if (!variants.ContainsKey(name))
                    return Fail($"{nameof(WorkerHutRolloutRules)} names the shell '{name}', which is not in {nameof(WorkerHutKitBuilder)}.{nameof(WorkerHutKitBuilder.Variants)}; nothing changed.");

            Physics.SyncTransforms();
            List<Hut> huts = Collect(camp.transform, variants);
            List<Vector3[]> lanes = LaneSegments();
            var report = new StringBuilder();
            int swapped = 0, skipped = 0, kept = 0;

            // In name order, so the neighbour rule sees the same camp on every machine.
            huts.Sort((a, b) => string.CompareOrdinal(a.Zone + "/" + a.Transform.name, b.Zone + "/" + b.Transform.name));
            foreach (Hut hut in huts)
            {
                if (hut.Variant != null) { kept++; continue; }
                if (!hut.Graybox3x3)
                {
                    // A hut of these zones that is neither a shell nor the 3 × 3 m box (the narrower Mafia huts).
                    if (System.Array.IndexOf(Zones, hut.Zone) >= 0)
                    {
                        skipped++;
                        report.AppendLine($"SKIPPED {hut.Zone}/{hut.Transform.name}: not the plain 3 × 3 m graybox hut (other wall lines or door side, scaled or tilted, or a component on its root)");
                    }
                    continue;
                }
                WorkerHutRolloutRules.Plot plot = Measure(hut, huts, terrain, lanes, variants);
                string[] candidates = WorkerHutRolloutRules.Candidates(plot, out string reason);
                if (candidates.Length == 0)
                {
                    skipped++;
                    report.AppendLine($"SKIPPED {hut.Zone}/{hut.Transform.name}: {reason}");
                    continue;
                }
                hut.Variant = WorkerHutRolloutRules.Pick(candidates, NeighbourVariants(hut, huts));
                hut.RoofSize = RoofSize(variants[hut.Variant]);
            }

            // Its own undo step, also when called from a script; closed even if a swap throws half way.
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Replace Graybox Worker Huts");
            int group = Undo.GetCurrentGroup();
            try
            {
                foreach (Hut hut in huts)
                {
                    if (!hut.Graybox3x3 || hut.Variant == null) continue;
                    Transform old = hut.Transform;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[hut.Variant], old.parent);
                    Undo.RegisterCreatedObjectUndo(instance, "Replace Graybox Worker Huts");
                    instance.name = old.name;
                    instance.transform.SetSiblingIndex(old.GetSiblingIndex());
                    instance.transform.SetPositionAndRotation(new Vector3(old.position.x, hut.SeatY, old.position.z),
                        Quaternion.Euler(0f, old.eulerAngles.y, 0f));
                    report.AppendLine($"{hut.Zone}/{old.name}: {hut.Variant}");
                    // Whatever is not a graybox part stays in the scene, in the new hut, where it stood.
                    var extras = new List<Transform>();
                    foreach (Transform part in old)
                        if (System.Array.IndexOf(GrayboxParts, part.name) < 0) extras.Add(part);
                    foreach (Transform extra in extras)
                    {
                        Undo.SetTransformParent(extra, instance.transform, "Replace Graybox Worker Huts");
                        report.AppendLine($"KEPT {hut.Zone}/{old.name}/{extra.name}: moved into the shell at its old place; check it against the new walls and doorway");
                    }
                    hut.Transform = instance.transform;
                    hut.Graybox3x3 = false;
                    Undo.DestroyObjectImmediate(old.gameObject);
                    swapped++;
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
                if (swapped > 0) EditorSceneManager.MarkSceneDirty(camp.scene);
            }

            // Roofs that still cross after the choice (a shell against a shell or against a hut left graybox):
            // listed, never moved.
            for (int i = 0; i < huts.Count; i++)
                for (int j = i + 1; j < huts.Count; j++)
                    if ((huts[i].Variant != null || huts[j].Variant != null)
                        && Area1WorkerGeometry.Overlap(Roof(huts[i]), Roof(huts[j])))
                        report.AppendLine($"ROOFS CROSS {Label(huts[i])} / {Label(huts[j])}");

            report.Insert(0, $"{swapped} swapped, {skipped} left graybox, {kept} already shells.\n");
            _terrain = null;
            string text = report.ToString();
            if (skipped > 0) GameLog.Warn(TAG, text);
            else GameLog.Info(TAG, text);
            if (swapped > 0) GameLog.Info(TAG, "Rebake the NavMesh before play.");
            return text;
        }

        private static string Label(Hut hut) => $"{hut.Zone}/{hut.Transform.name} ({hut.Variant ?? "graybox"})";

        private static string Fail(string message)
        {
            GameLog.Error(TAG, message);
            return message;
        }

        /// <summary>Every hut of the camp (all zones, so neighbours across a zone line count), shells and graybox.</summary>
        private static List<Hut> Collect(Transform camp, Dictionary<string, WorkerHutKitBuilder.Variant> variants)
        {
            var huts = new List<Hut>();
            foreach (Transform zone in camp)
            {
                bool ours = System.Array.IndexOf(Zones, zone.name) >= 0;
                foreach (Transform child in zone)
                {
                    if (!child.gameObject.activeInHierarchy || !child.name.StartsWith(HUT_PREFIX) || child.name.StartsWith(TARP_PREFIX))
                        continue;
                    var hut = new Hut { Transform = child, Zone = zone.name };
                    if (WorkerHutKitBuilder.TryGetVariant(child.gameObject, out WorkerHutKitBuilder.Variant variant))
                    {
                        hut.Variant = variant.Name;
                        hut.RoofSize = RoofSize(variant);
                    }
                    else
                    {
                        hut.Graybox3x3 = ours && IsGraybox3x3(child);
                        // A graybox neighbour that gets swapped can always take the tight roof, which is no larger
                        // than the graybox one: so the graybox roof is what a plot has to clear.
                        hut.RoofSize = GrayboxRoofSize(child);
                    }
                    huts.Add(hut);
                }
            }
            return huts;
        }

        /// <summary>Walls on ±1.44 m, an unscaled upright root and the door panels on local +Z.</summary>
        private static bool IsGraybox3x3(Transform hut)
        {
            if (PrefabUtility.IsPartOfAnyPrefab(hut.gameObject) || hut.lossyScale != Vector3.one) return false;
            if (Mathf.Abs(Mathf.DeltaAngle(hut.eulerAngles.x, 0f)) > 0.5f || Mathf.Abs(Mathf.DeltaAngle(hut.eulerAngles.z, 0f)) > 0.5f) return false;
            // The root is deleted: a component on it (an owner, a save ID) must not go with it.
            if (hut.GetComponents<Component>().Length > 1) return false;
            int sides = 0, doorPanels = 0;
            foreach (Transform part in hut)
            {
                Vector3 at = part.localPosition;
                if (part.name.StartsWith("Side") && Mathf.Abs(Mathf.Abs(at.x) - GRAYBOX_WALL) < GRAYBOX_TOLERANCE) sides++;
                if (part.name == "DoorSide" && Mathf.Abs(at.z - GRAYBOX_WALL) < GRAYBOX_TOLERANCE) doorPanels++;
            }
            return sides == 2 && doorPanels == 2;
        }

        /// <summary>Footprint of a graybox hut's roof from its <c>PlankRoof</c> slabs and <c>RidgeBeam</c>.</summary>
        private static Vector2 GrayboxRoofSize(Transform hut)
        {
            float halfX = 0f, halfZ = 0f;
            foreach (Transform part in hut)
            {
                if (part.name != "PlankRoof" && part.name != "RidgeBeam") continue;
                float tilt = part.localEulerAngles.z * Mathf.Deg2Rad;
                halfX = Mathf.Max(halfX, Mathf.Abs(part.localPosition.x) + part.localScale.x * 0.5f * Mathf.Abs(Mathf.Cos(tilt)));
                halfZ = Mathf.Max(halfZ, Mathf.Abs(part.localPosition.z) + part.localScale.z * 0.5f);
            }
            if (halfX <= 0f || halfZ <= 0f)
            {
                // Not the box hut: fall back on what it draws.
                Bounds bounds = new Bounds(hut.position, Vector3.zero);
                foreach (Renderer renderer in hut.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                float half = Mathf.Max(bounds.extents.x, bounds.extents.z);
                return new Vector2(half * 2f, half * 2f);
            }
            return new Vector2(halfX * 2f, halfZ * 2f);
        }

        private static Vector2 RoofSize(WorkerHutKitBuilder.Variant variant) =>
            new Vector2(variant.RoofHalfWidth * 2f, variant.RoofHalfLength * 2f);

        private static Vector2[] Roof(Hut hut) => Roof(hut, hut.RoofSize);

        private static Vector2[] Roof(Hut hut, Vector2 size)
        {
            Vector3 at = hut.Transform.position;
            return Area1WorkerGeometry.Corners(new Vector2(at.x, at.z), size, hut.Transform.eulerAngles.y);
        }

        private static WorkerHutRolloutRules.Plot Measure(Hut hut, List<Hut> huts, Terrain terrain, List<Vector3[]> lanes,
            Dictionary<string, WorkerHutKitBuilder.Variant> variants)
        {
            Transform t = hut.Transform;
            WorkerHutKitBuilder.Variant home = variants[WorkerHutRolloutRules.HOME];
            WorkerHutKitBuilder.Variant longHut = variants[WorkerHutRolloutRules.LONG];
            hut.SeatY = Ground(terrain, t.position);
            Ground(hut, terrain, home.HalfLength, out float rise, out float drop);

            bool crowded = Crosses(hut, huts, RoofSize(home), 0f) || RoofHitsBuilding(hut, home);
            bool defaultFree = DoorFree(hut, WorkerHutKitBuilder.DOOR_MIN_X, WorkerHutKitBuilder.DOOR_MAX_X, home.HalfLength)
                               && ThresholdFree(hut, WorkerHutKitBuilder.DOOR_MIN_X, WorkerHutKitBuilder.DOOR_MAX_X, home.HalfLength);
            bool mirroredFree = DoorFree(hut, -WorkerHutKitBuilder.DOOR_MAX_X, -WorkerHutKitBuilder.DOOR_MIN_X, home.HalfLength)
                                && ThresholdFree(hut, -WorkerHutKitBuilder.DOOR_MAX_X, -WorkerHutKitBuilder.DOOR_MIN_X, home.HalfLength);

            bool longFits = false;
            if (!crowded)
            {
                Ground(hut, terrain, longHut.HalfLength, out float longRise, out float longDrop);
                longFits = longRise <= WorkerHutRolloutRules.MAX_RISE && longDrop <= WorkerHutRolloutRules.MAX_DROP
                           && !Crosses(hut, huts, RoofSize(longHut), WALK_GAP)
                           && !RoofHitsBuilding(hut, longHut)
                           && ExtraMetreFree(hut, longHut)
                           && DoorFree(hut, WorkerHutKitBuilder.DOOR_MIN_X, WorkerHutKitBuilder.DOOR_MAX_X, longHut.HalfLength)
                           && !NearLane(hut, longHut, lanes);
            }
            return new WorkerHutRolloutRules.Plot(t.name, rise, drop, defaultFree, mirroredFree, crowded, longFits);
        }

        private static float Ground(Terrain terrain, Vector3 world) => terrain.SampleHeight(world) + terrain.transform.position.y;

        /// <summary>Highest ground in the room above the floor line, lowest ground under the wall lines below it.</summary>
        private static void Ground(Hut hut, Terrain terrain, float halfLength, out float rise, out float drop)
        {
            rise = 0f;
            drop = 0f;
            const int STEPS = 6;
            for (int i = 0; i <= STEPS; i++)
                for (int j = 0; j <= STEPS; j++)
                {
                    float u = i / (float)STEPS * 2f - 1f, v = j / (float)STEPS * 2f - 1f;
                    var local = new Vector3(u * WorkerHutKitBuilder.HALF_WIDTH, 0f, v * halfLength);
                    Vector3 world = hut.Transform.position + Quaternion.Euler(0f, hut.Transform.eulerAngles.y, 0f) * local;
                    float delta = Ground(terrain, world) - hut.SeatY;
                    bool underWalls = i == 0 || i == STEPS || j == 0 || j == STEPS;
                    if (underWalls) drop = Mathf.Max(drop, -delta);
                    bool inRoom = Mathf.Abs(local.x) < WorkerHutKitBuilder.HALF_WIDTH - 0.2f && Mathf.Abs(local.z) < halfLength - 0.2f;
                    if (inRoom) rise = Mathf.Max(rise, delta);
                }
        }

        private static bool Crosses(Hut hut, List<Hut> huts, Vector2 roofSize, float gap)
        {
            Vector2[] roof = Roof(hut, roofSize);
            foreach (Hut other in huts)
                if (other != hut && Area1WorkerGeometry.Overlap(roof, Roof(other), gap)) return true;
            return false;
        }

        /// <summary>True when the roof's volume meets a solid collider that is not a camp hut (those are tested by footprint).</summary>
        private static bool RoofHitsBuilding(Hut hut, WorkerHutKitBuilder.Variant variant)
        {
            Quaternion yaw = Quaternion.Euler(0f, hut.Transform.eulerAngles.y, 0f);
            var centre = new Vector3(hut.Transform.position.x, hut.SeatY + WorkerHutKitBuilder.EAVE + 0.45f, hut.Transform.position.z);
            var half = new Vector3(variant.RoofHalfWidth, 0.6f, variant.RoofHalfLength);
            return AnySolid(Physics.OverlapBox(centre, half, yaw, ~0, QueryTriggerInteraction.Ignore), hut, ignoreHuts: true);
        }

        /// <summary>
        /// Nothing solid from 0.25 m to 1.15 m outside the wall line in front of the doorway, between knee and head
        /// height (the threshold itself is the hut's own sill; low things are stepped over).
        /// </summary>
        private static bool DoorFree(Hut hut, float minX, float maxX, float halfLength)
        {
            Quaternion yaw = Quaternion.Euler(0f, hut.Transform.eulerAngles.y, 0f);
            var local = new Vector3((minX + maxX) * 0.5f, 1.1f, halfLength + 0.25f + 0.45f);
            Vector3 centre = new Vector3(hut.Transform.position.x, hut.SeatY, hut.Transform.position.z) + yaw * local;
            var half = new Vector3((maxX - minX) * 0.5f - 0.1f, 0.7f, 0.45f);
            if (AnySolid(Physics.OverlapBox(centre, half, yaw, ~0, QueryTriggerInteraction.Ignore), hut, ignoreHuts: false)) return false;
            // The terrain is not a collider hit: a bank right outside the doorway blocks it all the same.
            foreach (float out_ in new[] { 0.4f, 0.9f })
            {
                Vector3 step = new Vector3(hut.Transform.position.x, 0f, hut.Transform.position.z)
                               + yaw * new Vector3((minX + maxX) * 0.5f, 0f, halfLength + out_);
                if (Ground(_terrain, step) - hut.SeatY > DOOR_STEP) return false;
            }
            return true;
        }

        /// <summary>
        /// Nothing the hut keeps (a child that is not a graybox part, like the market counter) stands in the first
        /// metre inside the doorway: such a child moves into the shell and would block the way in.
        /// </summary>
        private static bool ThresholdFree(Hut hut, float minX, float maxX, float halfLength)
        {
            Quaternion yaw = Quaternion.Euler(0f, hut.Transform.eulerAngles.y, 0f);
            Vector3 centre = new Vector3(hut.Transform.position.x, hut.SeatY, hut.Transform.position.z)
                             + yaw * new Vector3((minX + maxX) * 0.5f, 1.1f, halfLength - 0.7f);
            var half = new Vector3((maxX - minX) * 0.5f - 0.1f, 0.7f, 0.5f);
            foreach (Collider hit in Physics.OverlapBox(centre, half, yaw, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(hut.Transform) || hit.transform == hut.Transform) continue;
                Transform part = hit.transform;
                while (part.parent != hut.Transform) part = part.parent;
                if (System.Array.IndexOf(GrayboxParts, part.name) < 0) return false;
            }
            return true;
        }

        /// <summary>The half metre a long hut adds at each end, plus the walking gap, holds nothing solid.</summary>
        private static bool ExtraMetreFree(Hut hut, WorkerHutKitBuilder.Variant longHut)
        {
            Quaternion yaw = Quaternion.Euler(0f, hut.Transform.eulerAngles.y, 0f);
            Vector3 origin = new Vector3(hut.Transform.position.x, hut.SeatY, hut.Transform.position.z);
            float depth = (longHut.HalfLength - WorkerHutKitBuilder.HOME_HALF_LENGTH + WALK_GAP) * 0.5f;
            foreach (float end in new[] { -1f, 1f })
            {
                Vector3 centre = origin + yaw * new Vector3(0f, 1.3f, end * (WorkerHutKitBuilder.HOME_HALF_LENGTH + 0.1f + depth));
                var half = new Vector3(WorkerHutKitBuilder.HALF_WIDTH + 0.1f, 0.9f, depth);
                if (AnySolid(Physics.OverlapBox(centre, half, yaw, ~0, QueryTriggerInteraction.Ignore), hut, ignoreHuts: false)) return false;
            }
            return true;
        }

        private static bool NearLane(Hut hut, WorkerHutKitBuilder.Variant variant, List<Vector3[]> lanes)
        {
            Vector2[] walls = Roof(hut, new Vector2(WorkerHutKitBuilder.HALF_WIDTH * 2f + 0.2f, variant.HalfLength * 2f + 0.2f));
            foreach (Vector3[] lane in lanes)
                if (Area1WorkerGeometry.Corridor(walls, new Vector2(lane[0].x, lane[0].z), new Vector2(lane[1].x, lane[1].z), LANE_CLEARANCE * 2f))
                    return true;
            return false;
        }

        /// <summary>
        /// True when a hit is something that stays: not this hut, not the terrain, not a graybox tarp the dresser
        /// replaces, and (for the roof test) not another camp hut.
        /// </summary>
        private static bool AnySolid(Collider[] hits, Hut hut, bool ignoreHuts)
        {
            foreach (Collider hit in hits)
            {
                if (hit is TerrainCollider || hit.transform.IsChildOf(hut.Transform)) continue;
                // Things that move (a character, a loose item) are not part of the plot.
                if (hit is CharacterController || hit.attachedRigidbody != null) continue;
                bool ignored = false;
                for (Transform t = hit.transform; t != null && !ignored; t = t.parent)
                {
                    if (t.name == DRESSING) ignored = true;
                    else if (System.Array.IndexOf(ReplacedTarps, t.name) >= 0 && t.parent != null && t.parent.name == REPLACED_TARPS_ZONE) ignored = true;
                    else if (ignoreHuts && t.name.StartsWith(HUT_PREFIX) && !t.name.StartsWith(TARP_PREFIX)
                             && t.parent != null && t.parent.parent != null && t.parent.parent.name == "TentCamp") ignored = true;
                }
                if (!ignored) return true;
            }
            return false;
        }

        private static List<string> NeighbourVariants(Hut hut, List<Hut> huts)
        {
            var names = new List<string>();
            Vector3 at = hut.Transform.position;
            foreach (Hut other in huts)
            {
                if (other == hut || other.Variant == null) continue;
                Vector3 to = other.Transform.position - at;
                if (new Vector2(to.x, to.z).magnitude < NEIGHBOUR_RANGE) names.Add(other.Variant);
            }
            return names;
        }

        /// <summary>The escort's lane chains as segments (left and right, by the index in each point's name).</summary>
        private static List<Vector3[]> LaneSegments()
        {
            var segments = new List<Vector3[]>();
            GameObject lanes = GameObject.Find(LANES);
            if (lanes == null)
            {
                GameLog.Warn(TAG, $"{LANES} not found; lanes not checked for long huts.");
                return segments;
            }
            foreach (string prefix in new[] { "HutLane_L_", "HutLane_R_" })
            {
                var chain = new SortedDictionary<int, Vector3>();
                foreach (Transform point in lanes.transform)
                    if (point.name.StartsWith(prefix) && int.TryParse(point.name.Substring(prefix.Length), out int index))
                        chain[index] = point.position;
                Vector3? previous = null;
                foreach (Vector3 point in chain.Values)
                {
                    if (previous.HasValue) segments.Add(new[] { previous.Value, point });
                    previous = point;
                }
            }
            return segments;
        }

        private static Terrain SceneTerrain(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
                if (terrain.gameObject.scene == scene) return terrain;
            return null;
        }
    }
}
