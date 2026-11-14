using System.Collections.Generic;
using Game.Core;
using Game.Inventory;
using Game.NPC;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Gives an NPC its skin tone (spec-npc-skin-tones). On the NPC root: reads <see cref="NPCEntity.SkinTone"/> through
    /// <see cref="PersistentID"/> and swaps every masked <c>SG_SkinLit</c> material under the model for its shared toned copy
    /// (<see cref="SkinToneMaterials"/>). Applied in <c>Start</c> (after <see cref="NpcHeadAppearance"/> swapped the head in
    /// <c>Awake</c>), and again whenever <see cref="EquipmentVisuals.ArmorVisual"/> changes (polled — no cross-system C#
    /// event), so a newly equipped armor's hands match. A skin material without a mask (head variants) keeps its own tone,
    /// with one warning.
    /// </summary>
    public class NpcSkinTone : MonoBehaviour
    {
        private const string TAG = "[NpcAppearance]";

        [Tooltip("Model root (Character): every renderer below it is toned, including armor visuals instantiated there.")]
        [SerializeField] private Transform _model;
        [Tooltip("Optional: the tone is re-applied when its armor visual changes.")]
        [SerializeField] private EquipmentVisuals _visuals;

        private SkinToneSO _tone;
        private GameObject _tonedArmor;
        private bool _started;
        private readonly HashSet<Material> _warned = new();
        private readonly List<Material> _materials = new();

        public SkinToneSO Tone => _tone;

        private void Awake()
        {
            if (TryGetComponent(out PersistentID id) && id.Entity is NPCEntity npc)
                _tone = npc.SkinTone;
            if (_tone == null) enabled = false; // nothing to poll
        }

        private void Start()
        {
            _started = true;
            Apply(_tone);
        }

        private void LateUpdate()
        {
            if (!_started || _visuals == null) return;
            var armor = _visuals.ArmorVisual;
            if (armor != null && armor != _tonedArmor) Apply(_tone);
        }

        /// <summary>
        /// Gives every masked skin material under the model <paramref name="tone"/> (null = back to the untoned source
        /// materials). Re-toning an already toned NPC swaps its copies. Returns the number of material slots changed.
        /// </summary>
        public int Apply(SkinToneSO tone)
        {
            _tone = tone;
            if (_visuals != null) _tonedArmor = _visuals.ArmorVisual; // also when _model is missing: no per-frame retry
            if (tone != null) enabled = true; // keep polling armor rebuilds (Awake disables an untoned NPC)
            if (_model == null)
            {
                if (tone != null) GameLog.Warn(TAG, $"{name}: no model assigned — skin tone {tone.name} not applied");
                return 0;
            }
            int swapped = 0;
            foreach (var renderer in _model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetSharedMaterials(_materials);
                bool changed = false;
                for (int i = 0; i < _materials.Count; i++)
                {
                    var current = _materials[i];
                    var source = SkinToneMaterials.SourceOf(current);
                    if (!SkinToneMaterials.IsSkin(source)) continue;
                    if (tone != null && !SkinToneMaterials.HasMask(source))
                    {
                        if (_warned.Add(source))
                            GameLog.Warn(TAG, $"{name}: {renderer.name} material {source.name} has no skin mask — keeps its own skin tone");
                        continue;
                    }
                    var target = tone != null ? SkinToneMaterials.Get(source, tone) : source;
                    if (target == current) continue;
                    _materials[i] = target;
                    changed = true;
                    swapped++;
                }
                if (changed) renderer.sharedMaterials = _materials.ToArray();
            }
            return swapped;
        }
    }
}
