# Sprint Change Proposal — Equipment Visual & Hit Detection Architecture
**Date:** 2026-03-21
**Workflow:** Correct Course
**Status:** Approved — 2026-03-21

---

## Section 1: Issue Summary

### Problem Statement

Story 7-4 (Equipment Visual Update) was completed correctly within its placeholder scope. However, planning ahead to a wider weapon variety (sword, axe, lance, bow) exposed two architectural gaps that must be resolved before the shop (7-5) introduces new equippable weapons to the game.

**Issue A — Equipment visuals are hardcoded, not data-driven**
`EquipmentVisuals.RefreshWeapon()` always spawns a yellow cube primitive regardless of which weapon is equipped. `WeaponSO` and `EquipableItemSO` have no `equipVisualPrefab` field, so `Assets/_Game/Prefabs/Items/Weapons/Swords/Sword base.prefab` (already in the project) cannot be referenced from item data. There is also no hook for weapon-type-specific animations (a lance stance differs from a sword grip).

**Issue B — Hit detection is weapon-agnostic**
`PlayerCombat.ExecuteHitDetection()` runs `Physics.OverlapSphereNonAlloc(transform.position, _config.attackHitRange)` — a fixed-radius sphere centered on the player root, firing on input frame before any animation plays. This is independent of which weapon is equipped. A lance should have a longer, narrower reach than a dagger; a bow requires a different detection approach entirely.

### Discovery Context

Both gaps became visible when completing 7-4 and simultaneously planning the weapon variety needed for Epic 7's remaining stories (shop, looting) and Epic 8 (crafting). The `Sword base.prefab` already exists in the project with no way to connect it to the item system.

### Evidence

| Location | Issue |
|---|---|
| `EquipmentVisuals.cs:66` | `CreatePlaceholder(PrimitiveType.Cube, ...)` — hardcoded for every weapon |
| `WeaponSO.cs` | Only `damageBonus` field — no visual or animation fields |
| `EquipableItemSO.cs` | No `equipVisualPrefab` field |
| `PlayerCombat.cs:311` | `OverlapSphereNonAlloc(transform.position, _config.attackHitRange)` — weapon-agnostic sphere |
| `PlayerCombat.cs:263` | `ExecuteHitDetection()` called directly in `TryAttack()` — fires on input, not on swing frame |
| `Assets/_Game/Prefabs/Items/Weapons/Swords/Sword base.prefab` | Exists but unreachable from any ItemSO |

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact |
|---|---|
| **Epic 7** (in-progress) | Add 2 new stories (7-8, 7-9) before 7-5. Existing done stories unaffected. |
| **Epic 8** (backlog) | `8-7-crafting-system` will produce weapons — benefits automatically from data-driven visual system |
| **Epic 6** (backlog) | Quest-reward weapons will work correctly with no extra story needed |
| All other epics | No impact |

### Story Impact

**Stories requiring changes:** None — all done stories remain valid.

**New stories to add:**

| ID | Title | Depends on | Blocks |
|---|---|---|---|
| `7-8` | Equipment Visual Prefab Support | 7-4 (done) | 7-9, 7-5 |
| `7-9` | Weapon Collider Hit Detection | 7-8 | 7-5 |

**Revised Epic 7 backlog order:**
```
7-8  equipment-visual-prefab-support   ← NEW
7-9  weapon-collider-hit-detection     ← NEW
7-5  shop-npc-trading
7-6  looting-system
7-7  gold-bribe-system
```

### Artifact Conflicts

| Artifact | Change Needed |
|---|---|
| `WeaponSO.cs` | Add `animatorOverrideController: AnimatorOverrideController` |
| `EquipableItemSO.cs` | Add `equipVisualPrefab: GameObject` |
| `EquipmentVisuals.cs` | Update `RefreshWeapon/Helmet()` to use prefab when available; add `Animator` ref + override logic; expose `ActiveWeaponGO` |
| `PlayerCombat.cs` | Add `EquipmentVisuals` ref; subscribe to equipment change to rebind `WeaponHitbox`; activate hitbox on attack instead of sphere when weapon equipped |
| `WeaponHitbox.cs` | **New file** — `Assets/_Game/Scripts/Combat/WeaponHitbox.cs` |
| `sprint-status.yaml` | Insert 7-8 and 7-9 before 7-5 |
| `project-context.md` | Add rules for `equipVisualPrefab` contract, `WeaponHitbox` pattern, animator override pattern |

