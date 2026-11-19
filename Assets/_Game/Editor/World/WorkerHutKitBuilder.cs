using System.Collections.Generic;
using System.IO;
using Game.Core;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Assembles the art-pass worker hut shells from Hivemind kit pieces and saves them as prefabs.
    /// Local +Z is the front doorway, −Z the rear opening. The origin is the floor centre.
    /// Rerunning replaces the prefabs' content, so scene instances follow.
    /// </summary>
    internal static class WorkerHutKitBuilder
    {
        private const string TAG = "[WorkerHutKit]";
        internal const string HOME_PATH = "Assets/_Game/Prefabs/World/Huts/WorkerHut_Home.prefab";
        internal const string LONG_PATH = "Assets/_Game/Prefabs/World/Huts/WorkerHut_Long.prefab";
        private const string KIT_ROOT = "Assets/Hivemind";
        private const string MISSING_PREFIX = "Missing_";
        /// <summary>Child holding the hut's (inactive by default) restricted-area volume.</summary>
        internal const string RESTRICTED_AREA = "RestrictedArea";

        // Wall centre lines: the kit's wall modules are 4 m and 3 m long and 2.5 m high.
        internal const float HALF_WIDTH = 1.5f;
        // Half the length along the door axis: 1.5 m for the home hut, 2 m for the long one. Set per build.
        private static float _halfLength = 1.5f;
        internal const float EAVE = 2.5f;
        // The door module's opening, as placed: off-centre, so one front corner stays a full wall.
        internal const float DOOR_MIN_X = 0f;
        internal const float DOOR_MAX_X = 1.05f;
        internal const float DOOR_HEIGHT = 2.14f;
        private const float DOOR_MODULE_STRETCH = 1.1f; // lifts the 1.95 m opening clear of the player's head
        private const float WALL = 0.3f;
        // Wall centre line to the kit modules' inner (visual) face: the room is 2.56 m across in the 3 m hut.
        private const float AREA_INSET = 0.22f;
        private const float PITCH = 28f;
        private const float OVERHANG = 0.3f;
        private const float WINDOW_MIN_X = -0.45f;
        private const float WINDOW_MAX_X = 0.4f;
        private const float WINDOW_SILL = 1.05f;
        private const float WINDOW_TOP = 1.95f;

        /// <summary>Half the home hut's length: the dressing tool places its props in this frame.</summary>
        internal const float HOME_HALF_LENGTH = 1.5f;

        private static Dictionary<string, string> _kit;
        private static int _missing;

        /// <summary>Kit pieces not found since the last <see cref="ResetKit"/>.</summary>
        internal static int MissingCount => _missing;

        /// <summary>Drops the name cache and the missing count; call at the start of a command.</summary>
        internal static void ResetKit()
        {
            _kit = null;
            _missing = 0;
        }

        [MenuItem("Tools/World/Build Worker Hut Kit Prefabs")]
        private static void BuildMenu() => BuildAll();

        /// <summary>Builds both sizes: the 3 × 3 m home hut (the blockout footprint) and a 3 × 4 m long hut.</summary>
        internal static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                GameLog.Error(TAG, "Leave Play mode and close Prefab Mode first; nothing changed.");
                return;
            }
            Build(HOME_PATH, HOME_HALF_LENGTH, "SM_wall3mB_a1");
            Build(LONG_PATH, 2f, "SM_wall4mB_a1");
        }

        internal static GameObject Build(string prefabPath, float halfLength, string sideWall)
        {
            ResetKit();
            _halfLength = halfLength;
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var rng = new System.Random(7);
                BuildWalls(root.transform, rng, sideWall);
                BuildRoof(root.transform, rng);
                BuildColliders(root.transform);
                BuildRestrictedArea(root.transform);
                if (_missing > 0)
                {
                    // Saving would replace a good hut with one that has a hole in it, and scene instances follow.
                    GameLog.Error(TAG, $"{_missing} kit piece(s) missing; {prefabPath} left as it was.");
                    return null;
                }
                string folder = prefabPath.Substring(0, prefabPath.LastIndexOf('/'));
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
                if (saved) GameLog.Info(TAG, $"Saved {prefabPath}");
                else GameLog.Error(TAG, $"Could not save {prefabPath}");
                return saved ? prefab : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildWalls(Transform root, System.Random rng, string sideWall)
        {
            Transform walls = Group("Walls", root);

            // Wall modules start at their pivot and run along local +X.
            Module(sideWall, walls, new Vector3(HALF_WIDTH, 0, _halfLength), 90f, "SideWall");
            Module(sideWall, walls, new Vector3(-HALF_WIDTH, 0, -_halfLength), -90f, "SideWall");

            // Rear: a square opening with a plank shutter swung open outside.
            Module("SM_wallWindow3m_a1", walls, new Vector3(-HALF_WIDTH, 0, -_halfLength), 0f, "RearWall");
            Transform shutter = Piece("SM_AssembledPlanksC_a1", walls,
                new Vector3(WINDOW_MAX_X + 0.06f, (WINDOW_SILL + WINDOW_TOP) * 0.5f, -_halfLength - 0.55f));
            shutter.localRotation = Quaternion.Euler(0, 14f, 90f);
            shutter.localScale = new Vector3(0.45f, 0.3f, 0.4f);
            Name(shutter, "Shutter");

            // Front: the door module, then a narrow barn panel to close the last metre.
            Transform door = Module("SM_wallDoor2m_a1", walls, new Vector3(HALF_WIDTH, 0, _halfLength), 180f, "DoorWall");
            door.localScale = new Vector3(1f, DOOR_MODULE_STRETCH, 1f);
            Transform filler = Module("SM_barnWallB_1m_a1", walls, new Vector3(HALF_WIDTH - 2f, 0, _halfLength), 180f, "FrontFiller");
            filler.localScale = new Vector3(1f / 1.19f, EAVE / 2f, 1f);

            // Gables: rows of horizontal boards, shorter toward the ridge.
            float tan = Mathf.Tan(PITCH * Mathf.Deg2Rad);
            float ridge = EAVE + HALF_WIDTH * tan;
            foreach (float end in new[] { -1f, 1f })
                for (int row = 0; row < 3; row++)
                {
                    float y = EAVE + row * 0.27f;
                    float half = (ridge - (y + 0.05f)) / tan; // near the lower edge of the board, so the eave corners close
                    if (half < 0.2f) break;
                    Transform board = Piece("SM_Damaged_Plank_B_0" + (row % 2 == 0 ? 3 : 2), walls, Vector3.zero);
                    float length = MeshLength(board);
                    board.localScale = new Vector3(half * 2f / length, 1f, 1f);
                    // The board's mesh runs from −length to 0 on X and lies flat; stand it on edge.
                    board.localRotation = Quaternion.Euler(end > 0 ? -90f : 90f, 0, (float)(rng.NextDouble() - 0.5) * 3f);
                    board.localPosition = new Vector3(half, end > 0 ? y : y + 0.29f, end * (_halfLength + 0.12f + (row % 2) * 0.02f));
                    Name(board, "GableBoard");
                }
        }

        private static Transform Module(string kitName, Transform parent, Vector3 at, float yaw, string name)
        {
            Transform module = Piece(kitName, parent, at);
            module.localRotation = Quaternion.Euler(0, yaw, 0);
            Name(module, name);
            return module;
        }

        private static void BuildRoof(Transform root, System.Random rng)
        {
            Transform roof = Group("Roof", root);
            // Nobody walks on the roof: keep it out of the NavMesh, like the graybox huts.
            var modifier = roof.gameObject.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = 1;

            float rad = PITCH * Mathf.Deg2Rad, tan = Mathf.Tan(rad);
            float ridge = EAVE + HALF_WIDTH * tan + 0.12f;
            float slope = (HALF_WIDTH + OVERHANG) / Mathf.Cos(rad);
            float length = (_halfLength + OVERHANG) * 2f;
            foreach (float side in new[] { -1f, 1f })
            {
                Transform plane = Group("Slope", roof);
                plane.localPosition = new Vector3(0, ridge, 0);
                plane.localRotation = Quaternion.Euler(0, 0, -side * PITCH);
                for (int n = 0; n < 2; n++)
                {
                    Transform panel = Piece("SM_AssembledPlanksA_a1", plane,
                        new Vector3(side * slope * 0.5f, 0, (n - 0.5f) * length * 0.5f));
                    panel.localRotation = Quaternion.Euler(0, (n == 0) == (side > 0) ? 0f : 180f, 0);
                    panel.localScale = new Vector3(slope / 2.14f, 0.6f, length * 0.5f / 2.4f * 1.02f);
                    Name(panel, "RoofBoards");
                }
                for (int n = 0; n < 5; n++)
                {
                    float along = (0.25f + (float)rng.NextDouble() * 0.6f) * slope;
                    float z = -length * 0.5f + (n + 0.3f + (float)rng.NextDouble() * 0.4f) * length / 5f;
                    Transform stone = Piece(n % 2 == 0 ? "SM_RockStone_01a" : "SM_RockStone_01b", plane,
                        new Vector3(side * along, 0.14f, z));
                    stone.localRotation = Quaternion.Euler(rng.Next(-10, 10), rng.Next(360), 0);
                    stone.localScale = Vector3.one * (0.3f + (float)rng.NextDouble() * 0.14f);
                    Name(stone, "RoofStone");
                }
            }
            // Two caps, one from each end, stretched so they meet in the middle whatever the hut's length.
            const float CAP_MESH_FRONT = 0.5f, CAP_MESH_LENGTH = 3.48f;
            float capScale = (length * 0.5f + 0.15f) / CAP_MESH_LENGTH;
            foreach (float end in new[] { -1f, 1f })
            {
                Transform cap = Piece("SM_Plank_Rope_300_A", roof,
                    new Vector3(0, ridge + 0.02f, -end * (length * 0.5f - CAP_MESH_FRONT * capScale)));
                cap.localRotation = Quaternion.Euler(0, end > 0 ? 0f : 180f, 0);
                cap.localScale = new Vector3(1.6f, 1f, capScale);
                Name(cap, "RidgeCap");
            }
        }

        private static void BuildColliders(Transform root)
        {
            Transform colliders = Group("Colliders", root);
            float mid = EAVE * 0.5f;
            foreach (float side in new[] { -1f, 1f })
                Box(colliders, "Side", new Vector3(side * HALF_WIDTH, mid, 0), new Vector3(WALL, EAVE, _halfLength * 2f + WALL));
            Span(colliders, "Front", _halfLength, -HALF_WIDTH, DOOR_MIN_X, 0, EAVE);
            Span(colliders, "Front", _halfLength, DOOR_MAX_X, HALF_WIDTH, 0, EAVE);
            Span(colliders, "Lintel", _halfLength, DOOR_MIN_X, DOOR_MAX_X, DOOR_HEIGHT, EAVE);
            Span(colliders, "Rear", -_halfLength, -HALF_WIDTH, WINDOW_MIN_X, 0, EAVE);
            Span(colliders, "Rear", -_halfLength, WINDOW_MAX_X, HALF_WIDTH, 0, EAVE);
            Span(colliders, "WindowSill", -_halfLength, WINDOW_MIN_X, WINDOW_MAX_X, 0, WINDOW_SILL);
            Span(colliders, "WindowHead", -_halfLength, WINDOW_MIN_X, WINDOW_MAX_X, WINDOW_TOP, EAVE);

            float rad = PITCH * Mathf.Deg2Rad;
            float slope = (HALF_WIDTH + OVERHANG) / Mathf.Cos(rad);
            foreach (float side in new[] { -1f, 1f })
            {
                Transform plane = Group("RoofPlane", colliders);
                plane.localPosition = new Vector3(0, EAVE + HALF_WIDTH * Mathf.Tan(rad) + 0.12f, 0);
                plane.localRotation = Quaternion.Euler(0, 0, -side * PITCH);
                Box(plane, "Roof", new Vector3(side * slope * 0.5f, 0, 0), new Vector3(slope, 0.16f, (_halfLength + OVERHANG) * 2f));
            }
        }

        /// <summary>
        /// The room between the wall faces, floor to eave, as an <b>inactive</b> <see cref="Game.World.RestrictedArea"/>
        /// with no owners: an owned hut's scene instance switches it on and lists its owners (two overrides). Inactive,
        /// its <c>Awake</c> never runs, so unowned huts log nothing. The doorway itself stays outside the box.
        /// </summary>
        private static void BuildRestrictedArea(Transform root)
        {
            Transform area = Group(RESTRICTED_AREA, root);
            area.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            var box = area.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, EAVE * 0.5f, 0f);
            box.size = new Vector3((HALF_WIDTH - AREA_INSET) * 2f, EAVE, (_halfLength - AREA_INSET) * 2f);
            area.gameObject.AddComponent<Game.World.RestrictedArea>();
            area.gameObject.SetActive(false);
        }

        /// <summary>A wall-thick box on an end wall, between two X positions and two heights.</summary>
        private static void Span(Transform parent, string name, float z, float fromX, float toX, float fromY, float toY)
        {
            Box(parent, name, new Vector3((fromX + toX) * 0.5f, (fromY + toY) * 0.5f, z), new Vector3(toX - fromX, toY - fromY, WALL));
        }

        private static void Box(Transform parent, string name, Vector3 at, Vector3 size)
        {
            Transform box = Group(name, parent);
            box.localPosition = at;
            box.gameObject.AddComponent<BoxCollider>().size = size;
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Instantiates a kit prefab by name as a nested prefab, with its own colliders switched off.</summary>
        internal static Transform Piece(string kitName, Transform parent, Vector3 localPosition)
        {
            string path = KitPath(kitName);
            var asset = path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                _missing++;
                GameLog.Warn(TAG, $"Kit piece '{kitName}' not found under {KIT_ROOT}");
                return Group(MISSING_PREFIX + kitName, parent);
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = localPosition;
            // The shell has its own box colliders; the pieces' mesh colliders would only snag the player.
            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            return go.transform;
        }

        /// <summary>Renames a piece, unless it is the placeholder of a missing kit prefab: that name must stay visible.</summary>
        internal static void Name(Transform piece, string name)
        {
            if (!piece.name.StartsWith(MISSING_PREFIX)) piece.name = name;
        }

        private static string KitPath(string kitName)
        {
            if (_kit == null)
            {
                _kit = new Dictionary<string, string>();
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { KIT_ROOT });
                var paths = new List<string>(guids.Length);
                foreach (string guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                paths.Sort(System.StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    // Several packs can ship the same name: the first path in ordinal order wins, on every machine.
                    if (!_kit.ContainsKey(name)) _kit[name] = path;
                }
            }
            return _kit.TryGetValue(kitName, out string found) ? found : null;
        }

        /// <summary>Unscaled length of a piece's first mesh (LOD0) along its local X.</summary>
        private static float MeshLength(Transform piece)
        {
            var filter = piece.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return 1f;
            return Mathf.Max(0.01f, Mathf.Abs(filter.sharedMesh.bounds.size.x * filter.transform.lossyScale.x));
        }
    }
}
