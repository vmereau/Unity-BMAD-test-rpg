using UnityEngine;

namespace Game.Animations
{
    /// <summary>
    /// Pure animator-parameter wrapper for monster entities — 1D <c>Speed</c> locomotion
    /// + Attack/GetHit/Death triggers. Mirrors <see cref="HumanoidAnimationBridge"/>.
    /// Lifecycle (ragdoll, AnimatorOverride, death-component-disable) lives on
    /// <c>MonsterAnimationDriver</c>, not here.
    /// </summary>
    public class MonsterAnimationBridge : MonoBehaviour
    {
        private static readonly int SpeedHash     = Animator.StringToHash("Speed");
        private static readonly int AttackHash    = Animator.StringToHash("Attack");
        private static readonly int GetHitHash    = Animator.StringToHash("GetHit");
        private static readonly int DeathHash     = Animator.StringToHash("Death");
        private static readonly int IsWarningHash = Animator.StringToHash("IsWarning");

        [SerializeField] private Animator _animator;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        public Animator Animator => _animator;

        public void SetMoveSpeed(float speed)
        {
            if (_animator == null) return;
            _animator.SetFloat(SpeedHash, speed);
        }

        public void TriggerAttack() => _animator?.SetTrigger(AttackHash);
        public void TriggerGetHit() => _animator?.SetTrigger(GetHitHash);
        public void TriggerDeath()  => _animator?.SetTrigger(DeathHash);

        public void SetWarning(bool active)
        {
            if (_animator == null) return;
            _animator.SetBool(IsWarningHash, active);
        }
    }
}
