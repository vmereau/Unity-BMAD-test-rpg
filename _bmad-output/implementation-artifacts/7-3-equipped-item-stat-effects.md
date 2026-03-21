# Story 7.3: Equipped Item Stat Effects

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want equipped items to modify my character stats and reduce incoming damage,
so that finding and equipping better gear creates meaningful power progression.

## Acceptance Criteria

### AC 1 — Stat bonus and defense bonus fields on `EquipableItemSO`

Add to `Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs` (namespace `Game.Inventory`):

```csharp
[Header("Stat Bonuses (additive, applied while equipped)")]
public int strengthBonus;
public int dexterityBonus;
public int enduranceBonus;
public int manaBonus;
public int defenseBonus;
```

- All fields default to `0` — **fully backwards-compatible** with all existing `WeaponSO` and `ArmorSO` SO assets
- Fields live on `EquipableItemSO` (the abstract base), not on `WeaponSO`/`ArmorSO` separately — any future equippable type automatically inherits stat bonus authoring without touching `EquipmentSystem`
- `defenseBonus` feeds the new `PlayerStats.Defense` property (see AC 2)

---

### AC 2 — `WeaponSO` gains a direct `damageBonus` field

Add to `Assets/_Game/ScriptableObjects/Items/WeaponSO.cs` (namespace `Game.Inventory`):

```csharp
[Header("Combat")]
public float damageBonus;
```

- Flat damage added directly to `ComputeEffectiveDamage()` when this weapon is equipped (separate from and stacked on top of the Strength stat bonus)
- Defaults to `0f` — backwards-compatible
- This is distinct from `strengthBonus`: `strengthBonus` scales via `damagePerStrength`, while `damageBonus` is an unscaled flat addition

---

### AC 3 — `PlayerStats` internal refactor + `Defense` property + `ApplyEquipmentBonuses`

Modify `Assets/_Game/Scripts/Player/PlayerStats.cs` (namespace `Game.Player`):

**Internal storage split (no public API change):**

```csharp
// Base values — initialized from config, permanently incremented by UpgradeStat()
private int _baseStrength, _baseDexterity, _baseEndurance, _baseMana;

// Equipment bonuses — replaced wholesale by ApplyEquipmentBonuses()
private int _equipStrBonus, _equipDexBonus, _equipEndBonus, _equipMnaBonus, _equipDefBonus;

// Effective values — computed properties; all callers (PlayerCombat, StaminaSystem, TrainerNPC)
// continue reading these unchanged and automatically receive base + equipment total.
public int Strength  => _baseStrength  + _equipStrBonus;
public int Dexterity => _baseDexterity + _equipDexBonus;
public int Endurance => _baseEndurance + _equipEndBonus;
public int Mana      => _baseMana      + _equipMnaBonus;

// New — no base defense; purely equipment-derived
public int Defense => _equipDefBonus;
```

**`Awake()` change:** initialize `_base*` fields instead of auto-properties:
```csharp
_baseStrength  = _config.baseStrength;
_baseDexterity = _config.baseDexterity;
_baseEndurance = _config.baseEndurance;
_baseMana      = _config.baseMana;
```

**`UpgradeStat()` change:** increments `_base*` fields (not auto-properties):
```csharp
case StatType.Strength:  _baseStrength  += points; break;
case StatType.Dexterity: _baseDexterity += points; break;
case StatType.Endurance: _baseEndurance += points; break;
case StatType.Mana:      _baseMana      += points; break;
```

**`StatType` enum:** add `Defense`:
```csharp
public enum StatType { Strength, Dexterity, Endurance, Mana, Defense }
```

**`GetStat()` update:** add `StatType.Defense => Defense,`

