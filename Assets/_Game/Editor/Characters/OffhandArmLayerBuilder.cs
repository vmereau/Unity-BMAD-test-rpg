using System;
using System.Collections.Generic;
using Game.Animations;
using Game.Core;
using Game.Inventory;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor.Characters
{
    /// <summary>
    /// Builds the <c>LeftArm</c> layer of Humanoid_Template (spec-sword-shield-take-combo): a left-arm-only
    /// <see cref="AvatarMask"/> (shoulder to hand, no fingers — the <c>LeftHand</c> layer owns them), one constant-muscle
    /// clip per <see cref="OffhandArmPose"/> other than None, and the layer itself (Override, weight 0, after the hand
    /// layers). <c>HandPoseDriver</c> steps the weight (<see cref="OffhandArmRule"/>); one pose today, so the layer has one
    /// state. Idempotent: the clip and mask are overwritten in place (GUIDs kept), the layer rebuilt at its index.
    /// Muscle values are fitted on the Player rig over <c>sword and shield idle</c> (frame 0) by coordinate descent through
    /// a PlayableGraph (BAKE_RECIPES.md → Torch hold): hand at shoulder height, 0.40 m left / 0.26 m above / 0.14 m ahead of
    /// the chest bone, elbow out to the side, forearm 45° up, fist knuckles near vertical (torch upright). Idle and
    /// locomotion only: attacks release the layer (<see cref="OffhandArmRule"/>), no fixed pose clears the sword swings.
    /// </summary>
    public static class OffhandArmLayerBuilder
    {
        private const string TAG = "[OffhandArmLayerBuilder]";
        public const string FOLDER = "Assets/_Game/Art/Characters/Humanoids/Animations/OffhandPoses";
        public const string MASK_PATH = FOLDER + "/LeftArmMask.mask";
        public const string TORCH_CLIP_PATH = FOLDER + "/Torch Hold.anim";
        public const string TORCH_STATE = "TorchHold";
        private const float CLIP_LENGTH = 1f;

        /// <summary>Torch hold: left-arm muscles (humanoid units), constant over the clip.</summary>
        public static readonly (string muscle, float value)[] TorchHold =
        {
            ("Left Shoulder Down-Up", -0.105f),
            ("Left Shoulder Front-Back", 0.001f),
            ("Left Arm Down-Up", 0.062f),
            ("Left Arm Front-Back", 0.281f),
            ("Left Arm Twist In-Out", 0.821f),
            ("Left Forearm Stretch", -0.563f),
            ("Left Forearm Twist In-Out", 0.714f),
            ("Left Hand Down-Up", -0.069f),
            ("Left Hand In-Out", 0.706f),
        };

        [MenuItem("Tools/Characters/Build Offhand Arm Layer")]
        private static void BuildFromMenu()
        {
            try { GameLog.Info(TAG, Build()); }
            catch (Exception exception) { GameLog.Error(TAG, exception.ToString()); }
        }

        /// <summary>Writes the mask and clip and rebuilds the layer. Returns a summary; throws on a missing controller or muscle.</summary>
        public static string Build()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HandPoseBuilder.CONTROLLER_PATH);
            if (controller == null) throw new InvalidOperationException($"OffhandArmLayerBuilder: no controller at {HandPoseBuilder.CONTROLLER_PATH}");
            foreach (var (muscle, _) in TorchHold)
                if (Array.IndexOf(HumanTrait.MuscleName, muscle) < 0)
                    throw new InvalidOperationException($"OffhandArmLayerBuilder: unknown muscle '{muscle}'");

            EnsureFolder(FOLDER);
            var mask = WriteMask();
            var torch = WriteClip(TORCH_CLIP_PATH, TorchHold);
            BuildLayer(controller, mask, torch);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return $"built {OffhandArmRule.LAYER} layer (mask, {torch.name})";
        }

        private static AvatarMask WriteMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MASK_PATH);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, MASK_PATH);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.transformCount = 0;
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static AnimationClip WriteClip(string path, (string muscle, float value)[] muscles)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = 30f;
            foreach (var (muscle, value) in muscles)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle),
                    AnimationCurve.Constant(0f, CLIP_LENGTH, value));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void BuildLayer(AnimatorController controller, AvatarMask mask, AnimationClip torch)
        {
            // Rebuilt in place: a rebuild keeps the layer's index; a first build appends it after the hand layers.
            var layers = controller.layers;
            int index = -1;
            for (int i = layers.Length - 1; i >= 0; i--)
            {
                if (layers[i].name != OffhandArmRule.LAYER) continue;
                controller.RemoveLayer(i);
                index = i;
            }

            var sm = new AnimatorStateMachine { name = OffhandArmRule.LAYER, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, controller);
            var state = sm.AddState(TORCH_STATE);
            state.motion = torch;
            state.writeDefaultValues = false;
            sm.defaultState = state;

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = OffhandArmRule.LAYER,
                stateMachine = sm,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0f,
            });
            if (index < 0) return;
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
