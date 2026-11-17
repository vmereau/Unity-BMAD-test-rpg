using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// StateMachineBehaviour on BowAim_State (Humanoid_Template, UpperBody layer). Entering the aim pose means the arrow is
    /// nocked: the <see cref="IBowNockListener"/> on the Animator GO (player: AnimationEventReceiver → PlayerCombat; NPC:
    /// EntityAnimationEventReceiver → EntityArcher) allows the next shot. Silent no-op without a listener.
    /// </summary>
    public class SMB_BowAimState : StateMachineBehaviour
    {
        private IBowNockListener _listener;
        private bool _resolved;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            IBowNockListener listener = GetListener(animator);
            // Unity null: a destroyed receiver (body swap) is not C# null.
            if (listener is Object obj && obj != null) listener.NotifyBowAimEntered();
        }

        private IBowNockListener GetListener(Animator animator)
        {
            // Resolved once per SMB instance (one per animator).
            if (!_resolved)
            {
                _listener = animator.GetComponent<IBowNockListener>();
                _resolved = true;
            }
            return _listener;
        }
    }
}
