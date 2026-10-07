using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Opt-in outline for an interactable. Placed on the interactable's root; <see cref="InteractionSystem"/>
    /// toggles the Outline rendering-layer bit on <see cref="_targets"/> when it gains / loses focus, and
    /// <c>InteractionOutlineFeature</c> draws the outline around renderers carrying that bit.
    /// No component = prompt-only (no outline). Materials are never swapped.
    /// </summary>
    public class InteractionHighlight : MonoBehaviour
    {
        private const string TAG = "[Interaction]";

        [Tooltip("Meshes to outline. Empty = all MeshRenderer/SkinnedMeshRenderer children.")]
        [SerializeField] private Renderer[] _targets;
        [SerializeField] private bool _overrideColor;
        [SerializeField] private Color _color = Color.white;

        private static int s_activeCount;

        private bool _highlighted;
        private uint _appliedBits;

        public bool HasColorOverride => _overrideColor;
        public Color ColorOverride => _color;

        /// <summary>True while at least one interactable is outlined — lets the renderer feature skip its passes.</summary>
        public static bool AnyHighlighted => s_activeCount > 0;

        // Static state survives Enter Play Mode when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_activeCount = 0;

        private void Awake()
        {
            if (_targets != null && _targets.Length > 0) return;

            var found = GetComponentsInChildren<Renderer>(true);
            var meshes = new List<Renderer>(found.Length);
            foreach (var r in found)
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                    meshes.Add(r);
            }
            _targets = meshes.ToArray();

            if (_targets.Length == 0)
                GameLog.Warn(TAG, $"InteractionHighlight on '{name}' found no MeshRenderer/SkinnedMeshRenderer — nothing will be outlined");
        }

        public void SetHighlighted(bool on, uint layerBits)
        {
            if (on == _highlighted || layerBits == 0) return;
            // No renderers → nothing to outline; don't count it, or the feature would run on an empty mask.
            if (_targets == null || _targets.Length == 0) return;

            foreach (var r in _targets)
            {
                if (r == null) continue;
                r.renderingLayerMask = on
                    ? InteractionFocus.WithLayer(r.renderingLayerMask, layerBits)
                    : InteractionFocus.WithoutLayer(r.renderingLayerMask, layerBits);
            }

            _highlighted = on;
            _appliedBits = on ? layerBits : 0;
            s_activeCount += on ? 1 : -1;
            if (s_activeCount < 0) s_activeCount = 0;
        }

        private void OnDisable()
        {
            // Destroyed or pooled targets must never leave a stale layer bit or active count behind.
            if (_highlighted) SetHighlighted(false, _appliedBits);
        }
    }
}