**New method:**
```csharp
/// <summary>
/// Replaces all equipment stat bonuses with the new totals.
/// Called by EquipmentSystem whenever the equipped loadout changes.
/// Raises _onStatsChanged so UI and systems refresh immediately.
/// </summary>
public void ApplyEquipmentBonuses(int str, int dex, int end, int mna, int def)
{
    _equipStrBonus = str;
    _equipDexBonus = dex;
    _equipEndBonus = end;
    _equipMnaBonus = mna;
    _equipDefBonus = def;
    GameLog.Info(TAG, $"Equipment bonuses applied — STR+{str} DEX+{dex} END+{end} MNA+{mna} DEF+{def}");
    _onStatsChanged?.Raise(true);
}
```

**`OnGUI` debug label** extended to show `DEF:{Defense}`.

**Critical — Zero-Breaking-Change:** `PlayerCombat.ComputeEffectiveDamage()` reads `_playerStats.Strength` — it now receives base + equipment strength automatically with no code change. Likewise `StaminaSystem.MaxStamina` reads `_playerStats.Endurance` — also automatic. Neither file needs modification for the stat bonus system.

---

### AC 4 — `EquipmentSystem` recomputes bonuses and notifies `PlayerStats`

Modify `Assets/_Game/Scripts/Inventory/EquipmentSystem.cs`:

**New serialized fields:**
```csharp
// TODO(Epic7-tech-debt): Cross-system direct refs (Game.Inventory → Game.Player).
// Prototype exception. Replace with a StatBonusProvider event channel.
[SerializeField] private PlayerStats _playerStats;
[SerializeField] private GameEventSO_Void _onEquipmentChanged; // already exists
```

**`Awake()` null-guard (warn only — not disabling):**
```csharp
if (_playerStats == null)
    GameLog.Warn(TAG, "PlayerStats not assigned — equipment stat bonuses inactive");
```

**New private helper:**
```csharp
private void RecomputeAndApplyBonuses()
{
    if (_playerStats == null) return;
    int str = 0, dex = 0, end = 0, mna = 0, def = 0;
    foreach (var item in _equipped.Values)
    {
        if (item is not EquipableItemSO eq) continue;
        str += eq.strengthBonus;
        dex += eq.dexterityBonus;
        end += eq.enduranceBonus;
        mna += eq.manaBonus;
        def += eq.defenseBonus;
    }
    _playerStats.ApplyEquipmentBonuses(str, dex, end, mna, def);
}
```

**Call order at end of `Equip()` and `Unequip()`:**
```
1. Mutate _equipped dictionary          ← already done
2. _onEquipmentChanged?.Raise(true)     ← EquipmentUI refreshes (already exists)
3. RecomputeAndApplyBonuses()           ← PlayerStats updated; _onStatsChanged fires
```

Step 3 must come after step 1 so the recompute sees the updated dictionary.

**`using Game.Player;`** directive required.

**New public accessors for weapon damage bonus** (queried by `PlayerCombat`):
```csharp
/// <summary>Flat damage bonus from the currently equipped weapon (0 if no weapon or damageBonus is 0).</summary>
public float GetWeaponDamageBonus()
    => GetEquipped(EquipmentSlot.Weapon) is WeaponSO w ? w.damageBonus : 0f;
```

Wire `_playerStats` on the `Player.prefab` `EquipmentSystem` component (same GO as `PlayerStats`).

---

### AC 5 — `PlayerCombat` integrates weapon `damageBonus`

Modify `Assets/_Game/Scripts/Combat/PlayerCombat.cs`:

**New serialized field:**
```csharp
// TODO(Epic7-tech-debt): Cross-system direct ref (Game.Combat → Game.Inventory).
// Prototype exception per Story 7.3 dev notes. Same pattern as existing _playerStats ref.
[SerializeField] private EquipmentSystem _equipmentSystem;
```

**`Awake()` null-guard (warn only — not disabling):**
```csharp
if (_equipmentSystem == null)
    GameLog.Warn(TAG, "EquipmentSystem not assigned — weapon damage bonus inactive");
```

