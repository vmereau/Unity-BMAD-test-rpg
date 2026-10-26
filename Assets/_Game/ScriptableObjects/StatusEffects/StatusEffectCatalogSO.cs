using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.StatusEffects
{
    /// <summary>
    /// Every <see cref="StatusEffectSO"/> that can be saved, resolvable by its <see cref="StatusEffectSO.effectId"/>.
    /// Maintained by hand — a new effect must be added here or its active instances are dropped on load.
    /// </summary>
    [CreateAssetMenu(menuName = "Status Effects/Status Effect Catalog", fileName = "StatusEffectCatalog")]
    public class StatusEffectCatalogSO : ScriptableObject
    {
        private const string TAG = "[StatusEffects]";

        [SerializeField] private List<StatusEffectSO> _effects = new List<StatusEffectSO>();

        /// <summary>May contain null entries — consumers must skip them.</summary>
        public IReadOnlyList<StatusEffectSO> Effects => _effects;

        /// <summary>The first effect with this ID, or null when the ID is empty or unknown.</summary>
        public StatusEffectSO FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var effect in _effects)
                if (effect != null && effect.effectId == id) return effect;
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var seen = new HashSet<string>();
            foreach (var effect in _effects)
            {
                if (effect == null) continue;
                if (string.IsNullOrEmpty(effect.effectId))
                    GameLog.Warn(TAG, $"{name}: {effect.name} has no effectId — it can't be restored from a save");
                else if (!seen.Add(effect.effectId))
                    GameLog.Warn(TAG, $"{name}: duplicate effectId '{effect.effectId}' ({effect.name}) — only the first resolves");
            }
        }
#endif
    }
}
