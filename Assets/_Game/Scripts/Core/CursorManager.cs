using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Single point of control for all cursor state changes.
    /// Use Lock() / Unlock() instead of setting Cursor.lockState or Cursor.visible directly.
    /// </summary>
    public static class CursorManager
    {
        /// <summary>Locks and hides the cursor (gameplay mode).</summary>
        public static void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Unlocks and shows the cursor (UI / menu mode).</summary>
        public static void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>True when the cursor is currently locked.</summary>
        public static bool IsLocked => Cursor.lockState == CursorLockMode.Locked;
    }
}
