using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Single source of truth for player action gating and state.
    /// Exposes: IsAirborne, IsBlocking, IsAttacking, IsDodging, IsBusy, IsInCombat, IsSneaking, IsDead.
    /// All Can-do queries (CanAttack, CanBlock, CanDodge, CanJump, CanMove, CanSneak) live here.
    /// Animation side-effects are delegated to PlayerAnimator — this class never touches the Animator directly.
    /// State is written by PlayerCombat and DodgeController; state is read by any system needing action gates.
    /// Story 2.6: Initial implementation.
    /// Story 2.6 (refactor): Animator side-effects moved here from PlayerCombat.
    /// Refactor: Animator calls further delegated to PlayerAnimator; moved to Game.Player namespace.
    /// Attach to the Player prefab root alongside PlayerCombat, DodgeController, StaminaSystem, CharacterController.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerAnimationDriver))]
    public class PlayerStateManager : MonoBehaviour
    {
        private const string TAG = "[Player]";

        [Tooltip("Grace window after losing isGrounded before the player is considered airborne. " +
                 "Absorbs single-frame ungroundings on slope crests, step edges, and small ledges. " +
                 "Also enables coyote-jump.")]
        [SerializeField] private float _coyoteTime = 0.5f;

        private CharacterController _characterController;
        private PlayerAnimationDriver _playerAnimator;
        private float _lastGroundedTime;

        /// <summary>
        /// True when the player is airborne after a coyote-time grace window.
        /// Reads as false for <c>_coyoteTime</c> seconds after <see cref="CharacterController.isGrounded"/>
        /// flips to false, so slope crests and small ledges don't trigger fall logic.
        /// Active jumps bypass the window via <see cref="NotifyJumpStarted"/>.
        /// </summary>
        public bool IsAirborne
        {
            get
            {
                if (_characterController == null) return false;
                if (_characterController.isGrounded)
                {
                    _lastGroundedTime = Time.time;
                    return false;
                }
                return Time.time - _lastGroundedTime > _coyoteTime;
            }
        }

        /// <summary>
        /// Called by <c>PlayerController</c> when an active jump is initiated.
        /// Expires the coyote window so <see cref="IsAirborne"/> becomes true on the next frame,
        /// preventing the rising/fall animation from being delayed by the grace period.
        /// Coyote time is only meant to absorb passive ungroundings (slopes, ledges), not real jumps.
        /// </summary>
        public void NotifyJumpStarted()
        {
            _lastGroundedTime = float.MinValue;
            SetSneaking(false);
        }

        /// <summary>True when the player cannot perform any action (dead, or cursor unlocked).</summary>
        public bool IsBusy => IsDead || !CursorManager.IsLocked;

        /// <summary>True from death until a save is loaded. Blocks every action through IsBusy.</summary>
        public bool IsDead { get; private set; }

        // Written by PlayerCombat via SetBlocking / SetAttacking / SetDodging / SetInCombat
        public bool IsBlocking { get; private set; }
        public bool IsAttacking { get; private set; }
        public bool IsDodging { get; private set; }
        public bool IsInCombat { get; private set; }

        /// <summary>
        /// True while in sneak stance. Written by <c>PlayerSneak</c>; cleared by dodge, jump, death and sprint
        /// (<see cref="ExitSneakToSprint"/>). Attacking and blocking keep it.
        /// </summary>
        public bool IsSneaking { get; private set; }

        /// <summary>True while the player is in an active dialogue conversation.</summary>
        public bool IsInDialogue { get; private set; }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
            {
                GameLog.Error(TAG, "CharacterController not found on Player — PlayerStateManager disabled");
                enabled = false;
                return;
            }

            _playerAnimator = GetComponent<PlayerAnimationDriver>();
            if (_playerAnimator == null)
            {
                GameLog.Error(TAG, "PlayerAnimationDriver not found on Player — PlayerStateManager disabled");
                enabled = false;
                return;
            }

            _lastGroundedTime = Time.time;
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
            {
                SetSneaking(false);
                _playerAnimator.PlayDodge(isBackwardRoll);
            }
        }

        /// <summary>Sets the InCombat state and drives the IsInCombat animator bool via PlayerAnimator.</summary>
        public void SetInCombat(bool value)
        {
            IsInCombat = value;
            _playerAnimator.SetInCombat(value);
            GameLog.Info(TAG, $"Combat stance: {(value ? "DRAWN" : "sheathed")}");
        }

        /// <summary>Enters or leaves sneak stance and drives the IsSneaking animator bool. Called by PlayerSneak.</summary>
        public void SetSneaking(bool value)
        {
            if (IsSneaking == value) return;
            IsSneaking = value;
            if (_playerAnimator != null) _playerAnimator.SetSneaking(value);
            GameLog.Info(TAG, $"Sneak: {value}");
        }

        /// <summary>Leaves sneak because the player started running — plays the sneak-to-sprint clip.</summary>
        public void ExitSneakToSprint()
        {
            if (!IsSneaking) return;
            IsSneaking = false;
            if (_playerAnimator != null) _playerAnimator.PlaySneakToSprint();
            GameLog.Info(TAG, "Sneak: false (sprint)");
        }

        /// <summary>
        /// Sets the IsInDialogue state. Called by DialogueSystem on open/close. Opening a conversation stands the
        /// player up (no sneaking while talking — neutral witnesses would otherwise react mid-dialogue). Other
        /// interactions (pickups, doors, containers) keep the sneak stance.
        /// </summary>
        public void SetInDialogue(bool value)
        {
            if (value) SetSneaking(false);
            IsInDialogue = value;
            GameLog.Info(TAG, $"IsInDialogue: {value}");
        }

        /// <summary>
        /// Called by PlayerHealth on death (true) and by the save system on revive (false). Death clears action
        /// states and plays the death animation; revive resets the Animator and re-applies the combat stance.
        /// </summary>
        public void SetDead(bool value)
        {
            if (IsDead == value) return;
            IsDead = value;
            GameLog.Info(TAG, $"IsDead: {value}");
            if (value)
            {
                IsBlocking = false;
                IsAttacking = false;
                IsDodging = false;
                IsSneaking = false;
            }
            if (_playerAnimator == null) return;

            if (value)
            {
                _playerAnimator.SetBlocking(false);
                _playerAnimator.SetSneaking(false);
                _playerAnimator.PlayDeath();
            }
            else
            {
                _playerAnimator.ResetAfterRevive();
                _playerAnimator.SetInCombat(IsInCombat);
            }
        }

        // ── Can-do queries ────────────────────────────────────────────────────

        /// <summary>True when the player is allowed to start an attack.</summary>
        public bool CanAttack() => !IsBusy && !IsAirborne && !IsBlocking && !IsDodging && IsInCombat;

        /// <summary>
        /// True when the player is allowed to raise a block.
        /// Note: blocking is intentionally permitted during an attack combo (block-cancel mechanic).
        /// OnBlockStarted in PlayerCombat resets combo state when a block is raised.
        /// </summary>
        public bool CanBlock() => !IsBusy && !IsAirborne && !IsDodging && !IsAttacking && IsInCombat;

        /// <summary>True when the player is allowed to dodge (state gates only; stamina not checked here).</summary>
        public bool CanDodge() => !IsBusy && !IsAirborne && !IsBlocking && !IsDodging;

        /// <summary>True when the player is allowed to jump (state gates only; isGrounded not checked here).</summary>
        public bool CanJump() => !IsBusy && !IsAirborne && !IsDodging && !IsBlocking && !IsAttacking;

        /// <summary>True when the player may enter sneak stance. Attacking and blocking are allowed.</summary>
        public bool CanSneak() => EvaluateCanSneak(IsBusy, IsAirborne, IsDodging);

        /// <summary>Pure sneak gate (tested in PlayerStateManagerTests). Attacking / blocking are deliberately not inputs.</summary>
        public static bool EvaluateCanSneak(bool isBusy, bool isAirborne, bool isDodging) =>
            !isBusy && !isAirborne && !isDodging;

        /// <summary>True when the player is allowed to move.</summary>
        public bool CanMove() => !IsBusy;
    }
}
