using NUnit.Framework;
using Game.World;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit-mode tests for interaction system logic.
    ///
    /// NOTE ON TEST DESIGN: InteractionSystem is a MonoBehaviour and cannot be instantiated
    /// directly in edit-mode tests (no scene). The helpers below mirror the exact logic in
    /// InteractionSystem.Update() and LateUpdate(). If production logic changes, these helpers
    /// MUST be updated to match — otherwise tests will give false confidence.
    ///
    /// The state-change tests (StateChange_*) cover the _previousInteractable caching logic
    /// that prevents per-frame UI dirty-marking (InteractionSystem.cs:81).
    /// </summary>
    public class InteractionSystemTests
    {
        private class StubInteractable : IInteractable
        {
            private readonly string _prompt;
            public StubInteractable(string prompt = "Test Prompt") => _prompt = prompt;
            public string InteractPrompt => _prompt;
            public void Interact() { }
        }

        // Mirrors InteractionSystem.Update() crosshair highlight condition
        private bool ShouldHighlight(IInteractable interactable) => interactable != null;

        // Mirrors InteractionSystem crosshair color selection
        private Color SelectCrosshairColor(bool hasInteractable, Color defaultColor, Color highlightColor)
            => hasInteractable ? highlightColor : defaultColor;

        // Mirrors InteractionSystem OnGUI prompt resolution
        private string ResolvePrompt(IInteractable interactable) => interactable?.InteractPrompt ?? "";

        // Mirrors InteractionSystem._previousInteractable caching check (line 81)
        private bool HasStateChanged(IInteractable previous, IInteractable current) => previous != current;

        // ── Crosshair color tests ──────────────────────────────────────────────

        [Test]
        public void Crosshair_NoInteractable_UsesDefaultColor()
        {
            bool result = ShouldHighlight(null);
            Color color = SelectCrosshairColor(result, Color.white, Color.yellow);
            Assert.IsFalse(result);
            Assert.AreEqual(Color.white, color);
        }

        [Test]
        public void Crosshair_WithInteractable_UsesHighlightColor()
        {
            var stub = new StubInteractable();
            bool result = ShouldHighlight(stub);
            Color color = SelectCrosshairColor(result, Color.white, Color.yellow);
            Assert.IsTrue(result);
            Assert.AreEqual(Color.yellow, color);
        }

        [Test]
        public void Crosshair_CustomColors_AreRespected()
        {
            Color customDefault = Color.gray;
            Color customHighlight = Color.green;
            var stub = new StubInteractable();
            Color withTarget = SelectCrosshairColor(true, customDefault, customHighlight);
            Color withoutTarget = SelectCrosshairColor(false, customDefault, customHighlight);
            Assert.AreEqual(customHighlight, withTarget);
            Assert.AreEqual(customDefault, withoutTarget);
        }

        // ── Prompt resolution tests ───────────────────────────────────────────

        [Test]
        public void Prompt_NoInteractable_ReturnsEmpty()
        {
            string prompt = ResolvePrompt(null);
            Assert.AreEqual("", prompt);
        }

        [Test]
        public void Prompt_WithInteractable_ReturnsPromptText()
        {
            var stub = new StubInteractable("Test Prompt");
            string prompt = ResolvePrompt(stub);
            Assert.AreEqual("Test Prompt", prompt);
        }

        [Test]
        public void Prompt_CustomText_IsPreserved()
        {
            var stub = new StubInteractable("Press E to open chest");
            Assert.AreEqual("Press E to open chest", ResolvePrompt(stub));
        }

        // ── State-change caching tests (mirrors _previousInteractable guard) ──

        [Test]
        public void StateChange_NullToNull_NoChange()
        {
            Assert.IsFalse(HasStateChanged(null, null));
        }

        [Test]
        public void StateChange_NullToInteractable_Changed()
        {
            var stub = new StubInteractable();
            Assert.IsTrue(HasStateChanged(null, stub));
        }

        [Test]
        public void StateChange_InteractableToNull_Changed()
        {
            var stub = new StubInteractable();
            Assert.IsTrue(HasStateChanged(stub, null));
        }

        [Test]
        public void StateChange_SameReference_NoChange()
        {
            var stub = new StubInteractable();
            Assert.IsFalse(HasStateChanged(stub, stub));
        }

        [Test]
        public void StateChange_DifferentReferences_Changed()
        {
            var a = new StubInteractable("A");
            var b = new StubInteractable("B");
            Assert.IsTrue(HasStateChanged(a, b));
        }

        // ── IInteractable interface contract ──────────────────────────────────

        [Test]
        public void Interact_DoesNotThrow()
        {
            var stub = new StubInteractable();
            Assert.DoesNotThrow(() => stub.Interact());
        }
    }
}
