using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Static hand-off from the start menu scene to <c>Core</c>: the menu requests a save slot before loading Core,
    /// <see cref="SceneLoader"/> skips its startup region when one is pending (read in Awake), and
    /// <see cref="SaveSystem"/> takes the request in Start and loads it. No request = the normal New Game boot.
    /// </summary>
    public static class GameSession
    {
        private static string s_pendingLoadSlot;

        public static bool HasPendingLoad => !string.IsNullOrEmpty(s_pendingLoadSlot);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset() => s_pendingLoadSlot = null;

        /// <summary>Asks the next Core boot to load <paramref name="slotId"/> instead of starting a new game.</summary>
        public static void RequestLoad(string slotId) => s_pendingLoadSlot = string.IsNullOrEmpty(slotId) ? null : slotId;

        /// <summary>Returns and clears the pending slot. False when none is pending.</summary>
        public static bool TryTakePendingLoad(out string slotId)
        {
            slotId = s_pendingLoadSlot;
            s_pendingLoadSlot = null;
            return !string.IsNullOrEmpty(slotId);
        }
    }
}
