using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Moves the player forward during a melee swing (spec-full-body-attacks-root-motion). Attacks play full-body and
    /// <see cref="PlayerStateManager.CanMove"/> is false while attacking, so this step is the only translation of a swing:
    /// the Mixamo clips are in place (no usable root motion). <c>PlayerCombat</c> calls <see cref="Begin"/> on every
    /// attack-state enter; the step stops on its own when the attack ends, is cancelled, or a dodge takes over.
    /// Runs before <c>PlayerController</c> so its gravity <c>Move</c> stays the frame's last one (isGrounded / velocity).
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerAttackStep : MonoBehaviour
    {
        private const string TAG = "[Player]";
        // Ledge probe: no ground within this drop below the next step position → the step stops (no lunge off a cliff).
        private const float MAX_GROUND_DROP = 0.75f;

        private CharacterController _characterController;
        private PlayerStateManager _stateManager;

        private AttackStep _step;
        private float _elapsed;
        private bool _active;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _stateManager = GetComponent<PlayerStateManager>();
            if (_stateManager == null)
            {
                GameLog.Error(TAG, "PlayerStateManager not found — PlayerAttackStep disabled");
                enabled = false;
            }
        }

        /// <summary>Starts <paramref name="step"/> now (attack state entered). Replaces a step still running (combo).</summary>
        public void Begin(AttackStep step)
        {
            _step = step;
            _elapsed = 0f;
            _active = step.distance > 0f && step.duration > 0f;
        }

        private void Update()
        {
            if (!_active) return;
            if (!_stateManager.IsAttacking || _stateManager.IsDodging || _stateManager.IsBusy || _stateManager.IsAirborne
                || !_characterController.enabled)
            {
                _active = false;
                return;
            }

            float from = _elapsed;
            _elapsed += Time.deltaTime;
            float metres = _step.DisplacementBetween(from, _elapsed);
            if (_elapsed >= _step.startTime + _step.duration) _active = false;
            if (metres <= 0f) return;

            Vector3 delta = transform.forward * metres;
            if (!HasGroundAhead(delta))
            {
                _active = false;
                return;
            }
            _characterController.Move(delta);
        }

        // Ground below the capsule's front edge after the move, within MAX_GROUND_DROP of the feet.
        private bool HasGroundAhead(Vector3 delta)
        {
            Vector3 feet = transform.position + _characterController.center
                - Vector3.up * (_characterController.height * 0.5f);
            Vector3 origin = feet + delta + transform.forward * _characterController.radius
                + Vector3.up * _characterController.stepOffset;
            return Physics.Raycast(origin, Vector3.down, _characterController.stepOffset + MAX_GROUND_DROP,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