**`ComputeEffectiveDamage()` — add weapon damage bonus:**
```csharp
private float ComputeEffectiveDamage()
{
    float damage = _config.attackDamage;
    if (_playerStats != null && _progressionConfig != null)
        damage += Mathf.Max(0f, (_playerStats.Strength - _progressionConfig.baseStrength)
                  * _progressionConfig.damagePerStrength);
    if (_playerSkills != null && _progressionConfig != null
        && _playerSkills.HasSkill("power_strike"))
        damage += _progressionConfig.powerStrikeDamageBonus;
    // Story 7.3: direct weapon damage bonus (flat, unscaled by stats).
    damage += _equipmentSystem?.GetWeaponDamageBonus() ?? 0f;
    return damage;
}
```

**`using Game.Inventory;`** directive required.

Wire `_equipmentSystem` on the `Player.prefab` `PlayerCombat` component (same GO as `EquipmentSystem`).

Note: `_playerStats.Strength` already reflects equipment strength bonus automatically via AC 3 — the `Strength * damagePerStrength` term handles stat-based weapon bonuses. `damageBonus` on `WeaponSO` is an additional flat damage bonus on top.

---

### AC 6 — `PlayerHealth` applies Defense reduction

Modify `Assets/_Game/Scripts/Player/PlayerHealth.cs`:

**New serialized field:**
```csharp
// TODO(Epic7-tech-debt): Cross-system direct ref (Game.Player → Game.Player).
// Same-system reference — PlayerHealth and PlayerStats both in Scripts/Player/. No architecture violation.
[SerializeField] private PlayerStats _playerStats;
```

**`Awake()` null-guard (warn only — not disabling):**
```csharp
if (_playerStats == null)
    GameLog.Warn(TAG, "PlayerStats not assigned — damage will not be reduced by Defense");
```

**`TakeDamage(float amount)` updated:**
```csharp
public void TakeDamage(float amount)
{
    if (IsDead) return;
    float defense = _playerStats != null ? _playerStats.Defense : 0f;
    float effective = Mathf.Max(1f, amount - defense);
    CurrentHealth -= effective;
    CurrentHealth = Mathf.Max(CurrentHealth, 0f);
    GameLog.Info(TAG, $"Player took {effective:F0} damage (raw {amount:F0} - def {defense:F0}) — HP: {CurrentHealth:F0}/{MaxHealth:F0}");
    if (CurrentHealth <= 0f) Die();
}
```

- **Formula:** `effective = max(1, raw - Defense)` — minimum 1 damage always gets through regardless of defense value (prevents invincibility via stacking)
- Defense is `int` cast to `float` in subtraction — promotes automatically, no explicit cast needed
- Wire `_playerStats` on `Player.prefab` `PlayerHealth` component (same GO as `PlayerStats`)

---

### AC 7 — `ItemDetailPanelUI` displays stat bonuses

Modify `Assets/_Game/Scripts/UI/ItemDetailPanelUI.cs`:

**New optional serialized fields** (add to `[Header("Equipment Type Label (optional)")]` block):
```csharp
[SerializeField] private TMP_Text _weaponDamageBonusText;  // child of WeaponSection
[SerializeField] private TMP_Text _equipableStatBonusText; // child of EquipableSection
```

**Update `ShowWeaponSection(WeaponSO item)`:**
```csharp
private void ShowWeaponSection(WeaponSO item)
{
    if (_equipableSection == null) return;
    _equipableSection.SetActive(true);
    if (_weaponSection == null) return;
    _weaponSection.SetActive(true);

    if (_weaponDamageBonusText != null)
    {
        bool hasDmgBonus = item.damageBonus > 0f;
        _weaponDamageBonusText.gameObject.SetActive(hasDmgBonus);
        if (hasDmgBonus) _weaponDamageBonusText.text = $"DMG: +{item.damageBonus:F0}";
    }
    ShowEquipableStatBonuses(item);
}
```

