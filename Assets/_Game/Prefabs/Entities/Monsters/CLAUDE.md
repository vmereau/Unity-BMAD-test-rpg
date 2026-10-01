# CLAUDE.md — Assets/_Game/Prefabs/Entities/Monsters

> Loaded when Claude accesses files in this folder. Covers monster prefab structure requirements for hit detection.

---

## Monster Prefab Structure

### Current Monster: Monster_DarknessSpider

```
Monster_DarknessSpider Variant.prefab  (variant of Assets/_Game/Prefabs/Entities/Entity_base.prefab)
├── NavMeshAgent, EntityBrain, EntityHealth, PersistentID, EntityPresence, InventorySystem  ← ROOT (Layer 6 Characters)
├── CreatureVisual        ← model (ragdoll bone colliders on Layer 6)
│   └── HitBox            ← trigger BoxCollider, Layer 7 CharacterHitbox — the hurtbox
├── InteractionCollider   ← trigger, Layer 8 Interactable (inherited from Entity_base)
└── EntityUICanvas        ← name / HP world-space UI (inherited from Entity_base)
```

**Spider `HitBox` values** (sized to the skinned-mesh bounds, legs included): `BoxCollider`,
`isTrigger = true`, center `(0, 0.293, -0.064)`, size `(1.73, 0.498, 1.43)`. It replaced the old
1.5 m-tall capsule (r 0.4, center y 0.75) that floated above the model as a workaround.

---

## Hit Detection Contract (WeaponHitbox sweeps)

`WeaponHitbox` (attacker side, see `Scripts/Combat/CLAUDE.md`) runs `Physics.Overlap*NonAlloc`
against Layer 7 with `QueryTriggerInteraction.Collide`. Consequences for targets:

- The hurtbox is a **trigger** collider on a child GO, Layer 7 **CharacterHitbox** — trigger so it
  never blocks movement or ragdolls.
- **No Rigidbody needed** on the entity or the weapon; the layer collision matrix is irrelevant
  (explicit-mask queries ignore it).
- **Size the hurtbox to the real body.** Low targets are reached by the attacker's `_verticalReach`
  (downward reach capsule), never by inflating the target's hurtbox.
- The target is resolved with `GetComponentInParent<IDamageable>()` from the hurtbox collider —
  `EntityHealth` (an `IDamageable`) must be on the root or any ancestor of `HitBox`. A sibling or
  unrelated GO → hits silently ignored.

---

## Adding New Monster Types

Checklist when creating a new monster prefab (as a variant of `Entity_base`):

- [ ] Hurtbox collider on a child GO wrapping the visible body (Box for wide/flat bodies, Capsule for upright ones)
- [ ] Hurtbox is a **trigger**, on **CharacterHitbox (Layer 7)**; root stays on **Characters (Layer 6)**
- [ ] `EntityHealth` component is on the root GO or any ancestor of the hurtbox GO
- [ ] No `Rigidbody` needed for hit detection
