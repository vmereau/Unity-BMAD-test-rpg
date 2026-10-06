using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// StateMachineBehaviour attached to each attack AnimatorState (Attack_1/2/3).
    /// Provides guaranteed enter/exit callbacks that complement animation events:
    ///   - Animation events handle TIMING (hit window open/close, combo window).
    ///   - This SMB handles STATE TRANSITIONS regardless of interrupt/crossfade.
    ///
    /// OnStateEnter: guarantees hitbox starts disabled at state entry.
    /// OnStateExit:  always fires NotifyAttackExited. PlayerCombat decides whether to
    ///               clean up based on _nextAttackQueued — no animator state queries needed.
    /// </summary>
    public class SMB_AttackState : StateMachineBehaviour
    {
        [SerializeField] private int attackIndex; // 1, 2, or 3 — set per-state in Animator Inspector

        private AnimationEventReceiver _receiver;
        private bool _resolved;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            AnimationEventReceiver receiver = GetReceiver(animator);
            if (receiver != null) receiver.NotifyAttackEntered(attackIndex);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            AnimationEventReceiver receiver = GetReceiver(animator);
            if (receiver != null) receiver.NotifyAttackExited();
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