### Technical Impact

- **`EquipmentVisuals`**: gains `Animator` and `RuntimeAnimatorController` references (serialized), `ActiveWeaponGO` property, updated refresh logic. Placeholder fallback retained — no regression.
- **`WeaponHitbox`**: new `Game.Combat` component. Trigger collider(s) on weapon prefab child. `Enable()`/`Disable()` API. `OnEnemyHit` event for damage callback.
- **`PlayerCombat`**: gains `EquipmentVisuals` ref (serialized). On equip change: unbind old `WeaponHitbox`, bind new one. `ExecuteHitDetection()` replaced by hitbox activation for armed state; sphere fallback kept for unarmed.
- **Weapon prefabs**: need a `HitboxRoot` child with a trigger collider shaped to the weapon (capsule for sword/lance, box for axe).
- **ItemSO assets**: each weapon/armor SO needs `equipVisualPrefab` assigned to use real visuals (null = placeholder fallback, fully backward compatible).

---

## Section 3: Recommended Approach

**Direct Adjustment** — add two focused stories to Epic 7.

### Rationale

- Both stories build directly on infrastructure already in place from 7-4 (`WeaponSocket`, event subscription, `EquipmentSystem` integration)
- Changes are additive and backward compatible — nothing breaks while new content is being authored
- 7-8 must precede 7-9: weapon hitbox detection requires the prefab instantiation (from 7-8) to exist at runtime so `WeaponHitbox` is reachable on the active weapon GO
- Both must precede 7-5 (shop): players equipping shop-bought weapons should see correct visuals and experience correct hit reach from day one
- Effort and risk are both low — no rollback needed, no MVP scope change

### Effort & Risk

| | Story 7-8 | Story 7-9 |
|---|---|---|
| Effort | Low | Low-Medium |
| Risk | Low | Low |
| Timeline impact | Minimal | Minimal |

---

## Section 4: Detailed Change Proposals

### Change A — `EquipableItemSO.cs`

```
File: Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs
Section: Class body — new field

OLD:
    [Header("Stat Bonuses (additive, applied while equipped)")]
    public int strengthBonus;
    ...

NEW:
    [Header("Visuals")]
    public GameObject equipVisualPrefab; // Prefab instantiated on socket when equipped. Null = placeholder primitive.

    [Header("Stat Bonuses (additive, applied while equipped)")]
    public int strengthBonus;
    ...
```

Rationale: All equippable items (weapon, helmet, armor) can have a dedicated visual prefab. Null value preserves placeholder fallback — no existing SOs break.

---

### Change B — `WeaponSO.cs`

```
File: Assets/_Game/ScriptableObjects/Items/WeaponSO.cs
Section: Class body — new field

OLD:
    [Header("Combat")]
    public float damageBonus;

NEW:
    [Header("Combat")]
    public float damageBonus;

    [Header("Animation")]
    public AnimatorOverrideController animatorOverrideController; // Optional. Applied to player Animator on equip. Null = use default controller.
```

Rationale: Different weapon types require different animation sets (sword vs axe vs lance vs bow). Override controller is swapped in on equip and restored on unequip. Null = no override, safe default.

---

### Change C — `EquipmentVisuals.cs`

```
File: Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs
Section: Serialized fields — add Animator references

OLD:
    [SerializeField] private Material _armorPlaceholderMaterial;

NEW:
    [SerializeField] private Material _armorPlaceholderMaterial;
    [SerializeField] private Animator _animator;
    [SerializeField] private RuntimeAnimatorController _defaultAnimatorController;
```

