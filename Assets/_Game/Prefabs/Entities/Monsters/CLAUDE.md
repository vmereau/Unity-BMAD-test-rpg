# CLAUDE.md — Assets/_Game/Prefabs/Entities/Monsters

> Loaded when Claude accesses files in this folder. Covers monster prefab structure requirements for hit detection.

---

## Monster Prefab Structure

### Current Monster: Monster_DarknessSpider

```
Monster_DarknessSpider Variant.prefab  (variant of Assets/_Game/Prefabs/Entities/Entity_base.prefab)
├── NavMeshAgent, EntityBrain, EntityHealth, PersistentID, EntityPresence, InventorySystem,
│   EntityMeleeAttacker (_hitboxes: Bite)  ← ROOT (Layer 6 Characters)
├── CreatureVisual        ← model + Animator + EntityAnimationEventReceiver (ragdoll bone colliders on Layer 6)
│   ├── …/cephalothorax/Hitbox_Bite  ← disabled trigger SphereCollider + WeaponHitbox (attacker side)
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
- [ ] Attack recipe below applied (attacker, receiver, `Hitbox_<id>`, clip events)

---

## Monster Attacks (animation-driven hit windows)

Recipe for any monster that attacks (see `Scripts/AI/CLAUDE.md`):

1. `EntityMeleeAttacker` on the root; `_selfFactionMember` = root `FactionMember`; assign it to `EntityBrain._meleeAttacker`.
2. `EntityAnimationEventReceiver` on the **Animator GO** (`CreatureVisual`), `_attacker` assigned.
3. `Hitbox_<id>` child under the biting/striking bone: disabled trigger Sphere/Box/Capsule + `WeaponHitbox`,
   Layer Default, **no Rigidbody** (the ragdoll scan grabs every child Rigidbody). Register `<id>` in `_hitboxes`.
4. Author `HitboxEnable(<id>)` / `HitboxDisable(<id>)` on the attack clip with the **Hitbox Tuner**
   (`Tools/Combat/Hitbox Tuner`, Prefab Mode). Check reach: root → hitbox centre at the lunge frame +
   radius + padding + 0.3 (player hurtbox radius) must cover `Entity.AttackRange`, and
   `EngageStoppingDistance` ≤ `AttackRange`.

**Never author events on vendor FBX clips.** The HEROIC FANTASY pack folder is **gitignored**
(`/Assets/HEROIC FANTASY CREATURES FULL PACK VOL 1`) and a pack re-import wipes `.meta` edits — events
written there are unversioned and fragile. Instead **copy the attack clip into the project** as a `.anim`
(`Object.Instantiate(fbxClip)` + `AssetDatabase.CreateAsset` keeps curves and clip settings) under
`Art/Characters/Monsters/<Monster>/Animations/<monster>_<action>.anim`, point the monster's
`AnimatorOverrideController` slot at it, and author events on the copy (the tuner writes `.anim` events in
seconds). The vendor pack stays untouched.

### DarknessSpider Bite (2026-10-01)

- **Fangs are the chelicerae** (`cephalothorax/Dummy025/chelicere_[LR]_[ab]`), front = +Z.
  `hook_L/R` are under `Abdomen_3` — the abdomen tip, **not** the fangs.
- `Hitbox_Bite` under `cephalothorax`, local position `(-0.27, 0, 0.01)` (chelicere_b midpoint at the
  lunge; drifts ≤ 0.04 in cephalothorax space over the clip), `SphereCollider` r 0.15,
  `_reachPadding` 0.1, `_verticalReach` 0.3.
- Attack clip = `Art/Characters/Monsters/DarknessSpider/Animations/darknessSpider_bite.anim` — a project
  copy of the pack's `DarknessSpider@CrawlBiteThreat` (frames 0–20, 30 fps), assigned to the `Bite` slot of
  `DarknessSpider.overrideController`. `HitboxEnable("Bite")` frame 7 (0.233 s), `HitboxDisable("Bite")`
  frame 13 (0.433 s). Lunge starts f6–8, fangs fully forward f10–12 (z ≈ 0.37 m from root), recoil from
  f14. Picked from sampled bone positions — refine visually with the tuner.
- `EnemyType_DarknessSpider`: `AttackRange` 2.2 → **0.8**, `EngageStoppingDistance` 1.8 → **0.65**
  (reach ≈ 0.37 + 0.15 + 0.1 + 0.3 = 0.92).
