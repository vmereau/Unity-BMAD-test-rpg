using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Single source of truth for player action gating and state.
    /// Exposes: IsAirborne, IsBlocking, IsAttacking, IsDodging, IsBusy.
    /// All Can-do queries (CanAttack, CanBlock, CanDodge, CanJump, CanMove) live here.
    /// Animation side-effects are delegated to PlayerAnimator — this class never touches the Animator directly.
    /// State is written by PlayerCombat and DodgeController; state is read by any system needing action gates.
    /// Story 2.6: Initial implementation.
    /// Story 2.6 (refactor): Animator side-effects moved here from PlayerCombat.
    /// Refactor: Animator calls further delegated to PlayerAnimator; moved to Game.Player namespace.
    /// Attach to the Player prefab root alongside PlayerCombat, DodgeController, StaminaSystem, CharacterController.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerAnimator))]
    public class PlayerStateManager : MonoBehaviour
    {
        private const string TAG = "[Player]";

        private CharacterController _characterController;
        private PlayerAnimator _playerAnimator;

        // Computed from engine state — no setter needed
        public bool IsAirborne => _characterController != null && !_characterController.isGrounded;

        /// <summary>True when the player cannot perform any action (cursor unlocked).</summary>
        public bool IsBusy => !CursorManager.IsLocked;

        // Written by PlayerCombat via SetBlocking / SetAttacking / SetDodging
        public bool IsBlocking { get; private set; }
        public bool IsAttacking { get; private set; }
        public bool IsDodging { get; private set; }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
            {
                GameLog.Error(TAG, "CharacterController not found on Player — PlayerStateManager disabled");
                enabled = false;
                return;
            }

            _playerAnimator = GetComponent<PlayerAnimator>();
            if (_playerAnimator == null)
            {
                GameLog.Error(TAG, "PlayerAnimator not found on Player — PlayerStateManager disabled");
                enabled = false;
                return;
            }
        }

        // ── State setters (called by PlayerCombat / DodgeController) ─────────

        /// <summary>Sets blocking state and drives the IsBlocking animator bool via PlayerAnimator.</summary>
        public void SetBlocking(bool value)
        {
            IsBlocking = value;
            _playerAnimator.SetBlocking(value);
        }

        /// <summary>
        /// Sets attacking state. When transitioning to true, optionally fires an animator trigger via PlayerAnimator.
        /// Pass the precomputed trigger hash to play the animation; pass 0 to update state only.
        /// </summary>
        public void SetAttacking(bool value, int triggerHash = 0)
        {
            IsAttacking = value;
            if (value && triggerHash != 0)
                _playerAnimator.PlayAttack(triggerHash);
        }

        /// <summary>Sets dodging state and fires the dodge animator trigger via PlayerAnimator.</summary>
        public void SetDodging(bool value, bool isBackwardRoll = false)
        {
            IsDodging = value;
            if (value)
                _playerAnimator.PlayDodge(isBackwardRoll);
        }

        // ── Can-do queries ────────────────────────────────────────────────────

        /// <summary>True when the player is allowed to start an attack.</summary>
        public bool CanAttack() => !IsBusy && !IsAirborne && !IsBlocking && !IsDodging;

        /// <summary>
        /// True when the player is allowed to raise a block.
        /// Note: blocking is intentionally permitted during an attack combo (block-cancel mechanic).
        /// OnBlockStarted in PlayerCombat resets combo state when a block is raised.
        /// </summary>
        public bool CanBlock() => !IsBusy && !IsAirborne && !IsDodging;

        /// <summary>True when the player is allowed to dodge (state gates only; stamina not checked here).</summary>
        public bool CanDodge() => !IsBusy && !IsAirborne && !IsBlocking && !IsDodging;

        /// <summary>True when the player is allowed to jump (state gates only; isGrounded not checked here).</summary>
        public bool CanJump() => !IsBusy && !IsAirborne && !IsDodging && !IsBlocking;

        /// <summary>True when the player is allowed to move.</summary>
        public bool CanMove() => !IsBusy;
    }
}
