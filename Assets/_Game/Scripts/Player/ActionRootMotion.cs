using Game.Animations;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Moves the player by an action clip's own root displacement (spec-action-root-motion-jab-trial, extended to the
    /// whole unarmed combo by spec-action-root-motion-unarmed-combo). Locomotion stays
    /// code-driven: this only runs between <see cref="Begin"/> and the end of the attack, for attacks whose
    /// <c>AttackStep.rootMotionScale</c> is &gt; 0 — <c>PlayerCombat</c> starts it instead of <see cref="PlayerAttackStep"/>.
    /// Implementing <c>OnAnimatorMove</c> on the Animator's GameObject makes Unity hand the root motion to this script
    /// ("Handled by Script"), so <c>Animator.applyRootMotion</c> stays off and nothing moves unless this component does it.
    /// The clip's rotation is never applied: steering and lock-on own the facing.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterController))]
    public class ActionRootMotion : MonoBehaviour
    {
        private const string TAG = "[Player]";
        // Small downward Move made after the horizontal one: CharacterController.isGrounded reflects the last Move, and
        // these run after PlayerController's gravity Move — a purely horizontal move would report "not grounded" all swing.
        private const float GROUND_STICK = 0.02f;

        private Animator _animator;
        private CharacterController _characterController;
        private PlayerStateManager _stateManager;

        private float _scale;
        private bool _active;
        // The attack state entered before the current one was moved by its clip too (combo chain, recovery restart, a
        // fresh press while a cancelled swing still blends out): an attack → attack crossfade's blended delta is then the
        // chain's own travel. False = that attack was moved by the scripted step.
        private bool _chainedFromClipDriven;
        // Whether the last attack state entered is clip-driven. Kept across Stop(): a cancelled swing keeps blending out
        // after the component stopped, and the next attack must still know what that outgoing clip is.
        private bool _lastAttackClipDriven;
        private float _restoreFeetPivot = 1f; // Animator.feetPivotActive before Begin (Unity's default is 1)

        /// <summary>True while an attack is being moved by its clip.</summary>
        public bool IsActive => _active;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _characterController = GetComponent<CharacterController>();
            _stateManager = GetComponent<PlayerStateManager>();
            if (_stateManager == null)
            {
                GameLog.Error(TAG, "PlayerStateManager not found — ActionRootMotion disabled");
                enabled = false;
            }
        }

        /// <summary>
        /// Called for every attack state entered, with its step's <c>rootMotionScale</c>. Scale &gt; 0: starts applying the
        /// clip's root displacement × <paramref name="scale"/> and returns true. Scale ≤ 0 (scripted attack) or unable to
        /// run (disabled): stops and returns false — the caller then uses the scripted step. When the attack entered before
        /// this one was clip-driven too (combo step, recovery restart), the travel carries on through the crossfade: the
        /// scale of the entering attack applies to the blend of both clips.
        /// </summary>
        public bool Begin(float scale)
        {
            _scale = scale;
            bool wasActive = _active;
            _active = isActiveAndEnabled && _animator != null && scale > 0f;
            _chainedFromClipDriven = _lastAttackClipDriven && _active;
            _lastAttackClipDriven = _active;
            if (!_active)
            {
                // Was running for the previous attack and can't run for this one: hand the feet pivot back.
                if (wasActive && _animator != null) _animator.feetPivotActive = _restoreFeetPivot;
                return false;
            }
            // Default 1 = the root pivots around the feet while states blend, which adds movement of its own during every
            // transition (measured: +0.07 m sideways and +0.06 m forward on the idle → jab blend). 0 = the clip's travel only.
            // Runtime-only property (a controller swap resets it), so it is set here and put back in Stop: the Animator
            // blends every other state exactly as before.
            if (!wasActive) _restoreFeetPivot = _animator.feetPivotActive;
            _animator.feetPivotActive = 0f;
            return true;
        }

        /// <summary>Stops applying root motion (another attack state took over, the attack ended or was cancelled).</summary>
        public void Stop()
        {
            if (!_active) return;
            _active = false;
            _chainedFromClipDriven = false;
            if (_animator != null) _animator.feetPivotActive = _restoreFeetPivot;
        }

        // Disabled mid-swing: OnAnimatorMove no longer runs, so nothing else would hand the feet pivot back.
        private void OnDisable()
        {
            Stop();
            _lastAttackClipDriven = false;
        }

        // Unity calls this after the Animator evaluated the frame. Present = root motion is never auto-applied.
        private void OnAnimatorMove()
        {
            if (!_active) return;
            if (_stateManager == null || !_stateManager.IsAttacking || _stateManager.IsDodging || _stateManager.IsBusy
                || _stateManager.IsAirborne || !_characterController.enabled)
            {
                Stop();
                return;
            }

            // Blending in from another attack state: deltaPosition is the weighted mix of both clips. Chained from a
            // clip-driven attack (jab → hook → uppercut, recovery restart) that mix is the combo's continuous travel.
            // From an attack moved by the scripted step it carries travel that was never applied — wait the blend out.
            bool inAttackBlend = _animator.IsInTransition(0)
                && _animator.GetCurrentAnimatorStateInfo(0).IsTag(UpperBodyWeight.ATTACK_TAG)
                && _animator.GetNextAnimatorStateInfo(0).IsTag(UpperBodyWeight.ATTACK_TAG);
            if (!ActionRootMotionMath.AppliesDuringBlend(inAttackBlend, _chainedFromClipDriven))
            {
                _characterController.Move(Vector3.down * GROUND_STICK); // keep isGrounded true while waiting
                return;
            }

            Vector3 delta = ActionRootMotionMath.Filter(_animator.deltaPosition, _scale);
            // No lunge off a ledge: that frame's travel is skipped and the clip keeps playing in place. Checked per frame
            // (not a one-way stop): a clip's small backward or sideways frames must not cancel the forward travel after them.
            if (delta.sqrMagnitude > 0f && PlayerAttackStep.HasGroundAhead(transform, _characterController, delta))
                _characterController.Move(delta);
            // Separate, purely vertical move: mixed into the horizontal one it would slide the capsule down any slope.
            _characterController.Move(Vector3.down * GROUND_STICK);
        }
    }
}