**Update `ShowArmorSection(ArmorSO item)`:**
```csharp
private void ShowArmorSection(ArmorSO item)
{
    if (_equipableSection == null) return;
    _equipableSection.SetActive(true);
    if (_armorSection == null) return;
    _armorSection.SetActive(true);
    if (_armorTypeText != null) _armorTypeText.text = ArmorSlotDisplayName(item.slot);
    ShowEquipableStatBonuses(item);
}
```

**New helper `ShowEquipableStatBonuses`:**
```csharp
private void ShowEquipableStatBonuses(EquipableItemSO item)
{
    if (_equipableStatBonusText == null) return;

    var sb = new System.Text.StringBuilder();
    if (item.strengthBonus  != 0) sb.AppendLine(FormatBonus("STR", item.strengthBonus));
    if (item.dexterityBonus != 0) sb.AppendLine(FormatBonus("DEX", item.dexterityBonus));
    if (item.enduranceBonus != 0) sb.AppendLine(FormatBonus("END", item.enduranceBonus));
    if (item.manaBonus      != 0) sb.AppendLine(FormatBonus("MNA", item.manaBonus));
    if (item.defenseBonus   != 0) sb.AppendLine(FormatBonus("DEF", item.defenseBonus));

    bool hasAny = sb.Length > 0;
    _equipableStatBonusText.gameObject.SetActive(hasAny);
    if (hasAny) _equipableStatBonusText.text = sb.ToString().TrimEnd();
}

private static string FormatBonus(string label, int value)
    => value > 0 ? $"{label}: +{value}" : $"{label}: {value}";
```

**Scene wiring:**
- Add `WeaponDamageBonusText` (TMP_Text GO) as child of `WeaponSection` in `ItemDetailPanel`
- Add `EquipableStatBonusText` (TMP_Text GO) as child of `EquipableSection` in `ItemDetailPanel`
- Wire both in `ItemDetailPanelUI` Inspector

Both fields are **optional** — if unassigned, the null guards skip display silently. `HideTypeSections()` deactivates the parent GOs (`_weaponSection`, `_equipableSection`), so child text GOs hide automatically — no extra hide calls needed.

---

### AC 8 — Test data assets

**Update `Assets/_Game/Data/Items/Weapon_TestSword.asset`:**
- `damageBonus = 5`
- `strengthBonus = 3`

**Update `Assets/_Game/Data/Items/Armor_TestHelmet.asset`:**
- `defenseBonus = 2`

**Update `Assets/_Game/Data/Items/Armor_TestArmor.asset`:**
- `defenseBonus = 5`, `enduranceBonus = 1`

With `staminaPerEndurance = 5`, equipping TestArmor adds `1 × 5 = 5` to MaxStamina via Endurance.
With `damagePerStrength = 2`, equipping TestSword adds `3 × 2 = 6` strength-based damage + `5` flat = `+11` total damage.

---

### AC 9 — Edit Mode tests

**`Assets/Tests/EditMode/EquipmentStatEffectsTests.cs`:**

```
ApplyEquipmentBonuses_UpdatesEffectiveStrength
    - Create PlayerStats stub; call ApplyEquipmentBonuses(str:2,...); assert Strength == baseStrength + 2

ApplyEquipmentBonuses_UpdatesDefense
    - Call ApplyEquipmentBonuses(def:5); assert Defense == 5

UpgradeStat_StillIncreasesBaseOnly_EquipmentUnaffected
    - Apply equipment bonuses (str:2); UpgradeStat(Strength, 3); assert Strength == baseStrength + 3 + 2

ApplyEquipmentBonuses_RaisesOnStatsChanged
    - Subscribe listener to _onStatsChanged; call ApplyEquipmentBonuses; assert listener fired

TakeDamage_ReducedByDefense
    - PlayerHealth stub with Defense=4; TakeDamage(10); assert HP reduced by 6

TakeDamage_MinimumOneDamage
    - Defense=10; TakeDamage(3); assert HP reduced by 1 (not 0 or negative)

TakeDamage_NoPlayerStats_UsesZeroDefense
    - _playerStats null; TakeDamage(10); assert HP reduced by 10, no crash

RecomputeBonuses_SumsAllEquippedItems
    - Equip WeaponSO(strengthBonus=2) + ArmorSO(defenseBonus=3) via EquipmentSystem
    - Assert PlayerStats.Strength == baseStrength + 2; PlayerStats.Defense == 3

GetWeaponDamageBonus_WithWeaponEquipped_ReturnsDamageBonus
    - WeaponSO.damageBonus=10; equip; assert GetWeaponDamageBonus() == 10f

GetWeaponDamageBonus_NoWeaponEquipped_ReturnsZero
    - Empty equipment; assert GetWeaponDamageBonus() == 0f
```

