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

        /// <summary>
        /// Length of the occlusion probe from the camera to a sphere-cast hit point, stopped
        /// <paramref name="backoff"/> short so the surface the target rests on doesn't block it.
        /// 0 = skip the probe: a hit at distance 0 means the cast started inside the target.
        /// </summary>
        public static float OcclusionProbeLength(float hitDistance, float distanceToPoint, float backoff) =>
            hitDistance <= 0f ? 0f : Mathf.Max(0f, distanceToPoint - backoff);

        /// <summary>
        /// Distance along the camera ray to start the interaction scan: the point closest to
        /// <paramref name="reachOrigin"/> (the player's head), never behind the camera. The orbit camera sits
        /// metres behind the player, so measuring the reach from the camera made it depend on pitch and arm length
        /// (looking down at the floor put items out of range).
        /// </summary>
        public static float ReachStartDistance(Vector3 rayOrigin, Vector3 rayDirection, Vector3 reachOrigin) =>
            Mathf.Max(0f, Vector3.Dot(reachOrigin - rayOrigin, rayDirection.normalized));

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
