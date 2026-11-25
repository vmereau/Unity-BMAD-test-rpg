using System.Collections.Generic;
using System.Linq;
using Game.Inventory;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// One-handed sword combo from the Mixamo sword-and-shield pack (spec-sword-shield-take-combo): the sword override maps
    /// the three attack states and the combat idle to the pack clips, the clips carry their own travel and the right
    /// events, every window closes before its state's exit, and the axe keeps the previous two-hit combo.
    /// </summary>
    public class SwordShieldComboTests
    {
        private const string ONE_HANDED = "Assets/_Game/Art/Characters/Humanoids/Animations/Combat/one handed/";
        private const string CLIPS = ONE_HANDED + "sword and shield/";
        private const string ARCHETYPES = "Assets/_Game/Data/Items/Weapons/Archetypes/";
        private const string CONTROLLER = "Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller";

        // base clip → (FBX, state) of the sword combo
        private static readonly (string baseClip, string fbx, string clip, string state)[] Steps =
        {
            ("lead jab step", "sword and shield slash.fbx", "sword shield cut", "Attack_1_State"),
            ("hook step", "sword and shield slash (3).fbx", "sword shield rising cut", "Attack_2_State"),
            ("uppercut step", "sword shield overhead.fbx", "sword shield overhead", "Attack_3_State"),
        };

        private static Dictionary<string, AnimationClip> Overrides(string file)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ONE_HANDED + file);
            Assert.That(controller, Is.Not.Null, file);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(pairs);
            return pairs.Where(p => p.Key != null && p.Value != null).ToDictionary(p => p.Key.name, p => p.Value);
        }

        private static ModelImporterClipAnimation ImportedClip(string fbx)
        {
            var importer = AssetImporter.GetAtPath(CLIPS + fbx) as ModelImporter;
            Assert.That(importer, Is.Not.Null, fbx);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human), fbx);
            Assert.That(importer.clipAnimations, Has.Length.EqualTo(1), fbx);
            return importer.clipAnimations[0];
        }

        // The exit-time transition back to locomotion (the one a late combo press can no longer interrupt).
        private static AnimatorStateTransition ExitTransition(string state)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER);
            var s = controller.layers[0].stateMachine.states.Select(c => c.state).First(x => x.name == state);
            var exit = s.transitions.FirstOrDefault(t => t.hasExitTime && t.destinationState != null
                                                         && t.destinationState.name == "LockOn Locomotion");
            Assert.That(exit, Is.Not.Null, state + " has no exit-time transition to LockOn Locomotion");
            return exit;
        }

        private static float ExitTime(string state) => ExitTransition(state).exitTime;

        private static Dictionary<string, float> Events(ModelImporterClipAnimation clip)
        {
            var names = clip.events.Select(e => e.functionName).ToList();
            Assert.That(names, Is.Unique, clip.name + ": one event of each kind");
            return clip.events.ToDictionary(e => e.functionName, e => e.time);
        }

        private static float Seconds(ModelImporterClipAnimation clip, string fbx)
        {
            AnimationClip asset = AssetDatabase.LoadAllAssetsAtPath(CLIPS + fbx).OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview"));
            return asset.length;
        }

        [Test]
        public void SwordOverride_MapsTheThreeAttacksAndTheIdle()
        {
            Dictionary<string, AnimationClip> map = Overrides("Sword_AnimatorOverride.overrideController");
            foreach (var step in Steps) Assert.That(map[step.baseClip].name, Is.EqualTo(step.clip), step.baseClip);
            Assert.That(map["Unarmed Idle Guard"].name, Is.EqualTo("sword shield idle"));
        }

        [Test]
        public void AxeOverride_KeepsThePreviousCombo()
        {
            Dictionary<string, AnimationClip> map = Overrides("Axe_AnimatorOverride.overrideController");
            Assert.That(map["lead jab step"].name, Is.EqualTo("SwordAttackLeft"));
            Assert.That(map["hook step"].name, Is.EqualTo("SwordAttackThrust"));
            Assert.That(map["Unarmed Idle Guard"].name, Is.EqualTo("SwordIdle"));
            var axe = AssetDatabase.LoadAssetAtPath<WeaponArchetypeSO>(ARCHETYPES + "Archetype_OneHandedAxe.asset");
            Assert.That(axe.animatorOverrideController.name, Is.EqualTo("Axe_AnimatorOverride"));
            Assert.That(axe.defaultComboSteps, Is.EqualTo(2));
            Assert.That(axe.attackSteps.All(s => !s.UsesRootMotion), Is.True, "axe keeps the scripted step");
        }

        [Test]
        public void SwordArchetype_ThreeClipDrivenSteps()
        {
            var sword = AssetDatabase.LoadAssetAtPath<WeaponArchetypeSO>(ARCHETYPES + "Archetype_OneHandedSword.asset");
            Assert.That(sword.animatorOverrideController.name, Is.EqualTo("Sword_AnimatorOverride"));
            Assert.That(sword.defaultComboSteps, Is.EqualTo(3));
            Assert.That(sword.attackSteps, Has.Length.EqualTo(3));
            foreach (AttackStep step in sword.attackSteps)
            {
                Assert.That(step.rootMotionScale, Is.EqualTo(1f));
                Assert.That(step.distance, Is.GreaterThan(0f), "NPCs use the scripted distance");
            }
        }

        [Test]
        public void Clips_CarryTheirTravel_EventsFitTheStates()
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                var (_, fbx, name, state) = Steps[i];
                ModelImporterClipAnimation clip = ImportedClip(fbx);
                Assert.That(clip.name, Is.EqualTo(name));
                Assert.That(clip.lockRootPositionXZ, Is.False, name + ": root XZ carries the travel (ActionRootMotion)");
                Assert.That(clip.lockRootRotation && clip.keepOriginalOrientation, Is.True, name + ": rotation baked, Original");
                Assert.That(clip.lockRootHeightY && clip.keepOriginalPositionY, Is.True, name + ": Y baked, Original");
                Assert.That(clip.loopTime, Is.False, name);

                float length = Seconds(clip, fbx);
                Dictionary<string, float> events = Events(clip);
                bool finisher = i == Steps.Length - 1;
                Assert.That(events.Keys, Is.EquivalentTo(finisher
                    ? new[] { "HitboxEnable", "HitboxDisable", "ComboWindowClose" }
                    : new[] { "HitboxEnable", "HitboxDisable", "ComboWindowOpen", "ComboWindowClose" }), name);
                Assert.That(events["HitboxEnable"], Is.LessThan(events["HitboxDisable"]), name);
                if (i > 0) Assert.That(events["HitboxEnable"] * length, Is.GreaterThanOrEqualTo(0.30f), name + ": chained hit late in the 0.42 s blend");
                if (!finisher) Assert.That(events["ComboWindowOpen"], Is.GreaterThanOrEqualTo(events["HitboxDisable"]), name);
                Assert.That(events["ComboWindowClose"], Is.LessThan(ExitTime(state)), name + ": window closes before the exit-time transition");
            }
        }

        // PlayerAttackTracker's watchdog force-ends an attack after attackStallTimeout without an attack callback: the gap
        // between consecutive events, and from the last one to the state's exit (end of the exit blend), must stay below it.
        [Test]
        public void Clips_NoCallbackGapLongerThanTheStallTimeout()
        {
            var config = AssetDatabase.LoadAssetAtPath<Game.Combat.CombatConfigSO>("Assets/_Game/Data/Config/CombatConfig.asset");
            Assert.That(config, Is.Not.Null);
            foreach (var (_, fbx, name, state) in Steps)
            {
                ModelImporterClipAnimation clip = ImportedClip(fbx);
                float length = Seconds(clip, fbx);
                AnimatorStateTransition exit = ExitTransition(state);
                float exitEnd = exit.exitTime * length + (exit.hasFixedDuration ? exit.duration : exit.duration * length);
                var times = clip.events.Select(e => e.time * length).Append(0f).Append(exitEnd).OrderBy(t => t).ToList();
                for (int i = 1; i < times.Count; i++)
                    Assert.That(times[i] - times[i - 1], Is.LessThan(config.attackStallTimeout),
                        $"{name}: {times[i] - times[i - 1]:F2} s without an attack callback");
            }
        }

        [Test]
        public void Idle_Loops()
        {
            var importer = AssetImporter.GetAtPath(CLIPS + "sword and shield idle.fbx") as ModelImporter;
            ModelImporterClipAnimation idle = importer.clipAnimations.Single();
            Assert.That(idle.name, Is.EqualTo("sword shield idle"));
            Assert.That(idle.loopTime, Is.True);
            Assert.That(idle.loopPose, Is.True, "Loop Pose");
            Assert.That(idle.events, Is.Empty);
        }
    }
}