```
Section: Private fields — add ActiveWeaponGO property

OLD:
    private GameObject _weaponVisual;

NEW:
    private GameObject _weaponVisual;
    public GameObject ActiveWeaponGO => _weaponVisual; // Exposed for PlayerCombat to locate WeaponHitbox
```

```
Section: RefreshWeapon() — use prefab when available

OLD:
    _weaponVisual = CreatePlaceholder(PrimitiveType.Cube, _weaponSocket, new Vector3(0.07f, 0.07f, 0.5f), Color.yellow);
    GameLog.Info(TAG, "Weapon visual attached");

NEW:
    if (weapon.equipVisualPrefab != null)
    {
        _weaponVisual = Object.Instantiate(weapon.equipVisualPrefab, _weaponSocket);
        _weaponVisual.transform.localPosition = Vector3.zero;
        _weaponVisual.transform.localRotation = Quaternion.identity;
        GameLog.Info(TAG, $"Weapon visual attached (prefab: {weapon.equipVisualPrefab.name})");
    }
    else
    {
        _weaponVisual = CreatePlaceholder(PrimitiveType.Cube, _weaponSocket, new Vector3(0.07f, 0.07f, 0.5f), Color.yellow);
        GameLog.Info(TAG, "Weapon visual attached (placeholder)");
    }
    ApplyAnimatorOverride((weapon as WeaponSO)?.animatorOverrideController);
```

```
Section: RefreshWeapon() destroy path — restore animator on unequip

OLD:
    if (_weaponVisual != null)
        Destroy(_weaponVisual);
    _weaponVisual = null;

NEW:
    if (_weaponVisual != null)
        Destroy(_weaponVisual);
    _weaponVisual = null;
    ApplyAnimatorOverride(null); // Restore default controller when weapon removed
```

```
Section: New private helper

NEW:
    private void ApplyAnimatorOverride(AnimatorOverrideController overrideController)
    {
        if (_animator == null) return;
        _animator.runtimeAnimatorController = overrideController != null
            ? overrideController
            : _defaultAnimatorController;
    }
```

Rationale: Prefab instantiation uses existing socket infrastructure. Placeholder path unchanged. Animator override is applied/cleared atomically with the visual refresh.

---

### Change D — New file `WeaponHitbox.cs`

```
File: Assets/_Game/Scripts/Combat/WeaponHitbox.cs (NEW)
Namespace: Game.Combat

public class WeaponHitbox : MonoBehaviour
{
    public event System.Action<EnemyHealth> OnEnemyHit;

    private Collider[] _colliders;

    private void Awake()
    {
        _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        Disable(); // Always start disabled
    }

    public void Enable()
    {
        foreach (var c in _colliders) c.enabled = true;
    }

    public void Disable()
    {
        foreach (var c in _colliders) c.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponentInParent<EnemyHealth>();
        if (health != null && !health.IsDead)
            OnEnemyHit?.Invoke(health);
    }
}
```

Rationale: Decoupled from `PlayerCombat` — fires an event rather than calling damage directly. `PlayerCombat` subscribes and applies `ComputeEffectiveDamage()` via the event. Colliders on the prefab child (`HitboxRoot`) are trigger colliders shaped to the weapon; they start disabled and are enabled only during attack frames.

---

### Change E — `PlayerCombat.cs`

```
File: Assets/_Game/Scripts/Combat/PlayerCombat.cs
Section: Serialized fields — add EquipmentVisuals ref

OLD:
    [SerializeField] private EquipmentSystem _equipmentSystem;

NEW:
    [SerializeField] private EquipmentSystem _equipmentSystem;
    [SerializeField] private EquipmentVisuals _equipmentVisuals;
    [SerializeField] private GameEventSO_Void _onEquipmentChanged; // Subscribe to rebind WeaponHitbox on weapon swap
```

```
Section: Private fields — add hitbox tracking

NEW:
    private WeaponHitbox _activeHitbox;
```

```
Section: OnEnable — subscribe to equipment changes

ADD:
    _onEquipmentChanged?.AddListener(RebindWeaponHitbox);
```

