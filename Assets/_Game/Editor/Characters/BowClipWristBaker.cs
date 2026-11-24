using System;
using System.Collections.Generic;
using Game.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor.Characters
{
    /// <summary>
    /// Bakes the three bow aim clips (Mixamo FBX) into <c>.anim</c> copies with a constant left-wrist offset
    /// (spec-bow-hand-grip). The bow riser lies across the left palm (<c>Archetype_Bow.drawnPose</c>), which turns the bow
    /// ~80° from where the clips' wrist held it; the offsets turn it back (fitted against the previous in-hand orientation
    /// on Aim Overdraw: 11° residual, 3° of it on the bow's long axis). Wrist muscles don't move the hand bone, so the
    /// arrow line calibration of <c>BowAimCorrection</c> is unchanged. One offset set for the three clips: the wrist
    /// doesn't turn at the Draw arrow → Aim → Fire arrow transitions. Idempotent: rewrites the <c>.anim</c> in place (GUIDs
    /// kept) and points the UpperBody bow states at them.
    /// </summary>
    public static class BowClipWristBaker
    {
        private const string TAG = "[BowClipWristBaker]";
        private const string FOLDER = "Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Bow/";
        private const string LAYER = "UpperBody";

        /// <summary>Source FBX, baked clip, UpperBody state.</summary>
        public static readonly (string source, string baked, string state)[] Clips =
        {
            (FOLDER + "Standing Draw Arrow.fbx", FOLDER + "Bow Draw Arrow.anim", "BowDrawArrow_State"),
            (FOLDER + "Standing Aim Overdraw.fbx", FOLDER + "Bow Aim Overdraw.anim", "BowAim_State"),
            (FOLDER + "Standing Fire Arrow.fbx", FOLDER + "Bow Fire Arrow.anim", "BowFire_State"),
        };

        /// <summary>Constant offsets added to the left wrist / forearm-twist muscle curves (humanoid muscle units).</summary>
        public static readonly (string muscle, float offset)[] WristOffsets =
        {
            ("Left Hand Down-Up", 0.819f),
            ("Left Hand In-Out", 0.594f),
            ("Left Forearm Twist In-Out", 0.488f),
        };

        [MenuItem("Tools/Characters/Bake Bow Wrist Clips")]
        private static void BakeFromMenu()
        {
            try { GameLog.Info(TAG, BakeAll()); }
            catch (Exception exception) { GameLog.Error(TAG, exception.ToString()); }
        }

        /// <summary>Bakes the clips and rewires the states. Returns a summary; throws on a missing clip, curve or state.</summary>
        public static string BakeAll()
        {
            // Validate before writing anything.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HandPoseBuilder.CONTROLLER_PATH);
            if (controller == null) throw new InvalidOperationException($"BowClipWristBaker: no controller at {HandPoseBuilder.CONTROLLER_PATH}");
            var states = new AnimatorState[Clips.Length];
            var sources = new AnimationClip[Clips.Length];
            for (int i = 0; i < Clips.Length; i++)
            {
                states[i] = FindState(controller, Clips[i].state);
                sources[i] = LoadSource(Clips[i].source);
                if (AnimationUtility.GetAnimationEvents(sources[i]).Length > 0) // the bakes carry no events (nock = SMB)
                    GameLog.Warn(TAG, $"{Clips[i].source} has events; the baked clip drops them");
                foreach (var (muscle, _) in WristOffsets)
                    if (AnimationUtility.GetEditorCurve(sources[i], MuscleBinding(muscle)) == null)
                        throw new InvalidOperationException($"BowClipWristBaker: {Clips[i].source} has no '{muscle}' curve");
            }

            var report = new List<string>();
            for (int i = 0; i < Clips.Length; i++)
            {
                var baked = Bake(sources[i], Clips[i].baked, out int clamped);
                states[i].motion = baked;
                report.Add($"{baked.name} ({clamped} keys clamped)");
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return "baked " + string.Join(", ", report);
        }

        /// <summary>
        /// Wrist value after the offset: clamped to the muscle range, or to the source's own value where the source already
        /// goes past it (the offset never pushes a muscle further out of range than the clip does).
        /// </summary>
        public static float OffsetValue(float source, float offset)
        {
            float limit = Mathf.Max(1f, Mathf.Abs(source));
            return Mathf.Clamp(source + offset, -limit, limit);
        }

        private static AnimationClip Bake(AnimationClip source, string path, out int clamped)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = source.frameRate;
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());

            var offsets = new Dictionary<string, float>();
            foreach (var (muscle, offset) in WristOffsets) offsets[muscle] = offset;
            clamped = 0;
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                if (binding.type == typeof(Animator) && string.IsNullOrEmpty(binding.path) &&
                    offsets.TryGetValue(binding.propertyName, out float offset))
                {
                    var keys = curve.keys;
                    for (int k = 0; k < keys.Length; k++)
                    {
                        float value = OffsetValue(keys[k].value, offset);
                        if (!Mathf.Approximately(value, keys[k].value + offset))
                        {
                            clamped++;
                            keys[k].inTangent = keys[k].outTangent = 0f; // a clamped key holds
                        }
                        keys[k].value = value;
                    }
                    curve.keys = keys;
                }
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(source));
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static EditorCurveBinding MuscleBinding(string muscle) =>
            EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle);

        private static AnimationClip LoadSource(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) return clip;
            throw new InvalidOperationException($"BowClipWristBaker: no clip in {path}");
        }

        private static AnimatorState FindState(AnimatorController controller, string stateName)
        {
            foreach (var layer in controller.layers)
            {
                if (layer.name != LAYER) continue;
                foreach (var child in layer.stateMachine.states)
                    if (child.state.name == stateName) return child.state;
            }
            throw new InvalidOperationException($"BowClipWristBaker: no state {stateName} in layer {LAYER}");
        }
    }
}