10 tests. File: `Assets/Tests/EditMode/EquipmentStatEffectsTests.cs`, namespace `Tests.EditMode`.

---

### AC 10 — Play Mode validation

- Equip `Weapon_TestSword` → debug HUD shows `STR` increased by 3, combat `DMG:` overlay increases by `6 + 5 = 11`
- Equip `Armor_TestHelmet` + `Armor_TestArmor` → debug HUD shows `DEF: 7`; `MaxStamina` increases by 5 (`enduranceBonus=1 × staminaPerEndurance=5`)
- Stand in front of enemy, let it attack → HP loss is `max(1, enemyDamage - 7)` per hit (visibly less than unarmored)
- Unequip all → stats/defense return to base; next hit does full damage
- Trainer upgrade STR +1 → `STR` increments on top of existing equipment bonus (both stack)
- Single-click Test Sword in detail panel → "DMG: +5", "STR: +3" visible in panel
- Single-click Test Helmet in detail panel → "DEF: +2" visible
- No regressions from stories 7-1, 7-2 (equip/unequip flow, double-click primary action, context menu equip, all 187 EditMode tests still pass)

## Tasks / Subtasks

- [ ] Task 1: Add stat/defense bonus fields to `EquipableItemSO` and `damageBonus` to `WeaponSO` (AC: 1, 2)
  - [ ] 1.1 Add `strengthBonus`, `dexterityBonus`, `enduranceBonus`, `manaBonus`, `defenseBonus` (int, default 0) to `EquipableItemSO.cs`
  - [ ] 1.2 Add `damageBonus` (float, default 0f) to `WeaponSO.cs`
  - [ ] 1.3 Verify compilation clean; all 187 existing EditMode tests still pass

- [ ] Task 2: Refactor `PlayerStats` for split base/equipment storage (AC: 3)
  - [ ] 2.1 Replace auto-property backing with `_base*` private fields
  - [ ] 2.2 Add `_equip*Bonus` private fields; make `Strength/Dexterity/Endurance/Mana` computed properties (`_base* + _equip*Bonus`)
  - [ ] 2.3 Add `Defense` computed property (`_equipDefBonus`)
  - [ ] 2.4 Add `Defense` to `StatType` enum; update `GetStat()` switch
  - [ ] 2.5 Update `Awake()` to initialize `_base*` fields
  - [ ] 2.6 Update `UpgradeStat()` to increment `_base*` fields
  - [ ] 2.7 Add `ApplyEquipmentBonuses(int str, int dex, int end, int mna, int def)` method
  - [ ] 2.8 Extend `OnGUI` debug label with `DEF:{Defense}`
  - [ ] 2.9 Verify compilation clean; `TrainerNPC`, `PlayerCombat`, `StaminaSystem` require zero modifications

