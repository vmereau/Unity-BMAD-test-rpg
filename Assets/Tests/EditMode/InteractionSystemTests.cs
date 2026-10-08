using NUnit.Framework;
using Game.Inventory;
using Game.World;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit-mode tests for interaction system logic.
    ///
    /// InteractionSystem is a MonoBehaviour and cannot be driven in edit mode (no scene). Its pure
    /// logic — focus-change detection, prompt verb/name resolution, outline layer-bit math and color
    /// selection — lives in the static <see cref="InteractionFocus"/> class, which these tests call
    /// directly. The only remaining mirror is <see cref="IsPromptCandidate"/>: the CanInteract gate is
    /// still inline in InteractionSystem.Update().
    /// </summary>
    public class InteractionSystemTests
    {
        private class StubInteractable : IInteractable
        {
            private readonly string _prompt;
            private readonly string _name;
            public StubInteractable(string prompt = "Test Prompt", bool canInteract = true, string name = "")
            {
                _prompt = prompt;
                _name = name;
                CanInteract = canInteract;
            }
            public string InteractPrompt => _prompt;
            public string NameTag => _name;
            public bool CanInteract { get; }
            public void Interact() { }
        }

        // Mirrors InteractionSystem.Update() prompt-candidate gate (skip when !CanInteract)
        private bool IsPromptCandidate(IInteractable interactable) =>
            interactable != null && interactable.CanInteract;

        private static bool TargetChanged(IInteractable previous, IInteractable next) =>
            InteractionFocus.HasFocusChanged(previous, "", "", false, next, "", "", false);

        // ── Crosshair color tests ──────────────────────────────────────────────

        [Test]
        public void Crosshair_NoInteractable_UsesDefaultColor()
        {
            Color color = InteractionFocus.SelectCrosshairColor(false, Color.white, Color.yellow);
            Assert.AreEqual(Color.white, color);
        }

        [Test]
        public void Crosshair_WithInteractable_UsesHighlightColor()
        {
            Color color = InteractionFocus.SelectCrosshairColor(true, Color.white, Color.yellow);
            Assert.AreEqual(Color.yellow, color);
        }

        [Test]
        public void Crosshair_CustomColors_AreRespected()
        {
            Color customDefault = Color.gray;
            Color customHighlight = Color.green;
            Assert.AreEqual(customHighlight, InteractionFocus.SelectCrosshairColor(true, customDefault, customHighlight));
            Assert.AreEqual(customDefault, InteractionFocus.SelectCrosshairColor(false, customDefault, customHighlight));
        }

        // ── Prompt resolution tests ───────────────────────────────────────────

        [Test]
        public void Prompt_NoInteractable_ReturnsEmpty()
        {
            Assert.AreEqual("", InteractionFocus.ResolveVerb(null));
        }

        [Test]
        public void Prompt_WithInteractable_ReturnsPromptText()
        {
            Assert.AreEqual("Test Prompt", InteractionFocus.ResolveVerb(new StubInteractable("Test Prompt")));
        }

        [Test]
        public void Prompt_CustomText_IsPreserved()
        {
            var stub = new StubInteractable("Press E to open chest");
            Assert.AreEqual("Press E to open chest", InteractionFocus.ResolveVerb(stub));
        }

        [Test]
        public void ResolveVerb_Null_ReturnsEmpty()
        {
            Assert.AreEqual("", InteractionFocus.ResolveVerb(null));
        }

        [Test]
        public void ResolveVerb_NullPrompt_ReturnsEmpty()
        {
            Assert.AreEqual("", InteractionFocus.ResolveVerb(new StubInteractable(prompt: null)));
        }

        [Test]
        public void ResolveName_ReturnsNameTag()
        {
            Assert.AreEqual("Health Potion", InteractionFocus.ResolveName(new StubInteractable(name: "Health Potion")));
        }

        // ── Focus-change detection (target and in-place verb/name changes) ────

        [Test]
        public void StateChange_NullToNull_NoChange()
        {
            Assert.IsFalse(TargetChanged(null, null));
        }

        [Test]
        public void StateChange_NullToInteractable_Changed()
        {
            Assert.IsTrue(TargetChanged(null, new StubInteractable()));
        }

        [Test]
        public void StateChange_InteractableToNull_Changed()
        {
            Assert.IsTrue(TargetChanged(new StubInteractable(), null));
        }

        [Test]
        public void StateChange_SameReference_NoChange()
        {
            var stub = new StubInteractable();
            Assert.IsFalse(TargetChanged(stub, stub));
        }

        [Test]
        public void StateChange_DifferentReferences_Changed()
        {
            Assert.IsTrue(TargetChanged(new StubInteractable("A"), new StubInteractable("B")));
        }

        [Test]
        public void FocusChange_SameTargetVerbChanged_Changed()
        {
            // e.g. a door's lock prompt changing after it is unlocked while focused
            var stub = new StubInteractable();
            Assert.IsTrue(InteractionFocus.HasFocusChanged(stub, "", "Spider", false, stub, "Loot", "Spider", false));
        }

        [Test]
        public void FocusChange_SameTargetNameChanged_Changed()
        {
            var stub = new StubInteractable();
            Assert.IsTrue(InteractionFocus.HasFocusChanged(stub, "Pick Up", "A", false, stub, "Pick Up", "B", false));
        }

        [Test]
        public void FocusChange_SameTargetSameText_NoChange()
        {
            var stub = new StubInteractable();
            Assert.IsFalse(InteractionFocus.HasFocusChanged(stub, "Pick Up", "Potion", false, stub, "Pick Up", "Potion", false));
        }

        [Test]
        public void FocusChange_SameTargetIllegalChanged_Changed()
        {
            // e.g. the owner of a focused object dies → prompt and outline turn back to normal
            var stub = new StubInteractable();
            Assert.IsTrue(InteractionFocus.HasFocusChanged(stub, "Steal", "Potion", true, stub, "Steal", "Potion", false));
        }

        [Test]
        public void FocusChange_SameTargetSameIllegal_NoChange()
        {
            var stub = new StubInteractable();
            Assert.IsFalse(InteractionFocus.HasFocusChanged(stub, "Open", "Chest", true, stub, "Open", "Chest", true));
        }

        // ── Liveness ───────────────────────────────────────────────────────────

        [Test]
        public void IsAlive_Null_False()
        {
            Assert.IsFalse(InteractionFocus.IsAlive(null));
        }

        [Test]
        public void IsAlive_PlainObject_True()
        {
            Assert.IsTrue(InteractionFocus.IsAlive(new StubInteractable()));
        }

        [Test]
        public void IsAlive_DestroyedComponent_False()
        {
            // Unity fake-null: the interface ref stays non-null after Destroy — the double-pickup guard relies on this.
            var go = new GameObject("IsAliveTest");
            IInteractable pickup = go.AddComponent<ItemPickup>();
            Assert.IsTrue(InteractionFocus.IsAlive(pickup));
            Object.DestroyImmediate(go);
            Assert.IsNotNull(pickup);
            Assert.IsFalse(InteractionFocus.IsAlive(pickup));
        }

        // ── Outline layer bits ─────────────────────────────────────────────────

        [Test]
        public void WithLayer_SetsBit_PreservesOthers()
        {
            Assert.AreEqual(0b101u | 0b010u, InteractionFocus.WithLayer(0b101u, 0b010u));
        }

        [Test]
        public void WithoutLayer_ClearsBit_PreservesOthers()
        {
            Assert.AreEqual(0b101u, InteractionFocus.WithoutLayer(0b111u, 0b010u));
        }

        [Test]
        public void WithLayer_Idempotent()
        {
            uint once = InteractionFocus.WithLayer(1u, 2u);
            Assert.AreEqual(once, InteractionFocus.WithLayer(once, 2u));
        }

        // ── Outline color ──────────────────────────────────────────────────────

        [Test]
        public void OutlineColor_NoOverride_UsesDefault()
        {
            Assert.AreEqual(Color.yellow, InteractionFocus.ResolveOutlineColor(false, Color.red, Color.yellow));
        }

        [Test]
        public void OutlineColor_Override_UsesOverride()
        {
            Assert.AreEqual(Color.red, InteractionFocus.ResolveOutlineColor(true, Color.red, Color.yellow));
        }

        // ── CanInteract gate tests ─────────────────────────────────────────────

        [Test]
        public void Gate_CanInteractTrue_IsPromptCandidate()
        {
            Assert.IsTrue(IsPromptCandidate(new StubInteractable("Talk", canInteract: true)));
        }

        [Test]
        public void Gate_CanInteractFalse_IsNotPromptCandidate()
        {
            Assert.IsFalse(IsPromptCandidate(new StubInteractable("Talk", canInteract: false)));
        }

        [Test]
        public void Gate_NullCandidate_IsNotPromptCandidate()
        {
            Assert.IsFalse(IsPromptCandidate(null));
        }

        [Test]
        public void Gate_DefaultStub_IsInteractable()
        {
            // Regression guard: existing stubs must default to interactable so prior tests stay valid.
            Assert.IsTrue(new StubInteractable().CanInteract);
        }

        // ── IInteractable interface contract ──────────────────────────────────

        [Test]
        public void Interact_DoesNotThrow()
        {
            var stub = new StubInteractable();
            Assert.DoesNotThrow(() => stub.Interact());
        }

        [Test]
        public void OutlineColor_Illegal_WinsOverOverride()
        {
            Assert.AreEqual(Color.magenta, InteractionFocus.ResolveOutlineColor(true, Color.magenta, true, Color.red, Color.yellow));
        }

        [Test]
        public void OutlineColor_Legal_FallsBackToOverrideRule()
        {
            Assert.AreEqual(Color.red, InteractionFocus.ResolveOutlineColor(false, Color.magenta, true, Color.red, Color.yellow));
            Assert.AreEqual(Color.yellow, InteractionFocus.ResolveOutlineColor(false, Color.magenta, false, Color.red, Color.yellow));
        }
    }
}
