using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Manages player health. Handles damage and death.
    /// On death: raises OnPlayerDied, then marks PlayerStateManager dead (blocks all actions, plays the death
    /// animation). The Player GameObject stays active — it hosts the UICanvas with the death screen.
    /// Revival happens through a save load (RestoreHealth).
    /// Attach to the Player prefab root.
    /// Story 2.9: Initial implementation.
    /// Story 7.3: Added Defense reduction via PlayerStats.Defense.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        private const string TAG = "[Combat]";

        [SerializeField] private CombatConfigSO _config;
        [SerializeField] private GameEventSO_Void _onPlayerDied;
        [SerializeField] private GameEventSO_Float _onPlayerHealthChanged;

        // Same-system reference — PlayerHealth and PlayerStats both in Scripts/Player/. No architecture violation.
        [SerializeField] private PlayerStats _playerStats;

        // Same-system reference (Game.Combat) — delegated to for block/dodge/perfect-block resolution.
        [SerializeField] private PlayerCombat _playerCombat;

        // Same-system reference — marks the player dead so every action gate closes.
        [SerializeField] private PlayerStateManager _playerStateManager;

        private int _bonusMaxHealth;

        public float CurrentHealth { get; private set; }
        /// <summary>Config base health + permanent bonuses (first-meal bonuses), which are saved.</summary>
        public float MaxHealth => _config != null ? _config.baseHealth + _bonusMaxHealth : 0f;
        /// <summary>Permanent max-health bonus on top of the config base (saved by PlayerSaveAdapter).</summary>
        public int BonusMaxHealth => _bonusMaxHealth;
        public bool IsDead { get; private set; }

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "CombatConfigSO not assigned — PlayerHealth disabled");
                enabled = false;
                return;
            }

            if (_onPlayerDied == null)
                GameLog.Warn(TAG, "OnPlayerDied event not assigned — death will not be broadcast");

            if (_playerStats == null)
                GameLog.Warn(TAG, "PlayerStats not assigned — damage will not be reduced by Defense");

            // Same-GO fallback so a missed Inspector wiring still self-heals (PlayerCombat is on the Player root).
            if (_playerCombat == null) _playerCombat = GetComponent<PlayerCombat>();
            if (_playerCombat == null)
                GameLog.Warn(TAG, "PlayerCombat not assigned and none on this GameObject — incoming hits will not check block/dodge");

            if (_playerStateManager == null) _playerStateManager = GetComponent<PlayerStateManager>();
            if (_playerStateManager == null)
                GameLog.Warn(TAG, "PlayerStateManager not assigned and none on this GameObject — death will not block input");

            CurrentHealth = _config.baseHealth;
        }

        private void Start()
        {
            // Raise after all OnEnable calls have run so subscribers (e.g. HealthBarUI) are already listening.
            _onPlayerHealthChanged?.Raise(CurrentHealth);
        }

        /// <summary>
        /// Applies damage to the player, reduced by Defense. Minimum 1 damage always gets through.
        /// Triggers death when health reaches zero. Calls are ignored if the player is already dead.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (IsDead) return;
            float defense = _playerStats != null ? _playerStats.Defense : 0f;
            float effective = Mathf.Max(1f, amount - defense);
            CurrentHealth -= effective;
            CurrentHealth = Mathf.Max(CurrentHealth, 0f);
            GameLog.Info(TAG, $"Player took {effective:F0} damage (raw {amount:F0} - def {defense:F0}) — HP: {CurrentHealth:F0}/{MaxHealth:F0}");
            _onPlayerHealthChanged?.Raise(CurrentHealth);
            if (CurrentHealth <= 0f) Die();
        }

        /// <summary>
        /// IDamageable entry point for AI attacks. Delegates block/dodge/perfect-block resolution
        /// to PlayerCombat, preserving all Defense/stamina logic. Returns NotBlocked if dead or unwired.
        /// </summary>
        public HitResult TryReceiveHit(GameObject attacker)
        {
            if (IsDead) return HitResult.NotBlocked;
            return _playerCombat != null ? _playerCombat.TryReceiveHit(attacker) : HitResult.NotBlocked;
        }

        /// <summary>
        /// Restores health by the given amount, capped at MaxHealth. Ignored if dead.
        /// Story 4.10: Added to support PotionItemSO.OnUse.
        /// </summary>
        public void Heal(float amount)
        {
            if (_config == null || IsDead) return;
            CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
            _onPlayerHealthChanged?.Raise(CurrentHealth);
            GameLog.Info(TAG, $"Player healed {amount} HP — HP: {CurrentHealth:F0}/{MaxHealth:F0}");
        }

        /// <summary>
        /// Permanently raises max health by <paramref name="amount"/> and current health by the same amount
        /// (first-meal bonus). Ignored if dead or the amount isn't positive.
        /// </summary>
        public void AddMaxHealthBonus(int amount)
        {
            if (_config == null)
            {
                GameLog.Warn(TAG, "AddMaxHealthBonus: CombatConfigSO not assigned — bonus ignored");
                return;
            }
            if (IsDead) return;
            if (amount <= 0)
            {
                GameLog.Warn(TAG, $"AddMaxHealthBonus called with non-positive amount ({amount}) — ignored");
                return;
            }
            _bonusMaxHealth += amount;
            CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
            _onPlayerHealthChanged?.Raise(CurrentHealth);
            GameLog.Info(TAG, $"Max health +{amount} — HP: {CurrentHealth:F0}/{MaxHealth:F0}");
        }

        /// <summary>
        /// Save/load: replaces the permanent max-health bonus (negative → 0). Raises nothing — call before
        /// <see cref="RestoreHealth"/>, which clamps to the new max and raises the health event.
        /// </summary>
        public void RestoreMaxHealthBonus(int bonus)
        {
            _bonusMaxHealth = Mathf.Max(0, bonus);
        }

        /// <summary>
        /// Save/load: sets health to the saved value (clamped to [1, MaxHealth]) and revives the player.
        /// Raises only the health-changed event.
        /// </summary>
        public void RestoreHealth(float health)
        {
            if (_config == null) return;
            CurrentHealth = Mathf.Clamp(health, 1f, MaxHealth);
            IsDead = false;
            _onPlayerHealthChanged?.Raise(CurrentHealth);
        }

        private void Die()
        {
            IsDead = true;
            GameLog.Info(TAG, "Player has died");

            // Raise event so the death screen / save system can react
            _onPlayerDied?.Raise(true);

            // Stay active (the UICanvas with the death screen is nested in the Player) — just block control.
            _playerStateManager?.SetDead(true);
        }

#if false // DISABLED: debug OnGUI — to be reworked
        private GUIStyle _guiStyle;

        private void OnGUI()
        {
            if (_config == null) return;
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 250, 400, 26),
                $"PlayerHP: {CurrentHealth:F0}/{MaxHealth:F0} | Dead:{IsDead}",
                _guiStyle);
        }
#endif
    }
}
