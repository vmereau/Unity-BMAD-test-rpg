using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Receives Animation Events fired from attack clips and routes them to PlayerCombat.
    /// Attach to the Player root (same GameObject as the Animator).
    /// Story 7.10: combo window events.
    /// Story 7.11: hitbox enable/disable events.
    /// </summary>
    public class AnimationEventReceiver : MonoBehaviour
    {
        private const string TAG = "[AnimationEventReceiver]";

        [SerializeField] private PlayerCombat _combat;

        private void Awake()
        {
            if (_combat == null)
                GameLog.Warn(TAG, "PlayerCombat not wired — animation events will be no-ops");
        }

        // Called from attack animation clips at the frame the combo window opens
        public void ComboWindowOpen() => _combat?.OnComboWindowOpen();

        // Called from attack animation clips at the frame the combo window closes
        public void ComboWindowClose() => _combat?.OnComboWindowClose();

        // Called from attack animation clips at the frame the hit window opens (Story 7.11)
        public void HitboxEnable() => _combat?.OnHitboxEnable();

        // Called from attack animation clips at the frame the hit window closes (Story 7.11)
        public void HitboxDisable() => _combat?.OnHitboxDisable();
    }
}