- [ ] Task 3: Extend `EquipmentSystem` — bonus recompute + `_playerStats` ref + `GetWeaponDamageBonus` (AC: 4)
  - [ ] 3.1 Add `[SerializeField] private PlayerStats _playerStats;` with TODO comment; add `using Game.Player;`
  - [ ] 3.2 Add `Awake()` warn-only null-guard for `_playerStats`
  - [ ] 3.3 Implement `RecomputeAndApplyBonuses()` iterating `EquipableItemSO` base type
  - [ ] 3.4 Call `RecomputeAndApplyBonuses()` at end of `Equip()` and `Unequip()` (after existing `_onEquipmentChanged?.Raise`)
  - [ ] 3.5 Add public `GetWeaponDamageBonus()` method
  - [ ] 3.6 Wire `_playerStats` on `Player.prefab` `EquipmentSystem` component
  - [ ] 3.7 Verify compilation clean

- [ ] Task 4: Extend `PlayerCombat` — weapon damage bonus (AC: 5)
  - [ ] 4.1 Add `[SerializeField] private EquipmentSystem _equipmentSystem;` with TODO comment; add `using Game.Inventory;`
  - [ ] 4.2 Add `Awake()` warn-only null-guard for `_equipmentSystem`
  - [ ] 4.3 Update `ComputeEffectiveDamage()` to add `_equipmentSystem?.GetWeaponDamageBonus() ?? 0f`
  - [ ] 4.4 Wire `_equipmentSystem` on `Player.prefab` `PlayerCombat` component
  - [ ] 4.5 Verify compilation clean

- [ ] Task 5: Extend `PlayerHealth` — defense mitigation (AC: 6)
  - [ ] 5.1 Add `[SerializeField] private PlayerStats _playerStats;` (same-system, no violation)
  - [ ] 5.2 Add `Awake()` warn-only null-guard
  - [ ] 5.3 Update `TakeDamage()` with `effective = Mathf.Max(1f, amount - defense)` formula
  - [ ] 5.4 Wire `_playerStats` on `Player.prefab` `PlayerHealth` component
  - [ ] 5.5 Verify compilation clean

- [ ] Task 6: Update `ItemDetailPanelUI` for stat bonus display (AC: 7)
  - [ ] 6.1 Add `_weaponDamageBonusText` and `_equipableStatBonusText` optional TMP_Text fields
  - [ ] 6.2 Update `ShowWeaponSection()` to show damage bonus + call `ShowEquipableStatBonuses()`
  - [ ] 6.3 Update `ShowArmorSection()` to call `ShowEquipableStatBonuses()`
  - [ ] 6.4 Implement `ShowEquipableStatBonuses(EquipableItemSO)` with `System.Text.StringBuilder`
  - [ ] 6.5 Add `WeaponDamageBonusText` TMP GO as child of `WeaponSection` in scene
  - [ ] 6.6 Add `EquipableStatBonusText` TMP GO as child of `EquipableSection` in scene
  - [ ] 6.7 Wire both in `ItemDetailPanelUI` Inspector
  - [ ] 6.8 Verify compilation clean

- [ ] Task 7: Update test data assets (AC: 8)
  - [ ] 7.1 Set `Weapon_TestSword.asset`: `damageBonus = 5`, `strengthBonus = 3`
  - [ ] 7.2 Set `Armor_TestHelmet.asset`: `defenseBonus = 2`
  - [ ] 7.3 Set `Armor_TestArmor.asset`: `defenseBonus = 5`, `enduranceBonus = 1`

- [ ] Task 8: Write Edit Mode tests (AC: 9)
  - [ ] 8.1 Create `Assets/Tests/EditMode/EquipmentStatEffectsTests.cs`
  - [ ] 8.2 Implement 10 test methods per AC 9
  - [ ] 8.3 Verify all new + existing tests pass (187 + 10 = 197 target)

- [ ] Task 9: Play Mode validation (AC: 10)
  - [ ] 9.1 Manual in-editor validation per AC 10 checklist

## Dev Notes

### Zero-Breaking-Change Refactor of `PlayerStats` — The Critical Insight

