using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Fall damage: tracks the highest point since the CharacterController last touched ground and, on landing, deals
    /// <see cref="MovementStaminaRules.FallDamage"/> (PlayerConfigSO "Fall Damage") through
    /// <see cref="PlayerHealth.TakeUnmitigatedDamage"/> (ignores Defense, no stagger). Any grounded frame ends the fall —
    /// steep terrain contacts included, so sliding down a face counts as several short falls. Swimming, death and teleports
    /// (<see cref="ResetFall"/>, or a one-frame move of more than 4 m sideways or upward — a falling hitch is never one) cancel
    /// it. A landing in swimmable water or while unconscious deals nothing.
    /// Attach to the Player root. Reads in LateUpdate, after every CharacterController.Move of the frame.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerFallDamage : MonoBehaviour
    {
        private const string TAG = "[Player]";
        private const float TELEPORT_DISTANCE = 4f; // sideways or upward in one frame; a fall hitch only moves down
        private const float LOG_MIN_HEIGHT = 2f;

        [SerializeField] private PlayerConfigSO _config;
        [SerializeField] private PlayerHealth _health;

        private CharacterController _characterController;
        private PlayerStateManager _stateManager;
        private PlayerSwimming _swimming;
        private bool _falling;
        private float _apexY;
        private Vector3 _lastPosition;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _stateManager = GetComponent<PlayerStateManager>();
            if (_stateManager == null)
                GameLog.Warn(TAG, "PlayerStateManager not found — falls into water may deal damage");
            _swimming = GetComponent<PlayerSwimming>();
            if (_health == null) _health = GetComponent<PlayerHealth>();
            if (_config == null || _health == null)
            {
                GameLog.Error(TAG, "PlayerConfigSO or PlayerHealth missing — PlayerFallDamage disabled");
                enabled = false;
                return;
            }
            _lastPosition = transform.position;
        }

        private void OnEnable() => ResetFall();

        /// <summary>Drops the fall in progress (teleport, load, respawn): the next landing deals nothing.</summary>
        public void ResetFall()
        {
            _falling = false;
            _lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            Vector3 position = transform.position;
            Vector3 step = position - _lastPosition;
            _lastPosition = position;
            bool teleported = step.y > TELEPORT_DISTANCE
                || new Vector2(step.x, step.z).sqrMagnitude > TELEPORT_DISTANCE * TELEPORT_DISTANCE;

            if (teleported || _health.IsDead || _health.IsUnconscious || (_stateManager != null && _stateManager.IsSwimming))
            {
                _falling = false;
                return;
            }

            if (!_characterController.isGrounded)
            {
                if (!_falling)
                {
                    _falling = true;
                    _apexY = position.y;
                }
                else if (position.y > _apexY)
                {
                    _apexY = position.y;
                }
                return;
            }

            if (!_falling) return;
            _falling = false;
            // A fast fall can reach a shallow pool's bottom in the frame PlayerSwimming last saw the feet above the water.
            if (_swimming != null && _swimming.IsInSwimmableWater(position)) return;
            float height = _apexY - position.y;
            float damage = MovementStaminaRules.FallDamage(height, _config.fallSafeHeight, _config.fallDamagePerMetre);
            if (height >= LOG_MIN_HEIGHT) // tuning aid; skips every hop and step
                GameLog.Info(TAG, $"Landed after a {height:F1} m fall — {damage:F0} fall damage");
            if (damage > 0f) _health.TakeUnmitigatedDamage(damage);
        }
    }
}