```
Section: OnDisable — unsubscribe and disable hitbox

ADD:
    _onEquipmentChanged?.RemoveListener(RebindWeaponHitbox);
    UnbindWeaponHitbox();
```

```
Section: TryAttack() — activate hitbox instead of sphere (when armed)

OLD:
    ExecuteHitDetection();

NEW:
    if (_activeHitbox != null)
        _activeHitbox.Enable();
    else
        ExecuteHitDetection(); // Fallback: unarmed sphere
```

```
Section: Combo reset paths — disable hitbox when attack ends

In all combo-end paths (finisher reset, window expiry, stamina denied):
ADD: _activeHitbox?.Disable();
```

```
Section: New private methods

private void RebindWeaponHitbox(bool _)
{
    UnbindWeaponHitbox();
    if (_equipmentVisuals == null) return;
    var weaponGO = _equipmentVisuals.ActiveWeaponGO;
    if (weaponGO == null) return;
    _activeHitbox = weaponGO.GetComponentInChildren<WeaponHitbox>();
    if (_activeHitbox != null)
        _activeHitbox.OnEnemyHit += OnWeaponHit;
}

private void UnbindWeaponHitbox()
{
    if (_activeHitbox != null)
    {
        _activeHitbox.Disable();
        _activeHitbox.OnEnemyHit -= OnWeaponHit;
        _activeHitbox = null;
    }
}

private void OnWeaponHit(EnemyHealth health)
{
    health.TakeDamage(ComputeEffectiveDamage());
    GameLog.Info(TAG, $"Weapon hit: {health.gameObject.name}");
}
```

Rationale: `ExecuteHitDetection()` (sphere) is retained as unarmed fallback. `WeaponHitbox` is rebound whenever equipment changes. Hit-per-enemy deduplication within a single swing is handled by disabling the hitbox colliders — once disabled after the combo step, no further triggers fire until the next attack.

---

### Change F — `sprint-status.yaml`

```
File: _bmad-output/implementation-artifacts/sprint-status.yaml
Section: epic-7 entries

OLD:
  7-5-shop-npc-trading: backlog
  7-6-looting-system: backlog
  7-7-gold-bribe-system: backlog

NEW:
  7-8-equipment-visual-prefab-support: backlog
  7-9-weapon-collider-hit-detection: backlog
  7-5-shop-npc-trading: backlog
  7-6-looting-system: backlog
  7-7-gold-bribe-system: backlog
```

---

## Section 5: Implementation Handoff

### Scope Classification: **Minor**

Both stories are self-contained, additive, and implementable directly by the dev team without backlog reorganization or architectural escalation.

### Handoff: Development team

**Story 7-8 responsibilities:**
- Add `equipVisualPrefab` to `EquipableItemSO`
- Add `animatorOverrideController` to `WeaponSO`
- Update `EquipmentVisuals` (prefab instantiation, animator override, `ActiveWeaponGO`)
- Assign `Sword base.prefab` as `equipVisualPrefab` on the test sword SO
- Wire new serialized refs on Player prefab (`_animator`, `_defaultAnimatorController`)

**Story 7-9 responsibilities:**
- Create `WeaponHitbox.cs`
- Add `HitboxRoot` child with trigger collider to `Sword base.prefab` (and any other weapon prefabs)
- Update `PlayerCombat` (hitbox binding, `RebindWeaponHitbox`, `OnWeaponHit`, fallback logic)
- Wire `_equipmentVisuals` and `_onEquipmentChanged` on Player prefab

**Success criteria:**
- Equipping `Sword base.prefab` shows the real 3D mesh on the player's hand (not a yellow cube)
- Equipping a `WeaponSO` with `animatorOverrideController` assigned changes the player's idle/attack animations
- Sword swing hits only enemies within the collider's actual reach (not a sphere around the player)
- Unequipping reverts to placeholder and restores default animations
- Unarmed attacks (no weapon equipped) still register via the sphere fallback
- No regressions on existing done stories (7-1 through 7-4)
