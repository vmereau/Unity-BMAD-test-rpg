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
        private float _upperBodyWeight;

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
            // Every frame: an equip change or a revive resets all Animator parameters. Off during a stance reaction, or
            // the held bool would retake the Base AnyState right after the reaction's blend and hide the clip.
            _humanoidBridge.SetSwimming(_stateManager.IsSwimming && !_stateManager.IsIncapacitated);

            // UpperBody layer: combat carry pose / block / aim / draw-sheathe over the legs; 0 while the Base Layer plays an
            // attack, dodge, jump, reaction or death full-body. Read from the Animator's state tags (same rule as NPCs).
            float target = UpperBodyWeight.Target(_humanoidBridge.IsBaseFullBody, _humanoidBridge.IsUpperBodyAction,
                _stateManager.IsInCombat);
            _upperBodyWeight = UpperBodyWeight.Step(_upperBodyWeight, target, Time.deltaTime, _config.upperBodyBlendTime);
            _humanoidBridge.SetUpperBodyWeight(_upperBodyWeight);
        }

        public void SetBlocking(bool value) => _humanoidBridge.SetBlocking(value);
        public void PlayAttack(int triggerHash) => _humanoidBridge.PlayAttack(triggerHash);
        public void ResetAttackTriggers() => _humanoidBridge.ResetAttackTriggers();
        public void PlayDodge(bool isBackwardRoll = false) => _humanoidBridge.PlayDodge(isBackwardRoll);
        public void SetInCombat(bool value) => _humanoidBridge.SetInCombat(value);
        public void PlayWeaponSwitch(bool draw, float speed) => _humanoidBridge.PlayWeaponSwitch(draw, speed);
        public void ResetWeaponSwitchTriggers() => _humanoidBridge.ResetWeaponSwitchTriggers();
        public void SetSneaking(bool value) => _humanoidBridge.SetSneaking(value);
        public void SetAiming(bool value) => _humanoidBridge.SetAiming(value);
        public void PlayBowFire() => _humanoidBridge.PlayBowFire();
        public void ResetBowFireTrigger() => _humanoidBridge.ResetBowFireTrigger();

        /// <summary>Leaves sneak through the <c>sneak to sprint</c> clip instead of <c>sneak to stand</c>.</summary>
        public void PlaySneakToSprint()
        {
            _humanoidBridge.SetSneaking(false);
            _humanoidBridge.TriggerSneakToSprint();
        }
        public void PlayDeath() => _humanoidBridge.TriggerDeath();
        public void PlayStagger(int variant) => _humanoidBridge.PlayStagger(variant);
        public void PlayKnockDown() => _humanoidBridge.PlayKnockDown();
        public void ClearStanceReaction() => _humanoidBridge.ResetStanceTriggers();
        /// <summary>Knocked out (held knocked down clip) / gets up (getting up clip) — spec-unconscious-knockout.</summary>
        public void SetUnconscious(bool down) => _humanoidBridge.SetUnconscious(down);
        public void ResetAfterRevive()
        {
            _humanoidBridge.ResetToDefaultState(); // Rebind restores the controller's default weights (UpperBody 0)
            _upperBodyWeight = 0f;
        }
    }
}
