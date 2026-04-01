using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// StateMachineBehaviour attached to the Death state in EnemyBase.controller.
    /// Calls EnemyAnimator.EnableRagdoll() when the Death state exits (Death → Dead transition).
    /// OnStateExit fires exactly once at true animation end, not during crossfade.
    ///
    /// The Animator lives on the CreatureVisual child GO; EnemyAnimator lives on the root.
    /// GetComponentInParent walks up from the Animator's GO to find EnemyAnimator on the root.
    ///
    /// Mirrors the SMB_AttackState pattern used in the player combat system.
    /// Enemy Creature System: Initial implementation.
    /// </summary>
    public class SMB_DeathState : StateMachineBehaviour
    {
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.GetComponentInParent<EnemyAnimator>()?.EnableRagdoll();
        }
    }
}