`Strength`, `Dexterity`, `Endurance`, `Mana` remain public `int` properties with the same names. All existing callers:
- `PlayerCombat.ComputeEffectiveDamage()` — reads `_playerStats.Strength`; now receives base + equipment automatically
- `StaminaSystem.MaxStamina` — reads `_playerStats.Endurance`; now receives effective value automatically
- `TrainerNPC.cs` — calls `GetStat()`; returns effective value automatically

**Zero file modifications needed for these three callers** because of the computed property approach. This is the most elegant pattern: centralize the bonus addition in `PlayerStats` via `ApplyEquipmentBonuses()`, and all consumers just work.

---

### `damageBonus` on `WeaponSO` vs `strengthBonus` on `EquipableItemSO` — Two Distinct Systems

- `strengthBonus` (on EquipableItemSO) → flows into `PlayerStats.Strength` → scaled by `damagePerStrength` in `ComputeEffectiveDamage()`. Good for "this weapon makes you stronger" design.
- `damageBonus` (on WeaponSO) → queried directly via `EquipmentSystem.GetWeaponDamageBonus()` → added flat in `ComputeEffectiveDamage()`. Good for "this weapon deals more raw damage" design.

A weapon can have both; they stack. Authors can use either or both to create distinct weapon identities.

---

### `EquipableItemSO` Stat Fields — Future-Proof for New Equippable Types

`RecomputeAndApplyBonuses()` iterates `_equipped.Values` and checks `item is EquipableItemSO`. Any future ring of resistance or magic amulet that extends `EquipableItemSO` will automatically contribute stat bonuses without modifying `EquipmentSystem` — just author the SO fields.

Previous story used `if (item is WeaponSO) {...} else if (item is ArmorSO) {...}` — this story's approach is strictly better.

---

### Defense — Equipment-Only Stat, No Trainer Upgrade

`Defense` has no base value from `ProgressionConfigSO` (no `baseDefense` field exists or is needed). `_equipDefBonus` starts at 0 and is only non-zero when defensive gear is equipped. `StatType.Defense` is added to the enum for `GetStat()` completeness, but trainers will never offer a Defense upgrade unless explicitly authored in a future story.

---

### Call Order in `Equip()` / `Unequip()`

```
1. Mutate _equipped dictionary                    ← existing logic
2. Log info                                       ← existing
3. _onEquipmentChanged?.Raise(true)               ← EquipmentUI visual refresh (existing)
4. RecomputeAndApplyBonuses()                     ← NEW: PlayerStats updated; _onStatsChanged fires
```

Step 4 MUST come after step 1 so the recompute iterates the already-updated dictionary. The order of steps 3 and 4 is flexible — keeping 3 before 4 means UI refreshes first then stat overlay updates, which is the natural visual sequence.

---

### `TakeDamage` Formula — Integer Defense, Float Damage

`EnemyBrain` passes `_config.attackDamage` (float from `CombatConfigSO`) to `TakeDamage`. `PlayerStats.Defense` is `int`. The subtraction `amount - defense` promotes the int to float automatically — no explicit cast. `Mathf.Max(1f, ...)` ensures minimum 1 damage, preventing invincibility edge cases with high defense stacking.

---

### Test Class Pattern — Follow `EquipmentSystemTests.cs`

`EquipmentStatEffectsTests.cs` follows the established test double pattern:
- Create components via `new GameObject().AddComponent<T>()`
- Assign serialized fields via `SerializedObject` + `FindProperty` or via `ScriptableObject.CreateInstance<T>()`
- Tear down with `Object.DestroyImmediate()` in `[TearDown]`
- Tests that need `_playerStats` on `EquipmentSystem`: assign via SerializedObject before calling `Awake()` (which requires `GetComponent` from the same GO — create both on one GO)

