using Game.Combat;
using Game.Core;
using Game.Factions;
using Game.Stealth;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Tags a GameObject as a targetable participant in faction combat.
    /// Self-registers with TargetRegistry on enable.
    /// Faction is sourced from PersistentID.Entity.Faction by default; override per-instance with _factionOverride
    /// (the Player uses _factionOverride because Player.prefab has no PersistentID).
    /// </summary>
    public class FactionMember : MonoBehaviour
    {
        private const string TAG = "[Faction]";

        [Tooltip("Optional. If null, faction is read from PersistentID.Entity.Faction.")]
        [SerializeField] private FactionSO _factionOverride;
        [Tooltip("Optional. Used to resolve faction from the Entity SO when no override is set.")]
        [SerializeField] private PersistentID _persistentID;

        private IDamageable _damageable;
        private FactionSO _faction;
        private bool _registered;

        public FactionSO Faction => _faction;
        public IDamageable Damageable => _damageable;
        public Transform Transform => transform;

        private IStealthTarget _stealthTarget;

        /// <summary>
        /// Non-null when this member is detected through the vision cone (the player, via PlayerSneak) instead of
        /// the plain detection radius. Resolved once in Awake. A disabled stealth-target component (e.g. PlayerSneak
        /// missing its config) reads as null, so the member falls back to plain radius detection.
        /// </summary>
        public IStealthTarget StealthTarget
        {
            get
            {
                if (_stealthTarget is Behaviour behaviour && (behaviour == null || !behaviour.enabled)) return null;
                return _stealthTarget;
            }
            private set => _stealthTarget = value;
        }

        private void Awake()
        {
            if (TryGetComponent(out IStealthTarget stealthTarget)) StealthTarget = stealthTarget;

            _damageable = GetComponent<IDamageable>();
            if (_damageable == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: no IDamageable component found on root — FactionMember disabled");
                enabled = false;
                return;
            }

            ResolveFaction();
            if (_faction == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: no faction resolved (override null and Entity SO faction null) — FactionMember disabled");
                enabled = false;
            }
        }

        // Priority: _factionOverride → PersistentID.Entity.Faction. Re-run on enable so a pooled/respawned
        // entity reused with a different Entity SO refreshes its faction instead of keeping a stale value.
        private void ResolveFaction()
        {
            if (_factionOverride != null)
            {
                _faction = _factionOverride;
                if (_persistentID != null && _persistentID.Entity != null &&
                    _persistentID.Entity.Faction != null && _persistentID.Entity.Faction != _factionOverride)
                {
                    GameLog.Warn(TAG, $"{gameObject.name}: _factionOverride ({_factionOverride.factionName}) shadows a different PersistentID faction ({_persistentID.Entity.Faction.factionName}).");
                }
                return;
            }

            if (_persistentID != null && _persistentID.Entity != null)
                _faction = _persistentID.Entity.Faction;
        }

        private void OnEnable()
        {
            if (_damageable == null) return; // Awake failed to find one — component already disabled
            ResolveFaction();
            if (_faction == null) return;
            TargetRegistry.Register(this);
            _registered = true;
        }

        private void OnDisable()
        {
            if (!_registered) return; // Guard: Awake may disable before OnEnable runs (root CLAUDE.md rule)
            TargetRegistry.Unregister(this);
            _registered = false;
        }
    }
}
