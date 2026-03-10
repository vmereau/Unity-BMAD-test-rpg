using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Centralizes read-only player state for combat gating and drives the resulting animator side-effects.
    /// Exposes: IsAirborne (engine-derived), IsBlocking, IsAttacking, IsDodging (stub for Story 2.7).
    /// SetBlocking drives the IsBlocking animator bool.
    /// SetAttacking optionally fires an animator trigger when transitioning to true.
    /// State is written by PlayerCombat; state is read by any system needing combat gate info.
    /// Story 2.6: Initial implementation.
    /// Story 2.6 (refactor): Animator side-effects moved here from PlayerCombat.
    /// Attach to the Player prefab root alongside PlayerCombat, StaminaSystem, CharacterController.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerStateManager : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");
        private static readonly int IsDodgingHash = Animator.StringToHash("IsDodging");
        private static readonly int IsDodgingBackwards = Animator.StringToHash("IsDodgingBackwards");

        private CharacterController _characterController;
        private Animator _animator;

        // Computed from engine state — no setter needed
        public bool IsAirborne => _characterController != null && !_characterController.isGrounded;

        // Written by PlayerCombat via SetBlocking / SetAttacking / SetDodging
        public bool IsBlocking { get; private set; }
        public bool IsAttacking { get; private set; }
        public bool IsDodging { get; private set; } // Reserved for Story 2.7

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
            {
                GameLog.Error(TAG, "CharacterController not found on Player — PlayerStateManager disabled");
                enabled = false;
                return;
            }

            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                GameLog.Error(TAG, "Animator not found on Player — PlayerStateManager disabled");
                enabled = false;
                return;
            }
        }

        /// <summary>Sets blocking state and drives the IsBlocking animator bool.</summary>
        public void SetBlocking(bool value)
        {
            IsBlocking = value;
            if (_animator != null) _animator.SetBool(IsBlockingHash, value);
        }

        /// <summary>
        /// Sets attacking state. When transitioning to true, optionally fires an animator trigger.
        /// Pass the precomputed trigger hash to fire the animation; pass 0 (default) to update state only.
        /// </summary>
        public void SetAttacking(bool value, int triggerHash = 0)
        {
            IsAttacking = value;
            if (value && triggerHash != 0 && _animator != null)
                _animator.SetTrigger(triggerHash);
        }

        /// <summary>Sets dodging state. Reserved for Story 2.7.</summary>
        public void SetDodging(bool value, bool isBackwardRoll = false)
        {
            IsDodging = value;
            if (_animator != null && value) {
                _animator.SetTrigger(isBackwardRoll ? IsDodgingBackwards : IsDodgingHash);
            }
        }

        // ── Can-do queries ────────────────────────────────────────────────

        /// <summary>True when the player is allowed to start an attack.</summary>
        public bool CanAttack() => !IsAirborne && !IsBlocking && !IsDodging;

        /// <summary>
        /// True when the player is allowed to raise a block.
        /// Note: blocking is intentionally permitted during an attack combo (block-cancel mechanic).
        /// OnBlockStarted in PlayerCombat resets combo state when a block is raised.
        /// </summary>
        public bool CanBlock() => !IsAirborne && !IsDodging;

        /// <summary>True when the player is allowed to dodge (state gates only; stamina not checked here).</summary>
        public bool CanDodge() => !IsAirborne && !IsBlocking && !IsDodging;

        /// <summary>True when the player is allowed to jump (state gates only; isGrounded not checked here).</summary>
        public bool CanJump() => !IsAirborne && !IsDodging;
    }
}
