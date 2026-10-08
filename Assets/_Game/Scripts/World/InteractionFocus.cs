using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Pure, stateless helpers behind <see cref="InteractionSystem"/> focus tracking, highlight
    /// layer toggling and prompt resolution. Kept free of Unity lifecycle so EditMode tests can
    /// call the production logic directly.
    /// </summary>
    public static class InteractionFocus
    {
        /// <summary>
        /// True when the interactable still exists. Destroyed components compare equal to null
        /// through Unity's overloaded operator, but not through the interface reference.
        /// </summary>
        public static bool IsAlive(IInteractable interactable) =>
            interactable is Object unityObject ? unityObject != null : interactable != null;

        /// <summary>True when the focused target changed, or its verb / name / legality changed in place.</summary>
        public static bool HasFocusChanged(IInteractable previous, string previousVerb, string previousName, bool previousIllegal,
                                           IInteractable next, string nextVerb, string nextName, bool nextIllegal) =>
            !ReferenceEquals(previous, next) || previousVerb != nextVerb || previousName != nextName ||
            previousIllegal != nextIllegal;

        public static string ResolveVerb(IInteractable interactable) =>
            IsAlive(interactable) ? interactable.InteractPrompt ?? "" : "";

        public static string ResolveName(IInteractable interactable) =>
            IsAlive(interactable) ? interactable.NameTag ?? "" : "";

        public static uint WithLayer(uint mask, uint bits) => mask | bits;

        public static uint WithoutLayer(uint mask, uint bits) => mask & ~bits;

        public static Color ResolveOutlineColor(bool hasOverride, Color overrideColor, Color defaultColor) =>
            hasOverride ? overrideColor : defaultColor;

        /// <summary>An illegal (theft) interaction always shows <paramref name="illegalColor"/>, even over a per-object override.</summary>
        public static Color ResolveOutlineColor(bool illegal, Color illegalColor, bool hasOverride, Color overrideColor,
                                                Color defaultColor) =>
            illegal ? illegalColor : ResolveOutlineColor(hasOverride, overrideColor, defaultColor);

        public static Color SelectCrosshairColor(bool hasTarget, Color defaultColor, Color highlightColor) =>
            hasTarget ? highlightColor : defaultColor;
    }
}
