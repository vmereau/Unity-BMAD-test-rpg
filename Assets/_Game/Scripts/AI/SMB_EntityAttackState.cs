using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// StateMachineBehaviour on the <c>Attack</c> state of <c>EntityBase.controller</c>.
    /// Safety net complementing the clip's HitboxEnable/HitboxDisable events: on state exit (normal
    /// end, GetHit interrupt, death) every hit window is closed via
    /// <see cref="EntityAnimationEventReceiver.NotifyAttackExited"/>.
    /// </summary>
    public class SMB_EntityAttackState : StateMachineBehaviour
    {
        private EntityAnimationEventReceiver _receiver;

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            EntityAnimationEventReceiver receiver = GetReceiver(animator);
            if (receiver != null) receiver.NotifyAttackExited();
        }

        private EntityAnimationEventReceiver GetReceiver(Animator animator)
        {
            if (_receiver == null)
                _receiver = animator.GetComponent<EntityAnimationEventReceiver>();
            return _receiver;
        }
    }
}
