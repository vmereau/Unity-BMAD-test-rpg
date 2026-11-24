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
    /// Builds the finger-pose layers of Humanoid_Template (spec-hand-pose-layer): one 1-frame finger-only clip per
    /// <see cref="HandPose"/> and hand, a finger-only <see cref="AvatarMask"/> per hand, and the <c>LeftHand</c> /
    /// <c>RightHand</c> layers (Override, weight 1, appended after <c>UpperBody</c>) switched by the <c>LeftHandPose</c> /
    /// <c>RightHandPose</c> ints. Idempotent: clips and masks are overwritten in place (GUIDs kept), the layers rebuilt.
    /// Pose values live in <see cref="Poses"/> (humanoid muscle units, −1 = closed, +1 = open; tuned on the v2 hand).
    /// </summary>
    public static class HandPoseBuilder
    {
        private const string TAG = "[HandPoseBuilder]";
        public const string CONTROLLER_PATH = "Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller";
        public const string FOLDER = "Assets/_Game/Art/Characters/Humanoids/Animations/HandPoses";
        public const string LEFT_LAYER = "LeftHand";
        public const string RIGHT_LAYER = "RightHand";
        public const string LEFT_PARAM = "LeftHandPose";
        public const string RIGHT_PARAM = "RightHandPose";
        private const float TRANSITION_SECONDS = 0.12f;
        private const float CLIP_LENGTH = 1f / 30f;

        public static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };

        /// <summary>One finger: proximal / intermediate / distal <c>Stretched</c> muscles and <c>Spread</c>.</summary>
        public readonly struct Finger
        {
            public readonly float S1, S2, S3, Spread;
            public Finger(float s1, float s2, float s3, float spread) { S1 = s1; S2 = s2; S3 = s3; Spread = spread; }
        }

        /// <summary>Per pose: Thumb, Index, Middle, Ring, Little. Same values on both hands (humanoid muscles are mirrored).</summary>
        public static readonly Dictionary<HandPose, Finger[]> Poses = new Dictionary<HandPose, Finger[]>
        {
            // Spread < 0 closes the bind pose's wide finger spread (−1 still leaves ~3 cm between knuckle centres).
            [HandPose.Relaxed] = new[]
            {
                new Finger(0.1f, -0.2f, -0.1f, -0.4f),
                new Finger(0.3f, -0.1f, 0.1f, -0.7f),
                new Finger(0.2f, -0.2f, 0.1f, -0.35f),
                new Finger(0.1f, -0.3f, 0f, -0.7f),
                new Finger(0f, -0.4f, -0.1f, -0.7f),
            },
            [HandPose.Fist] = new[]
            {
                // PIP is at its limit (−1 ≈ 81°): more MCP curl than ~85° or DIP than ~73° sinks the fingers into the palm.
                new Finger(-0.5f, -0.8f, -0.5f, 0.5f),
                new Finger(-0.45f, -1f, -0.82f, -1f),
                new Finger(-0.45f, -1f, -0.82f, -0.5f),
                new Finger(-0.45f, -1f, -0.82f, -1f),
                new Finger(-0.45f, -1f, -0.82f, -1f),
            },
            // Grips: handle across the palm at the WeaponSocket (palm skin, ~9.6 cm from the wrist). Middle / ring rest on
            // it, the little finger curls further past the end of a short handle (HandPoseCapture: grip gaps).
            [HandPose.OneHandGrip] = new[]
            {
                new Finger(-0.5f, -0.8f, -0.5f, 0.5f),
                new Finger(-0.45f, -1f, -0.6f, -0.6f),
                new Finger(-0.45f, -1f, -0.6f, -0.3f),
                new Finger(-0.45f, -1f, -0.6f, -0.6f),
                new Finger(-0.6f, -1f, -0.8f, -0.6f),
            },
            [HandPose.TwoHandGrip] = new[]
            {
                new Finger(-0.5f, -0.8f, -0.5f, 0.5f),
                new Finger(-0.45f, -1f, -0.6f, -0.6f),
                new Finger(-0.45f, -1f, -0.6f, -0.3f),
                new Finger(-0.45f, -1f, -0.6f, -0.6f),
                new Finger(-0.6f, -1f, -0.8f, -0.6f),
            },
            // Riser handle (5.5 cm wide along the fingers, 4.2 cm deep) across the palm at 30° (spec-bow-hand-grip): the
            // proximal phalanges lie along its side, the middle ones close over its face. Little finger curls more at the
            // PIP (an open one pushes its tip into the flaring riser). Gaps: tips 1–12 mm off the surface.
            [HandPose.BowGrip] = new[]
            {
                new Finger(-0.5f, -0.6f, -0.3f, 0.3f),
                new Finger(-0.05f, -0.25f, -0.16f, -0.6f),
                new Finger(-0.45f, -0.35f, -0.23f, -0.3f),
                new Finger(-0.45f, -0.15f, -0.1f, -0.6f),
                new Finger(-0.2f, -0.9f, -0.59f, -0.6f),
            },
            // Bow carry seat (spec-bow-combat-idle, archetype carryPose): the riser runs along the palm toward the
            // fingers (pre-spec-bow-hand-grip seat), held loosely while the bow idles. Middle / ring curl less than BowGrip
            // (their tips sank into the riser), the little finger tucks against the ring finger to reach the riser's edge.
            // Gaps (HandPoseCapture bowidle): tips Th 14 / In 32 / Mi −2 / Ri 1 / Li 12 mm (no finger skin inside the palm).
            [HandPose.BowCarry] = new[]
            {
                new Finger(-0.5f, -0.6f, -0.3f, 0.3f),
                new Finger(-0.05f, -0.25f, -0.16f, -0.6f),
                new Finger(-0.4f, -0.35f, -0.23f, -0.3f),
                new Finger(-0.3f, -0.15f, -0.1f, -0.6f),
                new Finger(-0.5f, -0.65f, -0.42f, -1f),
            },
            // The shield's handle bar behind the boss: same closure as the weapon grip (bar ~2 cm thick).
            [HandPose.ShieldHold] = new[]
            {
                new Finger(-0.5f, -0.8f, -0.5f, 0.5f),
                new Finger(-0.45f, -1f, -0.6f, -0.6f),
                new Finger(-0.45f, -1f, -0.6f, -0.3f),
                new Finger(-0.45f, -1f, -0.6f, -0.6f),
                new Finger(-0.6f, -1f, -0.8f, -0.6f),
            },
        };

        public static HandPose[] AllPoses => (HandPose[])Enum.GetValues(typeof(HandPose));

        public static string ClipPath(bool left, HandPose pose) => $"{FOLDER}/{(left ? "Left" : "Right")}_{pose}.anim";
        public static string MaskPath(bool left) => $"{FOLDER}/HandMask_{(left ? "Left" : "Right")}.mask";

        /// <summary>Animator muscle property names for one finger, e.g. <c>RightHand.Index.1 Stretched</c>.</summary>
        public static string Muscle(bool left, string finger, string suffix) => $"{(left ? "LeftHand" : "RightHand")}.{finger}.{suffix}";

        [MenuItem("Tools/Characters/Build Hand Poses")]
        private static void BuildFromMenu()
        {
            try { GameLog.Info(TAG, BuildAll()); }
            catch (Exception exception) { GameLog.Error(TAG, exception.ToString()); }
        }

        /// <summary>Writes the clips and masks and rebuilds both layers. Returns a summary; throws on a missing asset or pose.</summary>
        public static string BuildAll()
        {
            foreach (var pose in AllPoses)
                if (!Poses.ContainsKey(pose)) throw new InvalidOperationException($"HandPoseBuilder: no values for {pose}");

            // Validate everything before writing anything: a half-run leaves layers out of order.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
            if (controller == null) throw new InvalidOperationException($"HandPoseBuilder: no controller at {CONTROLLER_PATH}");
            EnsureIntParam(controller, LEFT_PARAM, check: true);
            EnsureIntParam(controller, RIGHT_PARAM, check: true);

            EnsureFolder(FOLDER);
            var clips = new Dictionary<(bool, HandPose), AnimationClip>();
            foreach (bool left in new[] { true, false })
                foreach (var pose in AllPoses)
                    clips[(left, pose)] = WriteClip(left, pose);
            var leftMask = WriteMask(true);
            var rightMask = WriteMask(false);

            EnsureIntParam(controller, LEFT_PARAM, check: false);
            EnsureIntParam(controller, RIGHT_PARAM, check: false);
            BuildLayer(controller, LEFT_LAYER, LEFT_PARAM, leftMask, pose => clips[(true, pose)]);
            BuildLayer(controller, RIGHT_LAYER, RIGHT_PARAM, rightMask, pose => clips[(false, pose)]);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return $"built {clips.Count} clips, 2 masks, layers {LEFT_LAYER} / {RIGHT_LAYER}";
        }

        /// <summary>Writes the pose's finger muscles into <paramref name="muscles"/> (indexed like <see cref="HumanTrait.MuscleName"/>).</summary>
        public static void ApplyToMuscles(HandPose pose, bool left, float[] muscles)
        {
            var values = Poses[pose];
            string side = left ? "Left" : "Right";
            for (int f = 0; f < Fingers.Length; f++)
            {
                SetMuscle(muscles, $"{side} {Fingers[f]} 1 Stretched", values[f].S1);
                SetMuscle(muscles, $"{side} {Fingers[f]} 2 Stretched", values[f].S2);
                SetMuscle(muscles, $"{side} {Fingers[f]} 3 Stretched", values[f].S3);
                SetMuscle(muscles, $"{side} {Fingers[f]} Spread", values[f].Spread);
            }
        }

        private static void SetMuscle(float[] muscles, string humanTraitName, float value)
        {
            int index = Array.IndexOf(HumanTrait.MuscleName, humanTraitName);
            if (index < 0) throw new InvalidOperationException($"HandPoseBuilder: unknown muscle '{humanTraitName}'");
            muscles[index] = value;
        }

        private static AnimationClip WriteClip(bool left, HandPose pose)
        {
            string path = ClipPath(left, pose);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());

            var values = Poses[pose];
            for (int f = 0; f < Fingers.Length; f++)
            {
                SetCurve(clip, Muscle(left, Fingers[f], "1 Stretched"), values[f].S1);
                SetCurve(clip, Muscle(left, Fingers[f], "2 Stretched"), values[f].S2);
                SetCurve(clip, Muscle(left, Fingers[f], "3 Stretched"), values[f].S3);
                SetCurve(clip, Muscle(left, Fingers[f], "Spread"), values[f].Spread);
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void SetCurve(AnimationClip clip, string property, float value)
        {
            var binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), property);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, CLIP_LENGTH, value));
        }

        private static AvatarMask WriteMask(bool left)
        {
            string path = MaskPath(left);
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.SetHumanoidBodyPartActive(left ? AvatarMaskBodyPart.LeftFingers : AvatarMaskBodyPart.RightFingers, true);
            mask.transformCount = 0;
            EditorUtility.SetDirty(mask);
            return mask;
        }

        // check = only validate (throws on a same-name non-int parameter); otherwise adds the int when missing.
        private static void EnsureIntParam(AnimatorController controller, string name, bool check)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name != name) continue;
                if (p.type != AnimatorControllerParameterType.Int)
                    throw new InvalidOperationException($"HandPoseBuilder: parameter {name} exists but is not an int");
                return;
            }
            if (!check) controller.AddParameter(name, AnimatorControllerParameterType.Int);
        }

        private static void BuildLayer(AnimatorController controller, string layerName, string param, AvatarMask mask,
            Func<HandPose, AnimationClip> clipFor)
        {
            // Rebuilt in place: a rebuild keeps the layer's index (other layers may sit after it).
            var layers = controller.layers;
            int index = -1;
            for (int i = layers.Length - 1; i >= 0; i--)
            {
                if (layers[i].name != layerName) continue;
                controller.RemoveLayer(i);
                index = i;
            }

            var sm = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, controller);

            foreach (var pose in AllPoses)
            {
                var state = sm.AddState(pose.ToString());
                state.motion = clipFor(pose);
                state.writeDefaultValues = false;
                if (pose == HandPose.Relaxed) sm.defaultState = state;

                var t = sm.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.Equals, (int)pose, param);
                t.hasExitTime = false;
                t.duration = TRANSITION_SECONDS;
                t.hasFixedDuration = true;
                t.canTransitionToSelf = false;
            }

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = layerName,
                stateMachine = sm,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f,
            });
            if (index < 0) return; // first build: appended
            var ordered = new List<AnimatorControllerLayer>(controller.layers);
            var added = ordered[ordered.Count - 1];
            ordered.RemoveAt(ordered.Count - 1);
            ordered.Insert(Mathf.Min(index, ordered.Count), added);
            controller.layers = ordered.ToArray();
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
