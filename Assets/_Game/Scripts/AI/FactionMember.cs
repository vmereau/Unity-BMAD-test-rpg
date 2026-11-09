using Game.Combat;
using Game.Core;
using Game.Factions;
using Game.Stealth;
using Game.World;
using UnityEngine;
using UnityEngine.Serialization;
using _Game.ScriptableObjects.Entities;

namespace Game.AI
{
    /// <summary>
    /// Tags a GameObject as a targetable participant in faction combat.
    /// Self-registers with TargetRegistry on enable.
    /// Single faction source per GameObject: an entity (PersistentID with an Entity SO) always uses
    /// <c>Entity.Faction</c>; the serialized <c>_faction</c> is only for a GameObject without an Entity — the Player.
    /// </summary>
    public class FactionMember : MonoBehaviour
    {
        private const string TAG = "[Faction]";

        [Tooltip("Only for a GameObject without a PersistentID Entity (the Player). Entities use their Entity SO faction.")]
        [SerializeField, FormerlySerializedAs("_factionOverride")] private FactionSO _faction;
        [Tooltip("Optional. Auto-resolved on the same GameObject if null. Its Entity SO provides the faction.")]
        [SerializeField] private PersistentID _persistentID;

        private IDamageable _damageable;
        private FactionSO _resolvedFaction;
        private bool _registered;
        private bool _misconfigLogged;

        public FactionSO Faction => _resolvedFaction;
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

        /// <summary>
        /// True for the player: an <see cref="IStealthTarget"/> component exists on this GameObject, whether or not it
        /// is enabled (a disabled PlayerSneak must not hide the player's faction from <see cref="TargetRegistry.Player"/>).
        /// </summary>
        public bool IsPlayer => _stealthTarget is UnityEngine.Object obj ? obj != null : _stealthTarget != null;

        private void Awake()
        {
            if (TryGetComponent(out IStealthTarget stealthTarget)) StealthTarget = stealthTarget;
            if (_persistentID == null) TryGetComponent(out _persistentID);

            _damageable = GetComponent<IDamageable>();
            if (_damageable == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: no IDamageable component found on root — FactionMember disabled");
                enabled = false;
                return;
            }

            ResolveFaction();
            if (_resolvedFaction == null)
            {
                string reason = EntityOrNull != null
                    ? $"Entity SO '{EntityOrNull.name}' has no faction"
                    : "no Entity SO and no _faction";
                GameLog.Error(TAG, $"{gameObject.name}: no faction resolved ({reason}) — FactionMember disabled");
                enabled = false;
            }
        }

        // Entity SO faction wins; _faction is only read when there is no Entity. Re-run on enable so a
        // pooled/respawned entity reused with a different Entity SO refreshes its faction.
        private Entity EntityOrNull => _persistentID != null ? _persistentID.Entity : null;

        private void ResolveFaction()
        {
            Entity entity = EntityOrNull;
            if (entity != null)
            {
                _resolvedFaction = entity.Faction;
                if (_faction != null && !_misconfigLogged)
                {
                    _misconfigLogged = true;
                    GameLog.Error(TAG, $"{gameObject.name}: _faction ({_faction.factionName}) is set on an entity — ignored, " +
                                       "only the Entity SO faction counts. Clear the field.");
                }
                return;
            }

            _resolvedFaction = _faction;
        }

        private void OnEnable()
        {
            if (_damageable == null) return; // Awake failed to find one — component already disabled
            ResolveFaction();
            if (_resolvedFaction == null) return;
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
