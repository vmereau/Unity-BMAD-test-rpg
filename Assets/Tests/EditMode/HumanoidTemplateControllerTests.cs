using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;

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
            "BowDrawArrow_State", "BowAim_State", "BowFire_State",
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
        public void Layers_AreBaseAndUpperBody_NoFullBodyLayer()
        {
            AnimatorController controller = LoadController();
            Assert.That(controller.layers.Select(l => l.name), Is.EqualTo(new[] { "Base Layer", "UpperBody" }));
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
    }
}
