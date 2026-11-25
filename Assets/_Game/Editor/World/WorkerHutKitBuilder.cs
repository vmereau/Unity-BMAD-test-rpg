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
    /// Builds the art-pass worker hut prefabs round the board-built shell models (Blender,
    /// <c>Art/Environment/Camp/models/worker hut</c>): model, weight poles, colliders and restricted area.
    /// Local +Z is the front doorway, −Z the rear opening. The origin is the floor centre.
    /// Rerunning replaces the prefabs' content, so scene instances follow.
    /// </summary>
    internal static class WorkerHutKitBuilder
    {
        private const string TAG = "[WorkerHutKit]";
        internal const string HOME_PATH = "Assets/_Game/Prefabs/World/Huts/WorkerHut_Home.prefab";
        internal const string LONG_PATH = "Assets/_Game/Prefabs/World/Huts/WorkerHut_Long.prefab";
        private const string PREFABS = "Assets/_Game/Prefabs/World/Huts/";
        private const string KIT_ROOT = "Assets/Hivemind";
        private const string MISSING_PREFIX = "Missing_";
        /// <summary>Child holding the hut's (inactive by default) restricted-area volume.</summary>
        internal const string RESTRICTED_AREA = "RestrictedArea";

        // Wall centre lines. The shell model is built to the same numbers (Source~/build_worker_hut.py).
        internal const float HALF_WIDTH = 1.5f;
        // Half the length along the door axis: 1.5 m for the home hut, 2 m for the long one. Set per build.
        private static float _halfLength = 1.5f;
        internal const float EAVE = 2.5f;
        // The doorway: off-centre, so one front corner stays a full wall. These are the default shell's; a mirrored
        // variant has the same doorway on the other half of the front wall (Variant.DoorMinX / DoorMaxX).
        internal const float DOOR_MIN_X = 0f;
        internal const float DOOR_MAX_X = 1.05f;
        private static float _doorMinX = DOOR_MIN_X, _doorMaxX = DOOR_MAX_X;
        internal const float DOOR_HEIGHT = 2.14f;
        // Wall colliders: 1.35 to 1.55 m from the middle (the boards' inner face is at 1.28, the posts end at 1.455).
        private const float WALL = 0.2f;
        private const float WALL_CENTRE = -0.05f; // collider centre, inside the wall centre line by this much
        // Wall centre line to the boards' inner face: the room is 2.56 m across in the 3 m hut.
        private const float AREA_INSET = 0.22f;
        private const float PITCH = 28f;
        internal const float OVERHANG = 0.3f;
        // Packed rows: with this eave the roof ends no further out than the graybox roof did (1.70 m from the middle).
        internal const float TIGHT_OVERHANG = 0.09f;
        private static float _overhang = OVERHANG;
        private const float WINDOW_MIN_X = -0.45f;
        private const float WINDOW_MAX_X = 0.4f;
        private const float WINDOW_SILL = 1.05f;
        private const float WINDOW_TOP = 1.95f;

        // Weight poles: two per slope, parallel to the ridge, lashed round the barge boards at both ends.
        private const string MODELS = "Assets/_Game/Art/Environment/Camp/models/";
        internal const string ROOF_POLE = MODELS + "roof pole/RoofPole.fbx";
        internal const string ROOF_LASHING = MODELS + "roof pole/RoofLashing.fbx";
        private const float POLE_MODEL_LENGTH = 3.7f;
        private const float BOARD_TOP = -0.05f; // the shell's roof boards, below the slope group's plane
        private const float POLE_RADIUS = 0.035f;
        private static readonly float[] PoleRows = { 0.3f, 0.78f }; // fraction of the slope from the ridge

        // The shell: one model per size, objects `Walls` and `Roof`, on M_Camp_HutShell / M_Camp_HutShellDark
        // (assigned by the importer's material remap).
        /// <summary>One shell of the family: a model exported by <c>build_worker_hut.py</c> and the prefab built round it.</summary>
        internal readonly struct Variant
        {
            internal readonly string Name;
            internal readonly float HalfLength, DoorMinX, DoorMaxX, Overhang;

            internal Variant(string name, float halfLength, bool mirrored, float overhang)
            {
                Name = name;
                HalfLength = halfLength;
                DoorMinX = mirrored ? -DOOR_MAX_X : DOOR_MIN_X;
                DoorMaxX = mirrored ? -DOOR_MIN_X : DOOR_MAX_X;
                Overhang = overhang;
            }

            internal string PrefabPath => PREFABS + Name + ".prefab";
            internal string ModelPath => MODELS + "worker hut/" + Name + ".fbx";
            internal bool Mirrored => DoorMinX < 0f;
            internal bool Tight => Overhang < OVERHANG;
            internal bool Long => HalfLength > HOME_HALF_LENGTH;
            /// <summary>Furthest reach of the roof from the middle, across the ridge (boards run up to 0.11 m long).</summary>
            internal float RoofHalfWidth => HALF_WIDTH + Overhang + 0.11f;
            /// <summary>Furthest reach of the roof from the middle, along the ridge (the ridge log sticks out 0.08 m).</summary>
            internal float RoofHalfLength => HalfLength + Overhang + 0.08f;
        }

        /// <summary>Every shell, in the order of the script's <c>VARIANTS</c>: change both together.</summary>
        internal static readonly Variant[] Variants =
        {
            new Variant("WorkerHut_Home", HOME_HALF_LENGTH, false, OVERHANG),
            new Variant("WorkerHut_Home_B", HOME_HALF_LENGTH, false, OVERHANG),
            new Variant("WorkerHut_Home_C", HOME_HALF_LENGTH, true, OVERHANG),
            new Variant("WorkerHut_Tight", HOME_HALF_LENGTH, false, TIGHT_OVERHANG),
            new Variant("WorkerHut_Tight_B", HOME_HALF_LENGTH, true, TIGHT_OVERHANG),
            new Variant("WorkerHut_Tight_C", HOME_HALF_LENGTH, false, TIGHT_OVERHANG),
            new Variant("WorkerHut_Tight_D", HOME_HALF_LENGTH, true, TIGHT_OVERHANG),
            new Variant("WorkerHut_Long", 2f, false, OVERHANG),
        };

        /// <summary>The shell a scene object is an instance of; false for anything else (a graybox hut).</summary>
        internal static bool TryGetVariant(GameObject instance, out Variant variant)
        {
            variant = default;
            if (instance == null || !PrefabUtility.IsAnyPrefabInstanceRoot(instance)) return false;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
            foreach (Variant candidate in Variants)
                if (candidate.PrefabPath == path)
                {
                    variant = candidate;
                    return true;
                }
            return false;
        }
        private const string MODEL_WALLS = "Walls";
        private const string MODEL_ROOF = "Roof";
        private const string SHELL_MATERIAL = "M_Camp_HutShell";
        private const string SHELL_DARK_MATERIAL = "M_Camp_HutShellDark";

        /// <summary>Half the home hut's length: the dressing tool places its props in this frame.</summary>
        internal const float HOME_HALF_LENGTH = 1.5f;

        private static Dictionary<string, string> _kit;
        private static int _missing;

        /// <summary>Kit pieces, camp models and shell checks that failed since the last <see cref="ResetKit"/>.</summary>
        internal static int MissingCount => _missing;

        /// <summary>Drops the name cache and the missing count; call at the start of a command.</summary>
        internal static void ResetKit()
        {
            _kit = null;
            _missing = 0;
        }

        [MenuItem("Tools/World/Build Worker Hut Kit Prefabs")]
        private static void BuildMenu() => BuildAll();

        /// <summary>Builds every shell of <see cref="Variants"/>: the 3 × 3 m huts (the blockout footprint) and a 3 × 4 m long hut.</summary>
        internal static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                GameLog.Error(TAG, "Leave Play mode and close Prefab Mode first; nothing changed.");
                return;
            }
            foreach (Variant variant in Variants) Build(variant);
        }

        internal static GameObject Build(Variant variant)
        {
            string prefabPath = variant.PrefabPath, modelPath = variant.ModelPath;
            ResetKit();
            _halfLength = variant.HalfLength;
            _doorMinX = variant.DoorMinX;
            _doorMaxX = variant.DoorMaxX;
            _overhang = variant.Overhang;
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var rng = new System.Random(7);
                BuildShell(root.transform, modelPath, rng);
                BuildColliders(root.transform);
                BuildRestrictedArea(root.transform);
                if (_missing > 0)
                {
                    // Saving would replace a good hut with one that has a hole in it, and scene instances follow.
                    GameLog.Error(TAG, $"{_missing} camp asset(s) missing or incomplete; {prefabPath} left as it was.");
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

        /// <summary>
        /// Takes the model apart into the prefab's own <c>Walls</c> and <c>Roof</c> objects (the meshes stay the
        /// model's), then lays the weight poles on the roof. A model without both objects, or with a material
        /// other than the two shell materials, counts as missing.
        /// </summary>
        private static void BuildShell(Transform root, string modelPath, System.Random rng)
        {
            Transform roof = Group("Roof", root);
            // Nobody walks on the roof: keep it out of the NavMesh, like the graybox huts.
            var modifier = roof.gameObject.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = 1;

            Transform model = Model(modelPath, root, Vector3.zero);
            if (model != null)
            {
                PrefabUtility.UnpackPrefabInstance(model.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Transform walls = model.Find(MODEL_WALLS), boards = model.Find(MODEL_ROOF);
                if (walls == null || boards == null)
                {
                    _missing++;
                    GameLog.Error(TAG, $"'{modelPath}' needs the objects '{MODEL_WALLS}' and '{MODEL_ROOF}'");
                }
                else
                {
                    walls.SetParent(root, true);
                    walls.SetAsFirstSibling();
                    boards.SetParent(roof, true);
                    boards.name = "RoofBoards";
                    if (!OnCampMaterials(walls) || !OnCampMaterials(boards))
                    {
                        _missing++;
                        GameLog.Error(TAG, $"'{modelPath}': remap its materials to {SHELL_MATERIAL} and {SHELL_DARK_MATERIAL} (model importer, Materials tab)");
                    }
                }
                Object.DestroyImmediate(model.gameObject);
            }

            float rad = PITCH * Mathf.Deg2Rad, tan = Mathf.Tan(rad);
            float ridge = EAVE + HALF_WIDTH * tan + 0.12f;
            float slope = (HALF_WIDTH + _overhang) / Mathf.Cos(rad);
            float length = (_halfLength + _overhang) * 2f;
            foreach (float side in new[] { -1f, 1f })
            {
                Transform plane = Group("Slope", roof);
                plane.localPosition = new Vector3(0, ridge, 0);
                plane.localRotation = Quaternion.Euler(0, 0, -side * PITCH);
                BuildWeightPoles(plane, side, slope, length, rng);
            }
        }

        /// <summary>True when the object has a renderer and every material on it is one of the two shell materials.</summary>
        private static bool OnCampMaterials(Transform part)
        {
            if (!part.TryGetComponent(out Renderer renderer) || renderer.sharedMaterials.Length == 0) return false;
            foreach (Material material in renderer.sharedMaterials)
                if (material == null || (material.name != SHELL_MATERIAL && material.name != SHELL_DARK_MATERIAL)) return false;
            return true;
        }

        /// <summary>
        /// Two bark poles lying on one slope (plane frame: X down the slope, Y off the boards, Z along the ridge), each
        /// tied round the barge boards at both ends. The lashing's rope end hangs straight down outside them.
        /// </summary>
        private static void BuildWeightPoles(Transform plane, float side, float slope, float length, System.Random rng)
        {
            float poleLength = length + 0.1f;
            foreach (float row in PoleRows)
            {
                float across = side * row * slope;
                Transform pole = Model(ROOF_POLE, plane, new Vector3(across, BOARD_TOP + POLE_RADIUS, 0f));
                if (pole == null) continue;
                // The mesh runs along its X; flip some poles end for end so the bends differ.
                pole.localRotation = Quaternion.Euler(0f, rng.Next(2) == 0 ? 90f : -90f, 0f);
                pole.localScale = new Vector3(poleLength / POLE_MODEL_LENGTH, 1f, 1f);
                Name(pole, "WeightPole");
                foreach (float end in new[] { -1f, 1f })
                {
                    Transform lashing = Model(ROOF_LASHING, plane,
                        new Vector3(across, BOARD_TOP + POLE_RADIUS, end * (_halfLength + _overhang + 0.012f)));
                    if (lashing == null) continue;
                    // Its coil wraps the mesh's X axis and the tail hangs down −Y: lay X along the ridge, then undo
                    // the slope's tilt so the tail hangs plumb.
                    lashing.localRotation = Quaternion.Euler(0f, 0f, side * PITCH) * Quaternion.Euler(0f, 90f, 0f);
                    Name(lashing, "PoleLashing");
                }
            }
        }

        /// <summary>A camp model (FBX) as a nested prefab; counted as missing when absent, like a kit piece.</summary>
        private static Transform Model(string path, Transform parent, Vector3 localPosition)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                _missing++;
                GameLog.Error(TAG, $"Camp model '{path}' not found");
                Group(MISSING_PREFIX + Path.GetFileNameWithoutExtension(path), parent);
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static void BuildColliders(Transform root)
        {
            Transform colliders = Group("Colliders", root);
            float mid = EAVE * 0.5f;
            foreach (float side in new[] { -1f, 1f })
                Box(colliders, "Side", new Vector3(side * (HALF_WIDTH + WALL_CENTRE), mid, 0),
                    new Vector3(WALL, EAVE, (_halfLength + WALL_CENTRE) * 2f + WALL));
            float front = _halfLength + WALL_CENTRE, rear = -front;
            Span(colliders, "Front", front, -HALF_WIDTH, _doorMinX, 0, EAVE);
            Span(colliders, "Front", front, _doorMaxX, HALF_WIDTH, 0, EAVE);
            Span(colliders, "Lintel", front, _doorMinX, _doorMaxX, DOOR_HEIGHT, EAVE);
            Span(colliders, "Rear", rear, -HALF_WIDTH, WINDOW_MIN_X, 0, EAVE);
            Span(colliders, "Rear", rear, WINDOW_MAX_X, HALF_WIDTH, 0, EAVE);
            Span(colliders, "WindowSill", rear, WINDOW_MIN_X, WINDOW_MAX_X, 0, WINDOW_SILL);
            Span(colliders, "WindowHead", rear, WINDOW_MIN_X, WINDOW_MAX_X, WINDOW_TOP, EAVE);

            float rad = PITCH * Mathf.Deg2Rad;
            float slope = (HALF_WIDTH + _overhang) / Mathf.Cos(rad);
            foreach (float side in new[] { -1f, 1f })
            {
                Transform plane = Group("RoofPlane", colliders);
                plane.localPosition = new Vector3(0, EAVE + HALF_WIDTH * Mathf.Tan(rad) + 0.12f, 0);
                plane.localRotation = Quaternion.Euler(0, 0, -side * PITCH);
                Box(plane, "Roof", new Vector3(side * slope * 0.5f, 0, 0), new Vector3(slope, 0.16f, (_halfLength + _overhang) * 2f));
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
    }
}
