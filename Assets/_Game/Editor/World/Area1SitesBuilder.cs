using System.Collections.Generic;
using Game.Core;
using Game.World;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Editor.World
{
    /// <summary>
    /// Rebuilds the graybox sites under Area1_Blockout from docs/World/Areas/area-001/layout.json. A run replaces only the
    /// roots it owns. Rebake the NavMesh afterwards.
    /// </summary>
    public static class Area1SitesBuilder
    {
        private const string TAG = "[Area1SitesBuilder]";

        private const string BLOCKOUT_ROOT = "Area1_Blockout";
        private const string QUARRY = "Quarry";
        private const string CLEFT_FEATURE = "quarry-cleft";
        private const string RUINS2_FEATURE = "zone-4";
        private const string BLOCKER_LAYER = "Ignore Raycast";
        private const string TIMBER = "Assets/_Game/Art/Materials/Blockout_Timber.mat";
        private const string STONE = "Assets/_Game/Art/Materials/Blockout_Stone.mat";
        private const string FACT = "Assets/_Game/Data/Facts/WorldFact_QuarryPassageOpened.asset";
        private const string FACT_EVENT = "Assets/_Game/Data/Events/OnWorldFactChanged.asset";

        private const float BLOCKER_ALONG = 6f;  // metres into the cleft from its quarry end, where the walls have risen
        private const float BLOCKER_WIDTH = 14f; // the walls flare, so it runs well into them
        private const float BLOCKER_HEIGHT = 8f;
        private const float BLOCKER_DEPTH = 1.6f;
        private const float BLOCKER_BURY = 1f;
        private const float RING_OFFSET = 10f;   // out from the Ruins 2 pad, on the cliff's inner face
        private const float RING_BOTTOM = 20f;   // world height, below the ground along the whole loop
        private const float RING_TOP = 75f;      // world height, well over the crest and a jump
        private const float RING_THICKNESS = 1f;
        private const int PLANKS = 5;
        private const int RUBBLE = 26;
        private const int SEED = 1337;

        [MenuItem("Tools/World/Build Area 1 Sites")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                GameLog.Error(TAG, "Leave play mode first; nothing changed.");
                return;
            }

            Terrain terrain = Terrain.activeTerrain;
            GameObject root = GameObject.Find(BLOCKOUT_ROOT);
            Material timber = AssetDatabase.LoadAssetAtPath<Material>(TIMBER);
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(STONE);
            WorldFact fact = AssetDatabase.LoadAssetAtPath<WorldFact>(FACT);
            GameEventSO_Fact factEvent = AssetDatabase.LoadAssetAtPath<GameEventSO_Fact>(FACT_EVENT);
            if (terrain == null || terrain.terrainData == null || root == null || timber == null || stone == null || fact == null || factEvent == null)
            {
                GameLog.Error(TAG, $"Need an active terrain, {BLOCKOUT_ROOT}, both blockout materials, {FACT} and {FACT_EVENT}; nothing changed.");
                return;
            }

            if (!Area1BlockoutBuilder.TryReadLayout(out Dictionary<string, JObject> features, out string problem))
            {
                GameLog.Error(TAG, $"{problem}; nothing changed.");
                return;
            }

            if (!features.TryGetValue(CLEFT_FEATURE, out JObject cleft) || !Area1BlockoutBuilder.HasPoints(cleft))
            {
                GameLog.Error(TAG, $"Layout has no usable feature '{CLEFT_FEATURE}'; nothing changed.");
                return;
            }

            if (!features.TryGetValue(RUINS2_FEATURE, out JObject ruins2) || !Area1BlockoutBuilder.HasPosition(ruins2)
                || ruins2["w"] == null || ruins2["d"] == null)
            {
                GameLog.Error(TAG, $"Layout has no usable feature '{RUINS2_FEATURE}'; nothing changed.");
                return;
            }

            Transform old = root.transform.Find(QUARRY);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var quarry = new GameObject(QUARRY).transform;
            quarry.SetParent(root.transform, false);
            Vector2[] line = Area1BlockoutBuilder.Points(cleft);
            BuildCleftBlocker(line, terrain, timber, stone, fact, factEvent, quarry);
            BuildRuinsRing(line, ruins2, quarry);

            EditorSceneManager.MarkSceneDirty(root.scene);
            GameLog.Info(TAG, "Done. Rebake the NavMesh.");
        }

        /// <summary>
        /// Baked as if it weren't there, so the cleft stays in the NavMesh, and carved at runtime. Removing it needs no rebake.
        /// </summary>
        private static void BuildCleftBlocker(Vector2[] line, Terrain terrain, Material timber, Material stone, WorldFact fact, GameEventSO_Fact factEvent, Transform parent)
        {
            Vector2 direction = (line[1] - line[0]).normalized;
            Vector2 at = BlockerPoint(line);
            float ground = Area1BlockoutBuilder.Ground(terrain, at);

            var blocker = new GameObject("CleftBlocker").transform;
            blocker.SetParent(parent, false);
            blocker.SetPositionAndRotation(new Vector3(at.x, ground, at.y), Area1BlockoutBuilder.Yaw(direction));
            blocker.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var seal = new GameObject("Seal").transform;
            seal.SetParent(blocker, false);

            var wall = seal.gameObject.AddComponent<BoxCollider>();
            wall.center = new Vector3(0f, BLOCKER_HEIGHT * 0.5f - BLOCKER_BURY, 0f);
            wall.size = new Vector3(BLOCKER_WIDTH, BLOCKER_HEIGHT, BLOCKER_DEPTH);

            var obstacle = seal.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = wall.center;
            obstacle.size = wall.size;
            obstacle.carving = true;
            obstacle.enabled = false; // a bake cuts an enabled carving obstacle into the NavMesh; FactRemovedObject enables it at runtime

            // Closes the air over the seal, so nobody crosses the cliff above the cleft and drops in behind it.
            var cap = new GameObject("Cap").transform;
            cap.SetParent(seal, false);
            cap.position = new Vector3(at.x, (ground + RING_TOP) * 0.5f, at.y);
            cap.gameObject.layer = LayerMask.NameToLayer(BLOCKER_LAYER);
            cap.gameObject.AddComponent<BoxCollider>().size = new Vector3(BLOCKER_WIDTH, RING_TOP - ground, BLOCKER_DEPTH);

            AddVisuals(seal, timber, stone);

            var remover = blocker.gameObject.AddComponent<FactRemovedObject>();
            var so = new SerializedObject(remover);
            so.FindProperty("_fact").objectReferenceValue = fact;
            so.FindProperty("_target").objectReferenceValue = seal.gameObject;
            so.FindProperty("_onFactChanged").objectReferenceValue = factEvent;
            so.FindProperty("_runtimeCarve").objectReferenceValue = obstacle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The player can jump up any steep face, so the cliffs alone don't seal Ruins 2. This invisible loop runs from one end
        /// of the blocker around the pad to the other, so with the seal in place there is no way in, and without it only the cleft.
        /// </summary>
        private static void BuildRuinsRing(Vector2[] line, JObject ruins2, Transform parent)
        {
            Vector2 at = BlockerPoint(line);
            Vector2 direction = (line[1] - line[0]).normalized;
            Vector2 across = new Vector2(-direction.y, direction.x) * (BLOCKER_WIDTH * 0.5f); // towards the quarry's east side
            Vector2 start = at + across, end = at - across;

            Vector2 center = Area1BlockoutBuilder.Position(ruins2);
            float east = center.x + (float)ruins2["w"] * 0.5f + RING_OFFSET;
            float west = center.x - (float)ruins2["w"] * 0.5f - RING_OFFSET;
            float south = center.y - (float)ruins2["d"] * 0.5f - RING_OFFSET;
            Vector2[] loop =
            {
                start, new Vector2(east, start.y), new Vector2(east, south), new Vector2(west, south), new Vector2(west, end.y), end,
            };

            var ring = new GameObject("RuinsRing").transform;
            ring.SetParent(parent, false);
            for (int i = 1; i < loop.Length; i++)
            {
                Vector2 middle = (loop[i - 1] + loop[i]) * 0.5f;
                Vector2 along = loop[i] - loop[i - 1];
                var wall = new GameObject($"Wall_{i - 1}").transform;
                wall.SetParent(ring, false);
                wall.SetPositionAndRotation(new Vector3(middle.x, (RING_BOTTOM + RING_TOP) * 0.5f, middle.y), Area1BlockoutBuilder.Yaw(along));
                wall.gameObject.layer = LayerMask.NameToLayer(BLOCKER_LAYER);
                wall.gameObject.isStatic = true;
                wall.gameObject.AddComponent<BoxCollider>().size =
                    new Vector3(RING_THICKNESS, RING_TOP - RING_BOTTOM, along.magnitude + RING_THICKNESS);
            }
        }

        private static Vector2 BlockerPoint(Vector2[] line) => line[0] + (line[1] - line[0]).normalized * BLOCKER_ALONG;

        private static void AddVisuals(Transform seal, Material timber, Material stone)
        {
            var random = new System.Random(SEED);
            float Range(float from, float to) => from + (float)random.NextDouble() * (to - from);

            for (int i = 0; i < PLANKS; i++)
            {
                Transform plank = Visual($"Plank_{i}", seal, timber, new Vector3(-2f + i, 1.8f, -0.2f), new Vector3(0.45f, 4.6f, 0.12f));
                plank.localRotation = Quaternion.Euler(Range(6f, 16f), 0f, Range(-14f, 14f));
            }

            for (int i = 0; i < RUBBLE; i++)
            {
                float size = Range(0.8f, 1.9f);
                Transform rock = Visual($"Rubble_{i}", seal, stone,
                    new Vector3(Range(-3.6f, 3.6f), Range(-0.3f, 1.3f), Range(-1.8f, -0.4f)),
                    new Vector3(size, size * Range(0.6f, 1f), size * Range(0.7f, 1.1f)));
                rock.localRotation = Quaternion.Euler(Range(0f, 40f), Range(0f, 360f), Range(0f, 40f));
            }
        }

        private static Transform Visual(string name, Transform parent, Material material, Vector3 localPosition, Vector3 size)
        {
            Transform box = Area1BlockoutBuilder.Box(name, parent, material, Vector3.zero, size);
            box.localPosition = localPosition;
            box.gameObject.isStatic = false; // the seal goes away at runtime
            Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }
    }
}
