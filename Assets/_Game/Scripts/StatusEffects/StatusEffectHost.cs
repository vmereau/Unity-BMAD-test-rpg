using System.Collections.Generic;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.StatusEffects
{
    /// <summary>
    /// Holds the player's active status effects (on the Player.prefab root) and pays them out from
    /// <see cref="GameClock.Now"/> — never from countdowns, so a sleep skip or a hitch is resolved in one pass on the
    /// next frame. Raises <c>_onStatusEffectsChanged</c> on apply / expire / clear / restore (HUD).
    /// Effects are cleared on death and saved by <c>PlayerSaveAdapter</c>.
    /// <see cref="_gameClock"/> lives in Core.unity: it is a scene override on the Player instance there (like
    /// <c>SleepSystem</c>); without it <see cref="Apply"/> refuses and food heals instantly.
    /// </summary>
    public class StatusEffectHost : MonoBehaviour
    {
        private const string TAG = "[StatusEffects]";

        [SerializeField] private GameClock _gameClock;
        // Cross-system call (like PotionItemSO): the effect's payout target.
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private StatusEffectCatalogSO _catalog;
        [SerializeField] private GameEventSO_Void _onStatusEffectsChanged;

        private readonly List<StatusEffectInstance> _active = new List<StatusEffectInstance>();
        private bool _warnedNoClock;

        public IReadOnlyList<StatusEffectInstance> Active => _active;
        /// <summary>Current world time, or 0 without a clock (HUD remaining time).</summary>
        public double Now => _gameClock != null ? _gameClock.Now : 0d;
        /// <summary>Health still to be restored by active heal effects (health bar preview).</summary>
        public float PendingHeal => StatusEffectMath.PendingOf(_active, StatusEffectKind.Heal);

        private void Awake()
        {
            if (_playerHealth == null) _playerHealth = GetComponent<PlayerHealth>();
            if (_playerHealth == null) GameLog.Warn(TAG, "PlayerHealth not assigned and none on this GameObject — heal effects do nothing");
            if (_catalog == null) GameLog.Warn(TAG, "StatusEffectCatalogSO not assigned — active effects are dropped on load");
            if (_onStatusEffectsChanged == null) GameLog.Warn(TAG, "_onStatusEffectsChanged not assigned — the HUD won't follow effects");
            // No clock warning here: Player.prefab has none by design (scene override in Core) — warned on first Apply.
        }

        /// <summary>
        /// Starts an effect delivering <paramref name="amount"/> over <paramref name="durationSeconds"/> game seconds
        /// from now. False (nothing applied) without a clock, while dead, or with a non-positive amount / duration.
        /// </summary>
        public bool Apply(StatusEffectSO effect, float amount, double durationSeconds)
        {
            if (effect == null) { GameLog.Warn(TAG, "Apply: effect is null"); return false; }
            if (!(amount > 0f) || !(durationSeconds > 0d) || double.IsInfinity(durationSeconds))
            {
                GameLog.Warn(TAG, $"Apply: {effect.name} with amount {amount} over {durationSeconds}s — ignored");
                return false;
            }
            if (_gameClock == null)
            {
                WarnNoClock();
                return false;
            }
            if (_playerHealth != null && _playerHealth.IsDead) return false;
            // A load replaces the active effects once the region is in — an effect applied now would be lost.
            if (SaveSystem.Instance != null && SaveSystem.Instance.IsLoading) return false;

            double now = _gameClock.Now;
            StatusEffectMath.AddInstance(_active, new StatusEffectInstance(effect, now, now + durationSeconds, amount));
            GameLog.Info(TAG, $"Applied {effect.name}: {amount} over {durationSeconds / GameTime.SECONDS_PER_MINUTE:0.#} game min ({_active.Count} active)");
            RaiseChanged();
            return true;
        }

        private void Update()
        {
            if (_active.Count == 0 || _gameClock == null) return;
            // The clock is restored before the async region reload, the effects only after it — don't pay meanwhile.
            if (SaveSystem.Instance != null && SaveSystem.Instance.IsLoading) return;
            if (_playerHealth != null && _playerHealth.IsDead)
            {
                Clear();
                return;
            }

            double now = _gameClock.Now;
            float heal = 0f;
            bool expired = false;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var instance = _active[i];
                float paidBy = StatusEffectMath.PaidByNow(instance, now);
                if (paidBy > instance.PaidAmount)
                {
                    float due = paidBy - instance.PaidAmount;
                    // Set (not +=) so float rounding can't leave a sliver owed every frame. Paid before the heal raises
                    // the health event, so the bar's pending preview is already current.
                    instance.PaidAmount = paidBy;
                    switch (instance.Effect.kind)
                    {
                        case StatusEffectKind.Heal: heal += due; break;
                        default: GameLog.Warn(TAG, $"{instance.Effect.name}: kind {instance.Effect.kind} has no payout — {due} lost"); break;
                    }
                }
                if (StatusEffectMath.IsExpired(instance, now))
                {
                    _active.RemoveAt(i);
                    expired = true;
                }
            }

            if (heal > 0f && _playerHealth != null) _playerHealth.Heal(heal);
            if (expired) RaiseChanged();
        }

        /// <summary>Removes every active effect without paying the rest.</summary>
        public void Clear()
        {
            if (_active.Count == 0) return;
            _active.Clear();
            GameLog.Info(TAG, "Status effects cleared");
            RaiseChanged();
        }

        public List<StatusEffectSaveData> Capture()
        {
            var result = new List<StatusEffectSaveData>(_active.Count);
            foreach (var instance in _active)
            {
                if (string.IsNullOrEmpty(instance.Effect.effectId))
                {
                    GameLog.Warn(TAG, $"{instance.Effect.name} has no effectId — not saved");
                    continue;
                }
                result.Add(new StatusEffectSaveData
                {
                    effectId = instance.Effect.effectId,
                    appliedAt = instance.AppliedAt,
                    expiresAt = instance.ExpiresAt,
                    totalAmount = instance.TotalAmount,
                    paidAmount = instance.PaidAmount
                });
            }
            return result;
        }

        /// <summary>
        /// Save/load: replaces the active effects with the saved ones (no stacking rules, no apply side effects).
        /// Unknown IDs and invalid entries are skipped. They catch up from the restored clock on the next frame; an entry
        /// stamped after the restored time (clock reset to the start) is shifted back so it resumes instead of stalling.
        /// </summary>
        public void Restore(List<StatusEffectSaveData> saved)
        {
            _active.Clear();
            if (saved != null && saved.Count > 0 && _gameClock == null)
            {
                WarnNoClock();
                GameLog.Warn(TAG, $"{saved.Count} saved status effect(s) dropped — no GameClock");
                saved = null;
            }
            if (saved != null)
            {
                foreach (var s in saved)
                {
                    if (s == null) continue;
                    var effect = _catalog != null ? _catalog.FindById(s.effectId) : null;
                    if (effect == null)
                    {
                        GameLog.Warn(TAG, $"Unknown status effect '{s.effectId}' in save — skipped");
                        continue;
                    }
                    if (!(s.totalAmount > 0f) || float.IsInfinity(s.totalAmount)
                        || !(s.expiresAt > s.appliedAt) || double.IsInfinity(s.expiresAt))
                    {
                        GameLog.Warn(TAG, $"Invalid saved status effect '{s.effectId}' — skipped");
                        continue;
                    }
                    double shift = s.appliedAt > _gameClock.Now ? _gameClock.Now - s.appliedAt : 0d;
                    _active.Add(new StatusEffectInstance(effect, s.appliedAt + shift, s.expiresAt + shift, s.totalAmount,
                        float.IsNaN(s.paidAmount) ? 0f : Mathf.Clamp(s.paidAmount, 0f, s.totalAmount)));
                }
            }
            RaiseChanged();
        }

        private void RaiseChanged() => _onStatusEffectsChanged?.Raise(true);

        private void WarnNoClock()
        {
            if (!_warnedNoClock) GameLog.Warn(TAG, "No GameClock assigned (scene without Core?) — status effects are unavailable");
            _warnedNoClock = true;
        }
    }
}
