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
        private static string s_originId;

        public static bool HasPendingLoad => !string.IsNullOrEmpty(s_pendingLoadSlot);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            s_pendingLoadSlot = null;
            s_originId = null;
        }

        /// <summary>The origin picked at New Game, applied by the next new-game boot. Null = the default origin.</summary>
        public static void SetOrigin(string originId) => s_originId = string.IsNullOrEmpty(originId) ? null : originId;

        /// <summary>Returns and clears the picked origin id (null when none was picked).</summary>
        public static string TakeOriginId()
        {
            string id = s_originId;
            s_originId = null;
            return id;
        }

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
