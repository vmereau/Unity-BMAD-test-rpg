using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// StateMachineBehaviour on AI attack states: the <c>Attack</c> state of <c>EntityBase.controller</c>
    /// and <c>Attack_1/2/3_State</c> of <c>Humanoid_Template</c>. Safety net complementing the clip's
    /// HitboxEnable/HitboxDisable events: enter/exit feed the attacker's active attack-state counter, and
    /// when the last attack state exits (normal end, GetHit interrupt, death) every hit window is closed.
    /// Counting (not exit-only) keeps a combo alive across crossfades, where Unity fires
    /// <c>OnStateEnter(next)</c> before <c>OnStateExit(previous)</c>.
    /// Also runs on the Player's humanoid attack states, which have no receiver → silent no-op.
    /// </summary>
    public class SMB_EntityAttackState : StateMachineBehaviour
    {
        private EntityAnimationEventReceiver _receiver;
        private bool _resolved;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            EntityAnimationEventReceiver receiver = GetReceiver(animator);
            if (receiver != null) receiver.NotifyAttackEntered();
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            EntityAnimationEventReceiver receiver = GetReceiver(animator);
            if (receiver != null) receiver.NotifyAttackExited();
        }

        // Resolved once per SMB instance (one per Animator) — the receiver is legitimately absent on the Player.
        private EntityAnimationEventReceiver GetReceiver(Animator animator)
        {
            if (!_resolved)
            {
                _receiver = animator.GetComponent<EntityAnimationEventReceiver>();
                _resolved = true;
            }
            return _receiver;
        }
    }
}
