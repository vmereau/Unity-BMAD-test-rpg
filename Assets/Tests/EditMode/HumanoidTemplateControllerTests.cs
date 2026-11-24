using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Game.Animations;
using UnityEditor.Animations;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Humanoid_Template layout (spec-full-body-attacks-root-motion): attacks and every other full-body state live on the
    /// Base Layer; the masked UpperBody layer (weight 0 by default) only holds the combat carry pose, block, draw / sheathe
    /// and the bow aim states.
    /// </summary>
    public class HumanoidTemplateControllerTests
    {
        private const string CONTROLLER_PATH = "Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller";

        private static readonly string[] AttackStates = { "Attack_1_State", "Attack_2_State", "Attack_3_State" };

        private static readonly HashSet<string> UpperBodyStates = new()
        {
            "LockOn Locomotion", "CombatIdle", "Block_State", "Draw_State", "Sheathe_State",
            "BowDrawArrow_State", "BowAim_State", "BowFire_State", "Drink_State",
        };

        // Base states without a clip on purpose: Death / Dead (ragdoll), GetHit (no clip yet).
        private static readonly HashSet<string> MotionlessBaseStates = new() { "Death", "Dead", "GetHit" };

        private static AnimatorController LoadController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
            Assert.That(controller, Is.Not.Null, CONTROLLER_PATH);
            return controller;
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name) =>
            sm.states.Select(c => c.state).FirstOrDefault(s => s.name == name);

        [Test]
        public void Layers_AreBaseUpperBodyThenHands_NoFullBodyLayer()
        {
            AnimatorController controller = LoadController();
            Assert.That(controller.layers.Select(l => l.name),
                Is.EqualTo(new[] { "Base Layer", "UpperBody", "LeftHand", "RightHand" }));
        }

        // spec-hand-pose-layer: finger-only layers built by Tools/Characters/Build Hand Poses.
        [TestCase(2, "LeftHand", "LeftHandPose", AvatarMaskBodyPart.LeftFingers, "LeftHand.")]
        [TestCase(3, "RightHand", "RightHandPose", AvatarMaskBodyPart.RightFingers, "RightHand.")]
        public void HandLayer_FingerMaskOneEventFreeFingerClipPerPose(int index, string name, string param,
            AvatarMaskBodyPart fingers, string curvePrefix)
        {
            AnimatorController controller = LoadController();
            AnimatorControllerLayer layer = controller.layers[index];
            Assert.That(layer.name, Is.EqualTo(name));
            Assert.That(layer.defaultWeight, Is.EqualTo(1f));
            Assert.That(layer.blendingMode, Is.EqualTo(AnimatorLayerBlendingMode.Override));
            Assert.That(layer.avatarMask, Is.Not.Null);
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(part), Is.EqualTo(part == fingers), $"{name} mask: {part}");

            Assert.That(controller.parameters.Any(p => p.name == param && p.type == AnimatorControllerParameterType.Int), param);
            Assert.That(layer.stateMachine.defaultState.name, Is.EqualTo(nameof(HandPose.Relaxed)));
            foreach (HandPose pose in System.Enum.GetValues(typeof(HandPose)))
            {
                AnimatorState state = FindState(layer.stateMachine, pose.ToString());
                Assert.That(state, Is.Not.Null, $"{name}: no state for {pose}");
                var clip = state.motion as AnimationClip;
                Assert.That(clip, Is.Not.Null, $"{name}/{pose}: no clip");
                Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty, $"{name}/{pose}: clip events would fire on every layer");
                Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip), Is.Empty);
                EditorCurveBinding[] curves = AnimationUtility.GetCurveBindings(clip);
                Assert.That(curves, Is.Not.Empty, $"{name}/{pose}: no curves");
                Assert.That(curves.All(b => b.propertyName.StartsWith(curvePrefix)), $"{name}/{pose}: non-finger curve");

                AnimatorStateTransition[] entries = layer.stateMachine.anyStateTransitions.Where(t => t.destinationState == state).ToArray();
                Assert.That(entries.Length, Is.EqualTo(1), $"{name}/{pose}: one AnyState transition");
                Assert.That(entries[0].canTransitionToSelf, Is.False);
                Assert.That(entries[0].conditions.Single().parameter, Is.EqualTo(param));
                Assert.That(entries[0].conditions.Single().mode, Is.EqualTo(AnimatorConditionMode.Equals));
                Assert.That(entries[0].conditions.Single().threshold, Is.EqualTo((float)(int)pose));
            }
        }

        [Test]
        public void UpperBody_IsMasked_WeightZero_Override()
        {
            AnimatorControllerLayer upper = LoadController().layers[1];
            Assert.That(upper.avatarMask, Is.Not.Null);
            Assert.That(upper.defaultWeight, Is.EqualTo(0f), "the drivers fade it in; NPCs and the player start at 0");
            Assert.That(upper.blendingMode, Is.EqualTo(AnimatorLayerBlendingMode.Override));
            Assert.That(upper.syncedLayerIndex, Is.EqualTo(-1));
        }

        [Test]
        public void UpperBody_HoldsOnlyUpperBodyStates()
        {
            AnimatorStateMachine upper = LoadController().layers[1].stateMachine;
            foreach (ChildAnimatorState child in upper.states)
                Assert.That(UpperBodyStates.Contains(child.state.name), $"unexpected UpperBody state '{child.state.name}'");
        }

        [Test]
        public void AttackStates_LiveOnBase_WithBothSmbs_AndAttackTag()
        {
            AnimatorStateMachine baseSm = LoadController().layers[0].stateMachine;
            foreach (string name in AttackStates)
            {
                AnimatorState state = FindState(baseSm, name);
                Assert.That(state, Is.Not.Null, name);
                Assert.That(state.tag, Is.EqualTo("Attack"), name);
                Assert.That(state.behaviours.Any(b => b.GetType().Name == "SMB_AttackState"), name + ": SMB_AttackState");
                Assert.That(state.behaviours.Any(b => b.GetType().Name == "SMB_EntityAttackState"), name + ": SMB_EntityAttackState");
            }
        }

        [Test]
        public void AttackStates_ExitOnBlock_First() // instant block-cancel
        {
            AnimatorStateMachine baseSm = LoadController().layers[0].stateMachine;
            foreach (string name in AttackStates)
            {
                AnimatorStateTransition first = FindState(baseSm, name).transitions[0];
                Assert.That(first.conditions.Any(c => c.parameter == "IsBlocking" && c.mode == AnimatorConditionMode.If), name);
            }
        }

        // Destination: the outgoing attack's exit-time transition can't cut a combo blend (Source did), while AnyState
        // dodge / jump / reactions / death still interrupt it at once (None froze the whole body for the blend).
        [Test]
        public void AttackTransitions_AreInterruptibleByDestinationOnly()
        {
            AnimatorStateMachine baseSm = LoadController().layers[0].stateMachine;
            foreach (AnimatorState state in AttackStates.Select(n => FindState(baseSm, n)))
            foreach (AnimatorStateTransition t in state.transitions)
                Assert.That(t.interruptionSource, Is.EqualTo(TransitionInterruptionSource.Destination),
                    $"{state.name} -> {t.destinationState?.name}");
            AnimatorStateTransition anyToAttack = baseSm.anyStateTransitions.Single(t => t.destinationState != null
                && t.destinationState.name == "Attack_1_State");
            Assert.That(anyToAttack.interruptionSource, Is.EqualTo(TransitionInterruptionSource.Destination));
        }

        [Test]
        public void EveryBaseState_HasAMotion_ExceptTheIntentionalOnes()
        {
            AnimatorStateMachine baseSm = LoadController().layers[0].stateMachine;
            foreach (ChildAnimatorState child in baseSm.states)
                if (!MotionlessBaseStates.Contains(child.state.name))
                    Assert.That(child.state.motion, Is.Not.Null, $"Base state '{child.state.name}' has no motion");
        }

        [Test]
        public void GetHit_IsNotAnAnyStateTransition() // a clip-less GetHit must not cut attacks
        {
            AnimatorStateMachine baseSm = LoadController().layers[0].stateMachine;
            Assert.That(baseSm.anyStateTransitions.Any(t => t.destinationState != null && t.destinationState.name == "GetHit"),
                Is.False);
        }

        // spec-one-hand-braced-block: the shield is an offhand that changes the block clip, not only its values.
        private const string COMBAT = "Assets/_Game/Art/Characters/Humanoids/Animations/Combat/";

        [Test]
        public void BlockState_BlendsWeaponAndShieldGuard_OnShieldInHand()
        {
            AnimatorController controller = LoadController();
            Assert.That(controller.parameters.Any(p => p.name == "ShieldInHand" && p.type == UnityEngine.AnimatorControllerParameterType.Float),
                "float ShieldInHand");
            AnimatorState block = FindState(controller.layers[1].stateMachine, "Block_State");
            Assert.That(block, Is.Not.Null);
            Assert.That(block.tag, Is.EqualTo("UpperBodyAction"));
            var tree = block.motion as BlendTree;
            Assert.That(tree, Is.Not.Null, "Block_State plays a blend tree");
            Assert.That(tree.blendType, Is.EqualTo(BlendTreeType.Simple1D));
            Assert.That(tree.blendParameter, Is.EqualTo("ShieldInHand"));
            Assert.That(tree.children.Select(c => (c.motion.name, c.threshold)),
                Is.EqualTo(new[] { ("Unarmed Block Guard", 0f), ("Block Idle", 1f) }));
        }

        [Test]
        public void SwordOverride_BracedGuardWithoutShield_BlockIdleWithShield()
        {
            var overrides = Overrides(COMBAT + "one handed/Sword_AnimatorOverride.overrideController");
            Assert.That(overrides["Unarmed Block Guard"], Is.EqualTo("Sword Braced Guard"));
            Assert.That(overrides["Block Idle"], Is.Null, "a shield in hand plays the base Block Idle");
            var twoHanded = Overrides(COMBAT + "two handed/TwoHanded_AnimatorOverride.overrideController");
            Assert.That(twoHanded["Unarmed Block Guard"], Is.EqualTo("2hand blocking"));
            Assert.That(twoHanded["Block Idle"], Is.EqualTo("2hand blocking"));
        }

        // spec-bow-combat-idle: the bow's CombatIdle is the untouched Mixamo standing idle (looping, event-free).
        [Test]
        public void BowOverride_CombatIdleIsTheStandingBowIdle()
        {
            var overrides = Overrides(COMBAT + "Bow/Bow_AnimatorOverride.overrideController");
            Assert.That(overrides["Unarmed Idle Guard"], Is.EqualTo("Standing Bow Idle"));
            var clips = new SerializedObject(AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                COMBAT + "Bow/Bow_AnimatorOverride.overrideController")).FindProperty("m_Clips");
            for (int i = 0; i < clips.arraySize; i++)
                Assert.That(clips.GetArrayElementAtIndex(i).FindPropertyRelative("m_OverrideClip").objectReferenceValue,
                    Is.Not.Null, "no empty override entries (ApplyOverrides leaves one per unmapped clip)");
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(COMBAT + "Bow/Standing Bow Idle.fbx").OfType<AnimationClip>()
                .Single(c => !c.name.StartsWith("__preview"));
            Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.True);
            Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty);
        }

        // spec-bow-hand-grip: the aim states play the wrist-corrected bakes (Tools/Characters/Bake Bow Wrist Clips).
        [TestCase("BowDrawArrow_State", "Bow Draw Arrow.anim", false)]
        [TestCase("BowAim_State", "Bow Aim Overdraw.anim", true)]
        [TestCase("BowFire_State", "Bow Fire Arrow.anim", false)]
        public void BowStates_PlayTheWristBakes(string stateName, string clipFile, bool loops)
        {
            AnimatorController controller = LoadController();
            AnimatorState state = FindState(controller.layers.First(l => l.name == "UpperBody").stateMachine, stateName);
            Assert.That(state, Is.Not.Null, stateName);
            Assert.That(AssetDatabase.GetAssetPath(state.motion), Is.EqualTo(COMBAT + "Bow/" + clipFile));
            var clip = (UnityEngine.AnimationClip)state.motion;
            Assert.That(clip.isHumanMotion, Is.True, "humanoid muscle curves");
            Assert.That(clip.events, Is.Empty);
            Assert.That(clip.isLooping, Is.EqualTo(loops));
            if (stateName == "BowAim_State")
                Assert.That(state.behaviours.OfType<Game.Combat.SMB_BowAimState>().Count(), Is.EqualTo(1), "the nock SMB");
        }

        // Stale-bake guard: the .anim is its FBX source plus the wrist offsets (re-run Bake Bow Wrist Clips if this fails
        // after a reimport or an offset change).
        [Test]
        public void BowWristBakes_MatchSourcePlusOffsets()
        {
            foreach (var (source, baked, _) in Game.Editor.Characters.BowClipWristBaker.Clips)
            {
                var src = AssetDatabase.LoadAllAssetsAtPath(source).OfType<UnityEngine.AnimationClip>()
                    .First(c => !c.name.StartsWith("__preview"));
                var bake = AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>(baked);
                Assert.That(bake, Is.Not.Null, baked);
                Assert.That(bake.length, Is.EqualTo(src.length).Within(1e-4f), baked);
                Assert.That(AnimationUtility.GetCurveBindings(bake).Length, Is.EqualTo(AnimationUtility.GetCurveBindings(src).Length), baked);
                foreach (var (muscle, offset) in Game.Editor.Characters.BowClipWristBaker.WristOffsets)
                {
                    var binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle);
                    var s = AnimationUtility.GetEditorCurve(src, binding).keys;
                    var b = AnimationUtility.GetEditorCurve(bake, binding).keys;
                    Assert.That(b.Length, Is.EqualTo(s.Length), $"{baked} {muscle}");
                    for (int k = 0; k < s.Length; k++)
                        Assert.That(b[k].value, Is.EqualTo(Game.Editor.Characters.BowClipWristBaker.OffsetValue(s[k].value, offset))
                            .Within(1e-4f), $"{baked} {muscle} key {k}");
                }
            }
        }

        [Test]
        public void BowWristBake_AddsOffsetWithinRange_KeepsOutOfRangeSourceValue()
        {
            Assert.That(Game.Editor.Characters.BowClipWristBaker.OffsetValue(0.1f, 0.5f), Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(Game.Editor.Characters.BowClipWristBaker.OffsetValue(0.8f, 0.5f), Is.EqualTo(1f));
            Assert.That(Game.Editor.Characters.BowClipWristBaker.OffsetValue(1.5f, 0.5f), Is.EqualTo(1.5f));
            Assert.That(Game.Editor.Characters.BowClipWristBaker.OffsetValue(-1.2f, 0.5f), Is.EqualTo(-0.7f).Within(1e-5f));
        }

        // spec-bow-hand-grip: the bow changes hands where the left hand is on its grip on the back.
        [TestCase("Standing Equip Bow.fbx", 0.30f)]
        [TestCase("Standing Disarm Bow.fbx", 0.325f)]
        public void BowDrawSheathe_WeaponSwapOnTheGrabFrame(string file, float normalizedTime)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(COMBAT + "Bow/" + file).OfType<UnityEngine.AnimationClip>()
                .First(c => !c.name.StartsWith("__preview"));
            var swaps = clip.events.Where(e => e.functionName == "WeaponSwap").ToArray();
            Assert.That(swaps.Length, Is.EqualTo(1));
            Assert.That(swaps[0].time / clip.length, Is.EqualTo(normalizedTime).Within(0.005f));
        }

        private static Dictionary<string, string> Overrides(string path)
        {
            var oc = AssetDatabase.LoadAssetAtPath<UnityEngine.AnimatorOverrideController>(path);
            Assert.That(oc, Is.Not.Null, path);
            var list = new List<KeyValuePair<UnityEngine.AnimationClip, UnityEngine.AnimationClip>>();
            oc.GetOverrides(list);
            return list.GroupBy(kv => kv.Key.name).ToDictionary(g => g.Key, g => g.First().Value != null ? g.First().Value.name : null);
        }
    }
}
