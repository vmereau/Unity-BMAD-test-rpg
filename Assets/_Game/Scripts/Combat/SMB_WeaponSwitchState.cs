using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// StateMachineBehaviour on the Draw_State / Sheathe_State AnimatorStates (Humanoid_Template, Attack layer).
    /// The WeaponSwap clip event times the weapon swap; this SMB's guaranteed OnStateExit completes it when the clip
    /// was interrupted (dodge, jump) before the event fired. Silent no-op on NPCs (no AnimationEventReceiver).
    /// </summary>
    public class SMB_WeaponSwitchState : StateMachineBehaviour
    {
        [SerializeField] private bool drawState; // true on Draw_State, false on Sheathe_State — set per-state in the Animator

        private AnimationEventReceiver _receiver;
        private bool _resolved;

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            AnimationEventReceiver receiver = GetReceiver(animator);
            if (receiver != null) receiver.NotifyWeaponSwitchExited(drawState);
        }

        private AnimationEventReceiver GetReceiver(Animator animator)
        {
            // Resolved once per SMB instance — NPCs share Humanoid_Template and have no receiver.
            if (!_resolved)
            {
                _receiver = animator.GetComponent<AnimationEventReceiver>();
                _resolved = true;
            }
            return _receiver;
        }
    }
}
