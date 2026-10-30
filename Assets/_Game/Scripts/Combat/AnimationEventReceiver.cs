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

        // The synced FullBody layer replays the Attack layer's clip: forward each clip event once per frame.
        // Handlers take AnimationEvent (Unity passes it to any event, whatever the authored parameter) for the key.
        private readonly ClipEventDeduper _deduper = new();

        private void Awake()
        {
            if (_combat == null)
                GameLog.Warn(TAG, "PlayerCombat not wired — animation events will be no-ops");
        }

        // Called from attack animation clips at the frame the combo window opens
        public void ComboWindowOpen(AnimationEvent e)
        {
            if (Forward(ClipEventDeduper.ClipEvent.ComboWindowOpen, e)) _combat.OnComboWindowOpen();
        }

        // Called from attack animation clips at the frame the combo window closes
        public void ComboWindowClose(AnimationEvent e)
        {
            if (Forward(ClipEventDeduper.ClipEvent.ComboWindowClose, e)) _combat.OnComboWindowClose();
        }

        // Called from attack animation clips at the frame the hit window opens (Story 7.11)
        public void HitboxEnable(AnimationEvent e)
        {
            if (Forward(ClipEventDeduper.ClipEvent.HitboxEnable, e)) _combat.OnHitboxEnable();
        }

        // Called from attack animation clips at the frame the hit window closes (Story 7.11)
        public void HitboxDisable(AnimationEvent e)
        {
            if (Forward(ClipEventDeduper.ClipEvent.HitboxDisable, e)) _combat.OnHitboxDisable();
        }

        // Called by SMB_AttackState.OnStateEnter — animator confirmed this attack state started
        public void NotifyAttackEntered(int attackIndex) => _combat?.OnAttackStateEntered(attackIndex);

        // Called by SMB_AttackState.OnStateExit — animator confirmed this attack state exited
        // (only when NOT transitioning to another attack state)
        public void NotifyAttackExited() => _combat?.OnAttackStateExited();

        // Called from draw / sheathe clips at the frame the weapon changes hand / sheath
        public void WeaponSwap(AnimationEvent e)
        {
            if (Forward(ClipEventDeduper.ClipEvent.WeaponSwap, e)) _combat.OnWeaponSwapEvent();
        }

        // Called by SMB_WeaponSwitchState.OnStateExit — draw (true) or sheathe (false) state exited
        public void NotifyWeaponSwitchExited(bool drawState) => _combat?.OnWeaponSwitchStateExited(drawState);

        // Called by SMB_BowAimState.OnStateEnter — the aim pose is reached, the arrow is nocked
        public void NotifyBowAimEntered() => _combat?.OnBowAimEntered();

        // SMB notifications are never deduplicated: the FullBody layer carries no SMBs.
        private bool Forward(ClipEventDeduper.ClipEvent clipEvent, AnimationEvent e)
        {
            if (_combat == null) return false;
            AnimationClip clip = e != null ? e.animatorClipInfo.clip : null;
            int key = ClipEventDeduper.ClipKey(clip != null ? clip.GetEntityId().GetHashCode() : 0, e != null ? e.time : 0f);
            return _deduper.ShouldForward(clipEvent, key, Time.frameCount);
        }
    }
}
