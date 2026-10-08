using System.Collections.Generic;
using Game.Animations;
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
    /// Combos: the brain rolls the hit count (<see cref="BeginAttack(float, int)"/>, clamped to the
    /// driver's <see cref="AIAnimationDriver.MaxComboSteps"/>); step 1 is the driver's TriggerAttack,
    /// steps 2..N are requested on <see cref="OnComboWindowOpen"/>. Each step's clip opens its own window,
    /// so every hit can damage the target once (damage per hit).
    /// The attack ends when the animator's active attack-state count (SMB enter/exit) returns to 0 —
    /// crossfades between combo states overlap enter(next) before exit(previous), keeping the attack alive.
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
        [Tooltip("Plays attack/combo steps. Auto-resolved on the same GO if null.")]
        [SerializeField] private AIAnimationDriver _animationDriver;

        public float CurrentDamage { get; private set; }
        public bool IsAttacking { get; private set; }
        public IReadOnlyList<NamedHitbox> Hitboxes => _hitboxes;
        public bool IsInAttackState => _activeAttackStates > 0;
        public int ComboStep => _combo.CurrentStep;
        public int ComboHits => _combo.TotalHits;

        private readonly HashSet<string> _warnedUnknownIds = new();
        private readonly AttackComboPlan _combo = new();
        private int _activeAttackStates;

        private void Awake()
        {
            if (_selfFactionMember == null) _selfFactionMember = GetComponent<FactionMember>();
            // No warning when missing — driverless setups (tests) are valid and play single hits.
            if (_animationDriver == null) _animationDriver = GetComponent<AIAnimationDriver>();
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
            _activeAttackStates = 0;
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

        /// <summary>Single-hit attack — see <see cref="BeginAttack(float, int)"/>.</summary>
        public void BeginAttack(float damage) => BeginAttack(damage, 1);

        /// <summary>
        /// Called by the brain when an attack starts: closes stale windows, stores the per-hit damage and
        /// plans a combo of <paramref name="comboHits"/> (clamped to the driver's MaxComboSteps; no driver → 1).
        /// </summary>
        public void BeginAttack(float damage, int comboHits)
        {
            CloseAllWindows();
            CurrentDamage = damage;
            IsAttacking = true;
            _combo.Begin(comboHits, _animationDriver != null ? _animationDriver.MaxComboSteps : 1);
        }

        /// <summary>
        /// Attack over or interrupted (last state exit, death, disengage) — closes every window, clears the
        /// combo and drops any queued attack trigger. Does not touch the attack-state count, which mirrors
        /// the animator.
        /// </summary>
        public void EndAttack()
        {
            CloseAllWindows();
            IsAttacking = false;
            _combo.Reset();
            if (_animationDriver != null) _animationDriver.CancelAttack();
        }

        /// <summary>ComboWindowOpen clip event — requests the next combo step while hits remain.</summary>
        public void OnComboWindowOpen()
        {
            if (!IsAttacking) return;
            if (!_combo.TryAdvance(out int next)) return;
            if (_animationDriver != null) _animationDriver.TriggerComboStep(next);
        }

        /// <summary>An attack animator state was entered (SMB_EntityAttackState).</summary>
        public void NotifyAttackStateEntered() => _activeAttackStates++;

        /// <summary>An attack animator state was exited — ends the attack when none remain active.</summary>
        public void NotifyAttackStateExited()
        {
            _activeAttackStates = Mathf.Max(0, _activeAttackStates - 1);
            if (_activeAttackStates == 0) EndAttack();
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
                    // Tell the victim's brain who hit it, so its damage reaction engages us, not a bystander.
                    if (target is Component targetComponent &&
                        targetComponent.TryGetComponent(out ISneakAttackTarget victim))
                        victim.NotifyHitBy(gameObject);
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
