using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Owner-agnostic AI melee attacker on the entity root (same GO as <see cref="EntityBrain"/> /
    /// <see cref="FactionMember"/>). Owns named <see cref="WeaponHitbox"/>es (bone-attached for
    /// creatures, weapon-attached for humanoids) and opens/closes them from <c>HitboxEnable(id)</c> /
    /// <c>HitboxDisable(id)</c> animation events routed by <see cref="EntityAnimationEventReceiver"/>.
    /// Hits are filtered by faction hostility, then resolved like the player:
    /// <c>TryReceiveHit → TakeDamage</c> only on <see cref="HitResult.NotBlocked"/>.
    /// Damage comes from the brain (<see cref="BeginAttack"/>), not the hitbox. Dedupe stays in
    /// <see cref="WeaponHitbox"/> — one hit per target per window; two hitboxes open at once can each
    /// hit the same target once.
    /// Humanoid reuse path: register the equipped weapon's <see cref="WeaponHitbox"/> at runtime
    /// (<see cref="RegisterHitbox"/>, e.g. id <c>Weapon</c>); the player's parameterless clip events
    /// arrive as an empty id, which opens every hitbox.
    /// </summary>
    public class EntityMeleeAttacker : MonoBehaviour
    {
        private const string TAG = "[AI]";

        [System.Serializable]
        public struct NamedHitbox
        {
            public string Id;
            public WeaponHitbox Hitbox;
        }

        [Tooltip("Hitboxes opened by HitboxEnable(id) animation events. An event with an empty id opens all.")]
        [SerializeField] private List<NamedHitbox> _hitboxes = new();
        [Tooltip("This entity's faction — hits only land on hostile factions. Auto-resolved on the same GO if null.")]
        [SerializeField] private FactionMember _selfFactionMember;

        public float CurrentDamage { get; private set; }
        public bool IsAttacking { get; private set; }
        public IReadOnlyList<NamedHitbox> Hitboxes => _hitboxes;

        private readonly HashSet<string> _warnedUnknownIds = new();

        private void Awake()
        {
            if (_selfFactionMember == null) _selfFactionMember = GetComponent<FactionMember>();
            if (_selfFactionMember == null)
                GameLog.Warn(TAG, $"{gameObject.name}: EntityMeleeAttacker has no FactionMember — hits land on any target with a FactionMember");

            var seenIds = new HashSet<string>();
            foreach (NamedHitbox entry in _hitboxes)
            {
                if (entry.Hitbox == null)
                {
                    GameLog.Warn(TAG, $"{gameObject.name}: hitbox '{entry.Id}' is not assigned — skipped");
                    continue;
                }
                if (!seenIds.Add(entry.Id ?? string.Empty))
                    GameLog.Warn(TAG, $"{gameObject.name}: duplicate hitbox id '{entry.Id}'");
                entry.Hitbox.SetOwner(transform);
                entry.Hitbox.OnHit += HandleHit;
            }
        }

        private void OnDisable()
        {
            EndAttack();
        }

        private void OnDestroy()
        {
            foreach (NamedHitbox entry in _hitboxes)
                if (entry.Hitbox != null) entry.Hitbox.OnHit -= HandleHit;
        }

        /// <summary>Runtime add (e.g. a humanoid's equipped weapon). Duplicate id → warn and ignore.</summary>
        public void RegisterHitbox(string id, WeaponHitbox hitbox)
        {
            if (hitbox == null) return;
            id ??= string.Empty;
            if (IndexOf(id) >= 0)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: hitbox id '{id}' already registered — ignored");
                return;
            }
            hitbox.SetOwner(transform);
            hitbox.OnHit += HandleHit;
            _hitboxes.Add(new NamedHitbox { Id = id, Hitbox = hitbox });
        }

        /// <summary>Runtime remove — closes the hitbox's window and unsubscribes it.</summary>
        public void UnregisterHitbox(string id)
        {
            int index = IndexOf(id ?? string.Empty);
            if (index < 0) return;
            WeaponHitbox hitbox = _hitboxes[index].Hitbox;
            if (hitbox != null)
            {
                hitbox.Disable();
                hitbox.OnHit -= HandleHit;
            }
            _hitboxes.RemoveAt(index);
        }

        /// <summary>Called by the brain when an attack starts: closes stale windows and stores the damage.</summary>
        public void BeginAttack(float damage)
        {
            CloseAllWindows();
            CurrentDamage = damage;
            IsAttacking = true;
        }

        /// <summary>Attack over or interrupted (state exit, death, disengage) — closes every window.</summary>
        public void EndAttack()
        {
            CloseAllWindows();
            IsAttacking = false;
        }

        /// <summary>Opens the hitbox with <paramref name="id"/>; null/empty = all. Ignored outside an attack.</summary>
        public void OpenWindow(string id)
        {
            if (!IsAttacking) return;
            if (string.IsNullOrEmpty(id))
            {
                foreach (NamedHitbox entry in _hitboxes)
                    if (entry.Hitbox != null) entry.Hitbox.Enable();
                return;
            }

            int index = IndexOf(id);
            if (index < 0)
            {
                if (_warnedUnknownIds.Add(id))
                    GameLog.Warn(TAG, $"{gameObject.name}: HitboxEnable('{id}') — no hitbox registered with that id");
                return;
            }
            if (_hitboxes[index].Hitbox != null) _hitboxes[index].Hitbox.Enable();
        }

        /// <summary>Closes the hitbox with <paramref name="id"/>; null/empty = all. Unknown id is silent.</summary>
        public void CloseWindow(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                CloseAllWindows();
                return;
            }
            int index = IndexOf(id);
            if (index >= 0 && _hitboxes[index].Hitbox != null) _hitboxes[index].Hitbox.Disable();
        }

        private void CloseAllWindows()
        {
            foreach (NamedHitbox entry in _hitboxes)
                if (entry.Hitbox != null) entry.Hitbox.Disable();
        }

        private int IndexOf(string id)
        {
            for (int i = 0; i < _hitboxes.Count; i++)
                if ((_hitboxes[i].Id ?? string.Empty) == id) return i;
            return -1;
        }

        private void HandleHit(IDamageable target, Vector3 hitPoint)
        {
            if (target == null || target.IsDead || !IsAttacking) return;
            if (!IsHostileTarget(target)) return;

            HitResult result = target.TryReceiveHit(gameObject);
            switch (result)
            {
                case HitResult.PerfectBlock:
                    GameLog.Info(TAG, $"{gameObject.name} attack staggered by perfect block");
                    break;
                case HitResult.Blocked:
                    GameLog.Info(TAG, $"{gameObject.name} attack blocked — no damage");
                    break;
                case HitResult.Dodged:
                    GameLog.Info(TAG, $"{gameObject.name} attack dodged — no damage");
                    break;
                case HitResult.NotBlocked:
                    target.TakeDamage(CurrentDamage);
                    GameLog.Info(TAG, $"{gameObject.name} hit landed at {hitPoint}");
                    break;
            }
        }

        private bool IsHostileTarget(IDamageable target)
        {
            if (target is not Component component || component == null) return false;
            if (!component.TryGetComponent(out FactionMember member))
                member = component.GetComponentInParent<FactionMember>();
            if (member == null) return false;
            if (_selfFactionMember == null || _selfFactionMember.Faction == null) return true;
            return _selfFactionMember.Faction.IsHostileTo(member.Faction);
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: appends to the serialized list (caller records Undo). Used by the Hitbox Tuner.</summary>
        public void EditorAddHitbox(string id, WeaponHitbox hitbox)
        {
            _hitboxes.Add(new NamedHitbox { Id = id, Hitbox = hitbox });
        }
#endif
    }
}
