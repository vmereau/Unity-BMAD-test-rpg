using Game.Core;
using UnityEngine;

namespace Game.StatusEffects
{
    /// <summary>What a status effect does with its amount on each tick. A new kind needs a case in <see cref="StatusEffectHost"/>.</summary>
    public enum StatusEffectKind
    {
        Heal
    }

    /// <summary>What happens when an effect is applied while instances of the same effect are active.</summary>
    public enum StatusEffectStacking
    {
        /// <summary>Each application runs on its own; their payouts add up (capped by <see cref="StatusEffectSO.maxStacks"/>).</summary>
        Stack,
        /// <summary>The new application replaces every active instance of the effect (their unpaid remainder is lost).</summary>
        Refresh
    }

    /// <summary>
    /// A status effect type (behavior only). The size — total amount and duration — comes from whoever applies it
    /// (e.g. a food <c>PotionItemSO</c>), so one effect serves many items and the HUD groups instances by effect.
    /// Saved by <see cref="effectId"/> through the <see cref="StatusEffectCatalogSO"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Status Effects/Status Effect", fileName = "SE_")]
    public class StatusEffectSO : ScriptableObject
    {
        private const string TAG = "[StatusEffects]";

        [Tooltip("Stable save ID — never change it once saves exist.")]
        public string effectId;
        public string displayName;
        [Tooltip("HUD icon. Empty = a plain square in the tint colour.")]
        public Sprite icon;
        public Color iconTint = Color.white;

        public StatusEffectKind kind = StatusEffectKind.Heal;
        [Tooltip("Game seconds between payouts (60 = 2 real seconds at 48 real min per game day). The last tick lands on expiry.")]
        [Min(1f)] public float tickIntervalSeconds = 60f;

        public StatusEffectStacking stacking = StatusEffectStacking.Stack;
        [Tooltip("Stack only: max simultaneous instances (0 = unlimited). Applying past the cap drops the oldest.")]
        [Min(0)] public int maxStacks;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(effectId))
                GameLog.Warn(TAG, $"{name}: effectId is empty — active instances can't be saved");
        }
#endif
    }
}
