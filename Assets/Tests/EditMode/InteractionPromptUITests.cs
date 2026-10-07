using NUnit.Framework;
using Game.UI;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Edit-mode tests for InteractionPromptUI.ClampToScreen (pure screen-edge clamping).</summary>
    public class InteractionPromptUITests
    {
        private static readonly Vector2 ScreenSize = new Vector2(1920f, 1080f);
        private static readonly Vector2 Size = new Vector2(200f, 50f);
        private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);
        private const float Margin = 16f;

        [Test]
        public void ClampToScreen_Inside_Unchanged()
        {
            var pos = new Vector2(960f, 540f);
            Assert.AreEqual(pos, InteractionPromptUI.ClampToScreen(pos, Size, CenterPivot, ScreenSize, Margin));
        }

        [Test]
        public void ClampToScreen_OffLeft_ClampedToMargin()
        {
            Vector2 result = InteractionPromptUI.ClampToScreen(new Vector2(-50f, 540f), Size, CenterPivot, ScreenSize, Margin);
            Assert.AreEqual(Margin + Size.x * 0.5f, result.x, 1e-4f);
            Assert.AreEqual(540f, result.y, 1e-4f);
        }

        [Test]
        public void ClampToScreen_OffRightTop_ClampedToMargin()
        {
            Vector2 result = InteractionPromptUI.ClampToScreen(new Vector2(2500f, 2000f), Size, CenterPivot, ScreenSize, Margin);
            Assert.AreEqual(ScreenSize.x - Margin - Size.x * 0.5f, result.x, 1e-4f);
            Assert.AreEqual(ScreenSize.y - Margin - Size.y * 0.5f, result.y, 1e-4f);
        }

        [Test]
        public void ClampToScreen_PivotBottomCenter_UsesPivot()
        {
            // Card pivot (0.5, 0): the whole card height sits above the pivot.
            var pivot = new Vector2(0.5f, 0f);
            Vector2 top = InteractionPromptUI.ClampToScreen(new Vector2(960f, 1080f), Size, pivot, ScreenSize, Margin);
            Assert.AreEqual(ScreenSize.y - Margin - Size.y, top.y, 1e-4f);

            Vector2 bottom = InteractionPromptUI.ClampToScreen(new Vector2(960f, 0f), Size, pivot, ScreenSize, Margin);
            Assert.AreEqual(Margin, bottom.y, 1e-4f);
        }
    }
}
