using Game.Animations;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Player-specific driver for the HumanoidAnimationBridge.
    /// Reads CharacterController velocity and PlayerStateManager state to drive the humanoid animation parameters.
    /// PlayerStateManager calls the public combat methods — no other script should call the Animator directly for the player.
    /// </summary>
    [RequireComponent(typeof(HumanoidAnimationBridge))]
    public class PlayerAnimationDriver : MonoBehaviour
    {
        private const string TAG = "[Player]";
        private const float RISING_VELOCITY_THRESHOLD = 0.1f;

        [SerializeField] private PlayerConfigSO _config;

        private HumanoidAnimationBridge _humanoidBridge;
        private CharacterController _characterController;
        private PlayerStateManager _stateManager;

        private void Awake()
        {
            _humanoidBridge = GetComponent<HumanoidAnimationBridge>();
            _characterController = GetComponent<CharacterController>();
            _stateManager = GetComponent<PlayerStateManager>();

            if (_characterController == null)
            {
                GameLog.Error(TAG, "CharacterController not found — PlayerAnimationDriver cannot read speed.");
                enabled = false;
                return;
            }
            if (_stateManager == null)
            {
                GameLog.Error(TAG, "PlayerStateManager not found — PlayerAnimationDriver cannot read IsAirborne.");
                enabled = false;
                return;
            }
            if (_config == null)
            {
                GameLog.Error(TAG, "PlayerConfigSO not assigned — PlayerAnimationDriver disabled.");
                enabled = false;
                return;
            }
            if (_config.runSpeed <= 0f)
            {
                GameLog.Error(TAG, "PlayerConfigSO.runSpeed must be > 0 — PlayerAnimationDriver disabled.");
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            // 2D blend tree driven by local-space velocity.
            Vector3 worldHoriz = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z);
            Vector3 localVelocity = transform.InverseTransformDirection(worldHoriz);

            float normX = Mathf.Clamp(localVelocity.x / _config.runSpeed, -1f, 1f);
            float normZ = Mathf.Clamp(localVelocity.z / _config.runSpeed, -1f, 1f);

            _humanoidBridge.SetMovement(normX, normZ);
            _humanoidBridge.SetGrounded(!_stateManager.IsAirborne);
            _humanoidBridge.SetRising(_characterController.velocity.y > RISING_VELOCITY_THRESHOLD);
        }

        public void SetBlocking(bool value) => _humanoidBridge.SetBlocking(value);
        public void PlayAttack(int triggerHash) => _humanoidBridge.PlayAttack(triggerHash);
        public void PlayDodge(bool isBackwardRoll = false) => _humanoidBridge.PlayDodge(isBackwardRoll);
        public void SetInCombat(bool value) => _humanoidBridge.SetInCombat(value);
    }
}
