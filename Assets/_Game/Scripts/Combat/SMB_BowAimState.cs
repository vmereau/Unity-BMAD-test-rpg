using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// StateMachineBehaviour on BowAim_State (Humanoid_Template, Attack layer). Entering the aim pose means the arrow is
    /// nocked: PlayerCombat allows the next shot. Silent no-op on NPCs (no AnimationEventReceiver).
    /// </summary>
    public class SMB_BowAimState : StateMachineBehaviour
    {
        private AnimationEventReceiver _receiver;
        private bool _resolved;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            AnimationEventReceiver receiver = GetReceiver(animator);
            if (receiver != null) receiver.NotifyBowAimEntered();
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
