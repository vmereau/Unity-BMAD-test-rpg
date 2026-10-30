using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Hidden stance meter of the player (spec-player-stance-stagger-knockdown), on the Player root. AI hits drain it
    /// (<see cref="IStanceTarget"/>, same <see cref="StanceMeter"/> rules as <c>EntityStance</c>); a regular block takes
    /// <c>blockStanceMultiplier</c> of it and never knocks down. Crossing the threshold staggers, reaching 0 knocks
    /// down: the attack / block / aim are interrupted (<see cref="PlayerCombat.InterruptForStance"/>) and
    /// <see cref="PlayerStateManager"/> locks every action and movement until the reaction ends. Health damage still
    /// applies while down; death overrides the reaction. Tuning in <see cref="CombatConfigSO"/>. Not saved —
    /// <see cref="ResetStance"/> on load.
    /// </summary>
    public class PlayerStance : MonoBehaviour, IStanceTarget
    {
        private const string TAG = "[Combat]";
        private const int STAGGER_VARIANTS = 2;

        [SerializeField] private CombatConfigSO _config;
        [Tooltip("Auto-resolved on the same GO if null.")]
        [SerializeField] private PlayerStateManager _stateManager;
        [Tooltip("Auto-resolved on the same GO if null.")]
        [SerializeField] private PlayerCombat _playerCombat;
        [Tooltip("Auto-resolved on the same GO if null.")]
        [SerializeField] private PlayerHealth _health;

        private readonly StanceMeter _meter = new();

        public float CurrentStance => _meter.Current;
        public float MaxStance => _config != null ? _config.playerMaxStance : 0f;
        public bool IsIncapacitated => _meter.IsIncapacitated;

        private StanceSettings Settings => new(_config.playerMaxStance, _config.playerStaggerThreshold,
            _config.playerStanceRegenDelay, _config.playerStanceRegenRate,
            _config.playerStaggerDuration, _config.playerKnockdownDuration);

        private void Awake()
        {
            if (_stateManager == null) _stateManager = GetComponent<PlayerStateManager>();
            if (_playerCombat == null) _playerCombat = GetComponent<PlayerCombat>();
            if (_health == null) _health = GetComponent<PlayerHealth>();
            if (_config == null || _stateManager == null)
            {
                GameLog.Error(TAG, "PlayerStance needs CombatConfigSO and PlayerStateManager — player stance disabled");
                enabled = false;
                return;
            }
            if (_playerCombat == null)
                GameLog.Warn(TAG, "PlayerCombat not found — a stance reaction will not interrupt attacks / block");
            if (_health == null)
                GameLog.Warn(TAG, "PlayerHealth not found — stance keeps running after death");
            _meter.Reset(Settings);
        }

        private void Update()
        {
            if (IsDead)
            {
                // Death overrides the reaction (SetDead already cleared the state manager's flags). Refilled so any
                // revive path starts at full stance, not only a save load.
                _meter.Reset(Settings);
                return;
            }

            if (_meter.Tick(Time.deltaTime, Settings))
            {
                _stateManager.EndStanceReaction();
                GameLog.Info(TAG, $"Player recovered — stance {CurrentStance:F0}/{MaxStance:F0}");
            }
        }

        // Only Update ends a reaction — disabled mid-reaction, the player would stay locked until a load.
        private void OnDisable()
        {
            if (!_meter.IsIncapacitated) return;
            _meter.ClearReaction();
            if (_stateManager != null) _stateManager.EndStanceReaction();
        }

        public void ApplyStanceDamage(float amount, bool canKnockDown) => Apply(amount, canKnockDown, blocked: false);

        public void ApplyBlockedStanceDamage(float amount) => Apply(amount, false, blocked: true);

        /// <summary>Load / revive: full stance, reaction dropped (the Animator snaps out of a reaction clip).</summary>
        public void ResetStance()
        {
            if (_config == null) return;
            _meter.Reset(Settings);
            if (_stateManager != null) _stateManager.EndStanceReaction(resetAnimator: true);
        }

        private bool IsDead => _health != null && _health.IsDead;

        private void Apply(float amount, bool canKnockDown, bool blocked)
        {
            if (!enabled || IsDead || amount <= 0f) return;

            if (blocked) amount = StanceMath.ApplyBlockMultiplier(amount, _config.blockStanceMultiplier);
            if (_stateManager.IsAttacking)
                amount = StanceMath.ApplyResistance(amount, _config.playerAttackStanceResistance);
            if (amount <= 0f) return; // fully absorbed

            float before = CurrentStance;
            StanceReaction reaction = _meter.Apply(amount, canKnockDown, Settings, out bool applied);
            if (!applied) return; // knocked down, or floored (no-knockdown hit at/below the threshold)
            GameLog.Info(TAG, $"Player stance {before:F0} → {CurrentStance:F0}/{MaxStance:F0} " +
                              $"(hit {amount:F0}{(blocked ? ", blocked" : "")}, knockdown {(canKnockDown ? "on" : "off")}) {reaction}");

            if (reaction == StanceReaction.None) return;
            if (_playerCombat != null) _playerCombat.InterruptForStance();
            if (reaction == StanceReaction.Stagger)
                _stateManager.BeginStagger(Random.Range(0, STAGGER_VARIANTS));
            else
                _stateManager.BeginKnockDown();
        }
    }
}
