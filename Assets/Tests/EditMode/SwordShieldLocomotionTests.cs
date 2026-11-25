using System.Collections.Generic;
using System.Linq;
using Game.Animations;
using Game.Editor.Characters;
using Game.Inventory;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Drawn-sword locomotion from the Mixamo sword-and-shield pack (spec-sword-shield-pack-locomotion): Base
    /// <c>LockOn Locomotion</c> blends the free tree into a combat tree of clip copies on <c>CombatLocomotion</c>, the sword
    /// override maps the copies (and the shield block hold) to the pack, other overrides leave them, and the pack clips
    /// import as in-place loops.
    /// </summary>
    public class SwordShieldLocomotionTests
    {
        private const string ANIMATIONS = "Assets/_Game/Art/Characters/Humanoids/Animations/";
        private const string CLIPS = ANIMATIONS + "Combat/one handed/sword and shield/";
        private const string CONTROLLER = "Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller";
        private const string ARCHETYPES = "Assets/_Game/Data/Items/Weapons/Archetypes/";

        // combat tree key → (pack FBX, clip)
        private static readonly (string key, string fbx, string clip)[] SwordMap =
        {
            ("Combat Idle", "sword and shield idle.fbx", "sword shield idle"),
            ("Combat Walking", "sword and shield walk.fbx", "sword shield walk"),
            ("Combat Walking Backwards", "sword and shield walk (2).fbx", "sword shield walk back"),
            ("Combat Running", "sword and shield run.fbx", "sword shield run"),
            ("Combat Running Backwards", "sword and shield run (2).fbx", "sword shield run back"),
            ("Combat Left strafe walking", "sword and shield strafe (2).fbx", "sword shield strafe walk left"),
            ("Combat Right Strafe Walking", "sword and shield strafe.fbx", "sword shield strafe walk right"),
            ("Combat Left Strafe Running", "sword and shield strafe (3).fbx", "sword shield strafe run left"),
            ("Combat Right Strafe Running", "sword and shield strafe (4).fbx", "sword shield strafe run right"),
            ("Block Idle", "sword and shield block idle.fbx", "sword shield block idle"),
        };

        private static readonly string[] OtherOverrides =
        {
            ANIMATIONS + "Combat/one handed/Axe_AnimatorOverride.overrideController",
            ANIMATIONS + "Combat/two handed/TwoHanded_AnimatorOverride.overrideController",
            ANIMATIONS + "Combat/Bow/Bow_AnimatorOverride.overrideController",
            "Assets/_Game/Art/Characters/Humanoids/Overrides/Humanoid_Base.overrideController",
        };

        private static Dictionary<string, AnimationClip> Overrides(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            Assert.That(controller, Is.Not.Null, path);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(pairs);
            return pairs.Where(p => p.Key != null && p.Value != null).ToDictionary(p => p.Key.name, p => p.Value);
        }

        private static BlendTree LocomotionWrapper()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER);
            var state = controller.layers[0].stateMachine.states.First(s => s.state.name == CombatLocomotionBuilder.BASE_STATE).state;
            var wrapper = state.motion as BlendTree;
            Assert.That(wrapper, Is.Not.Null, "LockOn Locomotion has no blend tree");
            return wrapper;
        }

        [Test]
        public void BaseLocomotion_BlendsFreeTreeIntoCombatTree_OnCombatLocomotionFloat()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER);
            Assert.That(controller.parameters.Any(p => p.name == HumanoidAnimationBridge.COMBAT_LOCOMOTION_PARAM
                                                      && p.type == AnimatorControllerParameterType.Float), "CombatLocomotion float");
            var wrapper = LocomotionWrapper();
            Assert.That(wrapper.blendType, Is.EqualTo(BlendTreeType.Simple1D));
            Assert.That(wrapper.blendParameter, Is.EqualTo(HumanoidAnimationBridge.COMBAT_LOCOMOTION_PARAM));
            Assert.That(wrapper.children.Select(c => c.threshold), Is.EqualTo(new[] { 0f, 1f }));
            var free = wrapper.children[0].motion as BlendTree;
            var combat = wrapper.children[1].motion as BlendTree;
            Assert.That(free, Is.Not.Null);
            Assert.That(combat, Is.Not.Null);
            Assert.That(free.children.Any(c => c.motion != null && c.motion.name == "Walking"), "free tree keeps the generic clips");
            Assert.That(combat.children, Has.Length.EqualTo(free.children.Length));
            for (int i = 0; i < free.children.Length; i++)
            {
                Assert.That(combat.children[i].position, Is.EqualTo(free.children[i].position), combat.children[i].motion.name);
                Assert.That(combat.children[i].timeScale, Is.EqualTo(free.children[i].timeScale), combat.children[i].motion.name);
                Assert.That(combat.children[i].motion.name, Is.EqualTo(CombatLocomotionBuilder.COPY_PREFIX + free.children[i].motion.name));
                Assert.That(combat.children[i].motion, Is.Not.SameAs(free.children[i].motion), "copies are distinct override keys");
            }
        }

        [Test]
        public void SwordOverride_MapsCombatTreeAndBlockHold_ToThePack()
        {
            var overrides = Overrides(ANIMATIONS + "Combat/one handed/Sword_AnimatorOverride.overrideController");
            foreach (var (key, fbx, clip) in SwordMap)
            {
                Assert.That(overrides.ContainsKey(key), $"sword override does not map {key}");
                Assert.That(overrides[key].name, Is.EqualTo(clip), key);
                Assert.That(AssetDatabase.GetAssetPath(overrides[key]), Is.EqualTo(CLIPS + fbx), key);
            }
            var free = (BlendTree)LocomotionWrapper().children[0].motion;
            foreach (var generic in free.children.Select(c => c.motion.name))
                Assert.That(overrides.ContainsKey(generic), Is.False, $"{generic} also plays sheathed: never overridden");
        }

        [Test]
        public void OtherOverrides_LeaveTheCombatTree()
        {
            foreach (var path in OtherOverrides)
            {
                var mapped = Overrides(path).Keys.Where(k => k.StartsWith(CombatLocomotionBuilder.COPY_PREFIX)).ToArray();
                Assert.That(mapped, Is.Empty, path);
            }
        }

        [Test]
        public void PackLocomotionClips_ImportAsInPlaceHumanoidLoops()
        {
            foreach (var (_, fbx, clip) in SwordMap)
            {
                var importer = AssetImporter.GetAtPath(CLIPS + fbx) as ModelImporter;
                Assert.That(importer, Is.Not.Null, fbx);
                Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human), fbx);
                Assert.That(importer.clipAnimations, Has.Length.EqualTo(1), fbx);
                var c = importer.clipAnimations[0];
                Assert.That(c.name, Is.EqualTo(clip), fbx);
                Assert.That(c.loopTime && c.loopPose, fbx + " loops");
                Assert.That(c.lockRootRotation && c.keepOriginalOrientation, fbx + " root rotation baked on Original");
                Assert.That(c.lockRootHeightY && c.keepOriginalPositionY, fbx + " root Y baked on Original");
                // The two holds don't travel (the idle keeps its earlier XZ bake); the loops' travel goes to root motion,
                // discarded outside attacks (applyRootMotion off): they play in place.
                if (fbx != "sword and shield idle.fbx")
                    Assert.That(c.lockRootPositionXZ, Is.False, fbx + " root XZ not baked");
            }
        }

        [Test]
        public void OnlyTheSwordArchetype_HasFullBodyCombatLocomotion()
        {
            var guids = AssetDatabase.FindAssets("t:WeaponArchetypeSO", new[] { ARCHETYPES.TrimEnd('/') });
            Assert.That(guids, Is.Not.Empty, "no archetype found");
            foreach (var guid in guids)
            {
                var archetype = AssetDatabase.LoadAssetAtPath<WeaponArchetypeSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(archetype.fullBodyCombatLocomotion, Is.EqualTo(archetype.name == "Archetype_OneHandedSword"), archetype.name);
            }
        }
    }
}