For `PlayerStats` tests: create a `ProgressionConfigSO` stub (via `ScriptableObject.CreateInstance`), wire via SerializedObject, then call `Awake()` via reflection or just use the `ApplyEquipmentBonuses()` method directly (it doesn't require Awake to have run if base values can remain 0 for test purposes).

---

### Project Structure Notes

**Files to MODIFY:**
```
Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs     ← add stat/defense bonus fields
Assets/_Game/ScriptableObjects/Items/WeaponSO.cs            ← add damageBonus field
Assets/_Game/Scripts/Player/PlayerStats.cs                  ← split storage + ApplyEquipmentBonuses + Defense
Assets/_Game/Scripts/Inventory/EquipmentSystem.cs           ← add _playerStats, RecomputeAndApplyBonuses, GetWeaponDamageBonus
Assets/_Game/Scripts/Combat/PlayerCombat.cs                 ← add _equipmentSystem + GetWeaponDamageBonus in ComputeEffectiveDamage
Assets/_Game/Scripts/Player/PlayerHealth.cs                 ← add _playerStats + defense formula in TakeDamage
Assets/_Game/Scripts/UI/ItemDetailPanelUI.cs                ← add stat bonus display helpers + optional text fields
Assets/_Game/Prefabs/Player/Player.prefab                   ← wire _playerStats on EquipmentSystem + PlayerHealth; wire _equipmentSystem on PlayerCombat
Assets/_Game/Data/Items/Weapon_TestSword.asset              ← damageBonus=5, strengthBonus=3
Assets/_Game/Data/Items/Armor_TestHelmet.asset              ← defenseBonus=2
Assets/_Game/Data/Items/Armor_TestArmor.asset               ← defenseBonus=5, enduranceBonus=1
Assets/_Game/Scenes/TestScene.unity                         ← may change during play-mode validation
```

**Files to CREATE:**
```
Assets/Tests/EditMode/EquipmentStatEffectsTests.cs
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/Combat/StaminaSystem.cs                ← reads .Endurance; gets effective value automatically via AC 3
Assets/_Game/Scripts/AI/TrainerNPC.cs                       ← calls GetStat(); gets effective value automatically
Assets/_Game/Scripts/AI/EnemyBrain.cs                       ← calls TakeDamage(float); unchanged call site
Assets/_Game/Scripts/Inventory/InventorySystem.cs           ← unchanged
Assets/_Game/ScriptableObjects/Items/ArmorSO.cs             ← inherits bonus fields from EquipableItemSO; no changes needed
Assets/_Game/Scripts/UI/InventoryUI.cs                      ← unchanged
Assets/_Game/Scripts/UI/EquipmentUI.cs                      ← unchanged
Assets/Tests/EditMode/EquipmentSystemTests.cs               ← 7-1 tests still valid
Assets/Tests/EditMode/InventoryPrimaryActionTests.cs        ← 7-2 tests still valid
```

### References

- Story 7-1 — `EquipmentSystem.Equip/Unequip` API; `EquipableItemSO` hierarchy; `Player.prefab` component layout; `OnEquipmentChanged` event asset; existing test pattern in `EquipmentSystemTests.cs`
- Story 3.6 — `PlayerStats.UpgradeStat()` and `GetStat()` implementations; `StaminaSystem.MaxStamina` Endurance formula (unchanged in 7-3); `PlayerCombat.ComputeEffectiveDamage()` structure
- `Assets/_Game/ScriptableObjects/Items/CLAUDE.md` — EquipableItemSO hierarchy; `ItemDetailPanelUI` section pattern
- `Assets/_Game/Scripts/UI/CLAUDE.md` — StringBuilder rule; no per-frame string allocation
- `Assets/_Game/Scripts/Player/CLAUDE.md` — PlayerStats fields; PlayerHealth.TakeDamage location
- `_bmad-output/game-architecture.md` §Configuration Management — no magic numbers; §Cross-cutting Concerns Logging — `GameLog` mandatory
- `_bmad-output/project-context.md` — cross-system ref TODO convention; `[SerializeField] private` rule; `GameLog` mandatory; StatType in `Game.Player` namespace

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
