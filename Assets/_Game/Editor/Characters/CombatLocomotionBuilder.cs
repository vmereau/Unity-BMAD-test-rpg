using System;
using System.Collections.Generic;
using Game.Animations;
using Game.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor.Characters
{
    /// <summary>
    /// Builds the combat locomotion of Humanoid_Template (spec-sword-shield-pack-locomotion). The Base Layer's
    /// <c>LockOn Locomotion</c> plays a 1D blend tree on <c>CombatLocomotion</c>: 0 = the free 2D tree (unchanged), 1 = a
    /// combat 2D tree with the same positions and time scales whose clips are <c>.anim</c> copies of the free tree's clips.
    /// The copies are distinct override keys: a weapon override maps them to its combat stance (sword → the Mixamo
    /// sword-and-shield pack) without touching the free tree, which also plays while the weapon is sheathed. Every other
    /// override leaves them, so its combat locomotion is today's. The drivers step the float from their combat flag.
    /// Idempotent: copies are overwritten in place (GUIDs kept) and the combat tree rebuilt; the free tree is kept. The
    /// copies are snapshots: rebuild after a free clip is added, renamed or re-imported (a removed clip's copy stays).
    /// </summary>
    public static class CombatLocomotionBuilder
    {
        private const string TAG = "[CombatLocomotionBuilder]";
        public const string FOLDER = "Assets/_Game/Art/Characters/Humanoids/Animations/CombatLocomotion";
        public const string BASE_STATE = "LockOn Locomotion";
        public const string WRAPPER_TREE = "Locomotion (free / combat)";
        public const string COMBAT_TREE = "Combat Locomotion";
        public const string COPY_PREFIX = "Combat ";

        [MenuItem("Tools/Characters/Build Combat Locomotion")]
        private static void BuildFromMenu()
        {
            try { GameLog.Info(TAG, Build()); }
            catch (Exception exception) { GameLog.Error(TAG, exception.ToString()); }
        }

        /// <summary>Copies the clips, rebuilds the combat tree and the wrapper. Returns a summary; throws on a malformed controller.</summary>
        public static string Build()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HandPoseBuilder.CONTROLLER_PATH);
            if (controller == null) throw new InvalidOperationException($"CombatLocomotionBuilder: no controller at {HandPoseBuilder.CONTROLLER_PATH}");
            var state = FindBaseState(controller);
            var (free, oldCombat, oldWrapper) = SplitMotion(state);
            ValidateFreeTree(free); // before any asset is written: a throw leaves nothing behind

            EnsureFolder(FOLDER);
            EnsureFloatParam(controller, HumanoidAnimationBridge.COMBAT_LOCOMOTION_PARAM);

            var combat = new BlendTree
            {
                name = COMBAT_TREE,
                blendType = free.blendType,
                blendParameter = free.blendParameter,
                blendParameterY = free.blendParameterY,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(combat, controller);
            var children = new List<ChildMotion>();
            foreach (var child in free.children)
            {
                var copy = child;
                copy.motion = WriteCopy((AnimationClip)child.motion);
                children.Add(copy);
            }
            combat.children = children.ToArray();

            var wrapper = new BlendTree
            {
                name = WRAPPER_TREE,
                blendType = BlendTreeType.Simple1D,
                blendParameter = HumanoidAnimationBridge.COMBAT_LOCOMOTION_PARAM,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(wrapper, controller);
            wrapper.children = new[]
            {
                new ChildMotion { motion = free, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = combat, threshold = 1f, timeScale = 1f },
            };
            state.motion = wrapper;

            // Only after the state points at the new wrapper: the old sub-assets are no longer referenced.
            if (oldWrapper != null) DestroySubAsset(oldWrapper);
            if (oldCombat != null) DestroySubAsset(oldCombat);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return $"built {BASE_STATE}: free tree + {COMBAT_TREE} ({children.Count} clip copies in {FOLDER})";
        }

        // One copy per clip name: every child must be a clip, with a unique name that is a valid file name.
        private static void ValidateFreeTree(BlendTree free)
        {
            var names = new HashSet<string>();
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            foreach (var child in free.children)
            {
                if (child.motion is not AnimationClip clip)
                    throw new InvalidOperationException($"CombatLocomotionBuilder: free tree child '{child.motion?.name}' is not a clip");
                if (!names.Add(clip.name))
                    throw new InvalidOperationException($"CombatLocomotionBuilder: two free tree clips are named '{clip.name}'");
                if (clip.name.IndexOfAny(invalid) >= 0)
                    throw new InvalidOperationException($"CombatLocomotionBuilder: clip name '{clip.name}' is not a valid file name");
            }
        }

        private static AnimatorState FindBaseState(AnimatorController controller)
        {
            foreach (var child in controller.layers[0].stateMachine.states)
                if (child.state.name == BASE_STATE) return child.state;
            throw new InvalidOperationException($"CombatLocomotionBuilder: Base Layer has no '{BASE_STATE}' state");
        }

        // First build: the state plays the free 2D tree. Rebuild: it plays the wrapper (free @0, combat @1).
        private static (BlendTree free, BlendTree oldCombat, BlendTree oldWrapper) SplitMotion(AnimatorState state)
        {
            if (state.motion is not BlendTree tree)
                throw new InvalidOperationException($"CombatLocomotionBuilder: '{BASE_STATE}' has no blend tree");
            if (tree.name != WRAPPER_TREE) return (tree, null, null);
            if (tree.children.Length != 2 || tree.children[0].motion is not BlendTree free || tree.children[1].motion is not BlendTree combat)
                throw new InvalidOperationException($"CombatLocomotionBuilder: '{WRAPPER_TREE}' is not free / combat");
            return (free, combat, tree);
        }

        private static AnimationClip WriteCopy(AnimationClip source)
        {
            string path = $"{FOLDER}/{COPY_PREFIX}{source.name}.anim";
            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = COPY_PREFIX + source.name;
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(copy, path);
                return copy;
            }
            EditorUtility.CopySerialized(copy, existing);
            existing.name = COPY_PREFIX + source.name;
            UnityEngine.Object.DestroyImmediate(copy);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void EnsureFloatParam(AnimatorController controller, string name)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name != name) continue;
                if (p.type != AnimatorControllerParameterType.Float)
                    throw new InvalidOperationException($"CombatLocomotionBuilder: parameter '{name}' exists but is not a float");
                return;
            }
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        private static void DestroySubAsset(UnityEngine.Object asset)
        {
            AssetDatabase.RemoveObjectFromAsset(asset);
            UnityEngine.Object.DestroyImmediate(asset, true);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
