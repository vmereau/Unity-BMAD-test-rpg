using Game.Animations;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// StateMachineBehaviour attached to the Death state in EnemyBase.controller.
    /// Calls EntityAnimationBridge.EnableRagdoll() when the Death state exits (Death → Dead transition).
    /// OnStateExit fires exactly once at true animation end, not during crossfade.
    ///
    /// The Animator lives on a child GO (e.g. CreatureVisual); EntityAnimationBridge lives on the root.
    /// GetComponentInParent walks up from the Animator's GO to find EntityAnimationBridge on the root.
    /// </summary>
    public class SMB_DeathState : StateMachineBehaviour
    {
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.GetComponentInParent<EntityAnimationBridge>()?.EnableRagdoll();
        }
    }
}
