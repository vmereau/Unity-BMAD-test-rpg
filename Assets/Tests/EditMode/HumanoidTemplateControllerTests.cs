using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;

namespace Tests.EditMode
{
    /// <summary>Humanoid_Template's FullBody layer must mirror the Attack layer (spec-two-handed-full-body-attacks).</summary>
    public class HumanoidTemplateControllerTests
    {
        private const string CONTROLLER_PATH = "Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller";

        private static AnimatorController LoadController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
            Assert.That(controller, Is.Not.Null, CONTROLLER_PATH);
            return controller;
        }

        private static int LayerIndex(AnimatorController controller, string name)
        {
            for (int i = 0; i < controller.layers.Length; i++)
                if (controller.layers[i].name == name) return i;
            Assert.Fail($"No '{name}' layer");
            return -1;
        }

        [Test]
        public void FullBodyLayer_IsSyncedToAttack_Unmasked_WeightZero()
        {
            AnimatorController controller = LoadController();
            AnimatorControllerLayer fullBody = controller.layers[LayerIndex(controller, "FullBody")];
            Assert.That(fullBody.syncedLayerIndex, Is.EqualTo(LayerIndex(controller, "Attack")));
            Assert.That(fullBody.avatarMask, Is.Null);
            Assert.That(fullBody.defaultWeight, Is.EqualTo(0f), "NPCs share the controller: FullBody must default to 0");
            Assert.That(fullBody.blendingMode, Is.EqualTo(AnimatorLayerBlendingMode.Override));
        }

        [Test]
        public void FullBodyLayer_MirrorsEveryAttackStateMotion()
        {
            AnimatorController controller = LoadController();
            AnimatorControllerLayer attack = controller.layers[LayerIndex(controller, "Attack")];
            AnimatorControllerLayer fullBody = controller.layers[LayerIndex(controller, "FullBody")];
            foreach (ChildAnimatorState child in attack.stateMachine.states)
                Assert.That(fullBody.GetOverrideMotion(child.state), Is.SameAs(child.state.motion),
                    $"FullBody motion of '{child.state.name}' differs from the Attack layer — copy it with SetOverrideMotion");
        }
    }
}
