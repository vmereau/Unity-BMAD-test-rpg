using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// StateMachineBehaviour attached to the Death state in EnemyBase.controller.
    /// Calls EntityAnimator.EnableRagdoll() when the Death state exits (Death → Dead transition).
    /// OnStateExit fires exactly once at true animation end, not during crossfade.
    ///
    /// The Animator lives on a child GO (e.g. CreatureVisual); EntityAnimator lives on the root.
    /// GetComponentInParent walks up from the Animator's GO to find EntityAnimator on the root.
    ///
    /// Mirrors the SMB_AttackState pattern used in the player combat system.
    /// Enemy Creature System: Initial implementation.
    /// </summary>
    public class SMB_DeathState : StateMachineBehaviour
    {
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.GetComponentInParent<EntityAnimator>()?.EnableRagdoll();
        }
    }
}
