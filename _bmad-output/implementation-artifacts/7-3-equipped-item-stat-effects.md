# Story 7-3: Equipped Item Stat Effects + Defense

Status: ready-for-dev

## Story

As a player,
I want equipped weapons and armor to modify my character stats and reduce damage I take,
so that finding and equipping better gear has a meaningful mechanical impact on my character.

## Acceptance Criteria

### AC 1 — Stat bonus fields on `WeaponSO` and `ArmorSO`

Both `WeaponSO.cs` and `ArmorSO.cs` extended with the following serialized fields (all default to 0):

```csharp
public int strBonus;
public int dexBonus;
public int endBonus;
public int mnaBonus;
public int defBonus;
```

These fields are added to **both** types — weapons can carry stat bonuses, armor can carry defense (and optionally stat bonuses too).

No validation required beyond defaults: a weapon with `defBonus = 5` and all other bonuses 0 is valid.

---

### AC 2 — `PlayerStats` internal refactor + `Defense` + `ApplyEquipmentBonuses`

`PlayerStats.cs` refactored so the four existing public properties return **effective values** (base + equipment bonus). **Public API is unchanged** — callers (`PlayerCombat`, `StaminaSystem`, `TrainerNPC`) require zero modifications.

**Internal change:**

Replace direct auto-property backing with explicit base fields and equipment bonus fields:

```csharp
// Base values — initialized from config, permanently incremented by UpgradeStat()
private int _baseStrength, _baseDexterity, _baseEndurance, _baseMana;

// Equipment bonuses — recomputed by ApplyEquipmentBonuses(); reset to 0 when all unequipped
private int _equipStrBonus, _equipDexBonus, _equipEndBonus, _equipMnaBonus, _equipDefBonus;

// Effective values — what all callers read (unchanged public API)
public int Strength  => _baseStrength  + _equipStrBonus;
public int Dexterity => _baseDexterity + _equipDexBonus;
public int Endurance => _baseEndurance + _equipEndBonus;
public int Mana      => _baseMana      + _equipMnaBonus;

// New — no base defense; purely from equipment
public int Defense => _equipDefBonus;
```

**`Awake()` change:** initialize `_baseStrength = _config.baseStrength` etc. (instead of `Strength = _config.baseStrength`).

**`UpgradeStat()` change:** increments `_base*` fields instead of the properties:
```csharp
case StatType.Strength: _baseStrength += points; break;
// etc.
```

**New method:**

```csharp
/// <summary>
/// Replaces all equipment stat bonuses with the new totals.
/// Called by EquipmentSystem whenever the equipped loadout changes.
/// Raises _onStatsChanged so UI and systems refresh immediately.
/// </summary>
public void ApplyEquipmentBonuses(int str, int dex, int end, int mna, int def)
{
    _equipStrBonus  = str;
    _equipDexBonus  = dex;
    _equipEndBonus  = end;
    _equipMnaBonus  = mna;
    _equipDefBonus  = def;
    GameLog.Info(TAG, $"Equipment bonuses applied — STR+{str} DEX+{dex} END+{end} MNA+{mna} DEF+{def}");
    _onStatsChanged?.Raise(true);
}
```

**`GetStat()` change:** add `StatType.Defense` case:
```csharp
StatType.Defense => Defense,
```
Add `Defense` to the `StatType` enum (`EquipmentSlot.cs` is a separate file — add to existing `StatType` enum in `PlayerStats.cs`):
```csharp
public enum StatType { Strength, Dexterity, Endurance, Mana, Defense }
```

**`OnGUI` debug label** extended to show `DEF:{Defense}`.

---

### AC 3 — `EquipmentSystem` recomputes bonuses and notifies `PlayerStats`

`EquipmentSystem.cs` extended:

- `[SerializeField] private PlayerStats _playerStats;` — null-guard in `Awake()` logs warn only (not disabling — game is functional without stats)
- New private helper `RecomputeAndApplyBonuses()`:
  ```csharp
  private void RecomputeAndApplyBonuses()
  {
      if (_playerStats == null) return;
      int str = 0, dex = 0, end = 0, mna = 0, def = 0;
      foreach (var item in _equipped.Values)
      {
          if (item is WeaponSO w) { str += w.strBonus; dex += w.dexBonus; end += w.endBonus; mna += w.mnaBonus; def += w.defBonus; }
          else if (item is ArmorSO a) { str += a.strBonus; dex += a.dexBonus; end += a.endBonus; mna += a.mnaBonus; def += a.defBonus; }
      }
      _playerStats.ApplyEquipmentBonuses(str, dex, end, mna, def);
  }
  ```
- Called at the end of both `Equip()` and `Unequip()`, after `_onEquipmentChanged?.Raise(true)`

---

### AC 4 — `PlayerHealth` applies Defense reduction

`PlayerHealth.cs` extended:

- `[SerializeField] private PlayerStats _playerStats;` — null-guard in `Awake()` logs warn only (not disabling — damage still works, just unmitigated)
- `TakeDamage(float amount)` modified:
  ```csharp
  public void TakeDamage(float amount)
  {
      if (IsDead) return;
      float defense = _playerStats != null ? _playerStats.Defense : 0f;
      float effective = Mathf.Max(1f, amount - defense);
      CurrentHealth -= effective;
      CurrentHealth = Mathf.Max(CurrentHealth, 0f);
      GameLog.Info(TAG, $"Player took {effective:F0} damage (raw {amount:F0} - def {defense:F0}) — HP: {CurrentHealth:F0}/{_config.baseHealth:F0}");
      if (CurrentHealth <= 0f) Die();
  }
  ```
- **Formula:** `effective = max(1, raw - Defense)` — minimum 1 damage always gets through regardless of defense value

---

### AC 5 — Test data: 3 stat-bearing placeholder items updated

Update the 3 test assets created in story 7-1 with non-zero bonuses so stat effects can be validated in play mode:

- `Weapon_TestSword.asset` — `strBonus = 3`
- `Armor_TestHelmet.asset` — `defBonus = 2`
- `Armor_TestArmor.asset` — `defBonus = 5, endBonus = 1`

---

### AC 6 — Edit Mode tests

**`Assets/Tests/EditMode/EquipmentStatEffectsTests.cs`**:

- `ApplyEquipmentBonuses_UpdatesEffectiveStats` — set bonuses via `ApplyEquipmentBonuses(2,0,0,0,0)`; `Strength` returns `baseStrength + 2`
- `ApplyEquipmentBonuses_UpdatesDefense` — set `def = 5`; `Defense` returns 5
- `UpgradeStat_StillIncreasesBaseOnly` — `UpgradeStat(Strength, 3)`; `Strength = base + 3`; equip bonus unaffected
- `ApplyEquipmentBonuses_RaisesOnStatsChanged` — listener on `_onStatsChanged` fires after call
- `TakeDamage_ReducedByDefense` — `TakeDamage(10)` with `Defense = 4` → HP reduced by 6
- `TakeDamage_MinimumOneDamage` — `TakeDamage(3)` with `Defense = 10` → HP reduced by 1 (not 0 or negative)
- `TakeDamage_NoPlayerStats_UsesZeroDefense` — `_playerStats` null → `TakeDamage(10)` reduces HP by 10, no crash
- `RecomputeBonuses_SumsAllEquippedItems` — equip sword (str+2) + helmet (def+3); `Strength = base+2`, `Defense = 3`

---

### AC 7 — Play Mode validation

- Equip `Weapon_TestSword` → debug HUD shows `STR` increased by 3
- Equip `Armor_TestHelmet` + `Armor_TestArmor` → debug HUD shows `DEF: 7`
- Stand in front of enemy, let it attack → take `max(1, enemyDamage - 7)` HP per hit
- Unequip all armor → defense returns to 0; next hit does full damage
- Trainer stat upgrade still works: buy STR +1 → `STR` increments on top of existing equipment bonus
- All Edit Mode tests pass; no regressions from stories 7-1 and 7-2

## Tasks / Subtasks

- [ ] Task 1: Add stat bonus fields to `WeaponSO` and `ArmorSO` (AC: 1)
  - [ ] 1.1 Add `strBonus`, `dexBonus`, `endBonus`, `mnaBonus`, `defBonus` to `WeaponSO.cs`
  - [ ] 1.2 Add same fields to `ArmorSO.cs`
  - [ ] 1.3 Verified — compilation clean

- [ ] Task 2: Refactor `PlayerStats` (AC: 2)
  - [ ] 2.1 Replace auto-property backing with `_base*` + `_equip*Bonus` private fields
  - [ ] 2.2 Make `Strength`, `Dexterity`, `Endurance`, `Mana` computed properties (`_base* + _equip*Bonus`)
  - [ ] 2.3 Add `Defense` computed property
  - [ ] 2.4 Add `Defense` to `StatType` enum
  - [ ] 2.5 Update `Awake()` to initialize `_base*` fields
  - [ ] 2.6 Update `UpgradeStat()` to increment `_base*` fields
  - [ ] 2.7 Add `ApplyEquipmentBonuses()` method
  - [ ] 2.8 Update `GetStat()` to handle `StatType.Defense`
  - [ ] 2.9 Extend `OnGUI` debug label with `DEF:{Defense}`
  - [ ] 2.10 Verified — compilation clean; `TrainerNPC`, `PlayerCombat`, `StaminaSystem` unaffected

- [ ] Task 3: Extend `EquipmentSystem` (AC: 3)
  - [ ] 3.1 Add `[SerializeField] private PlayerStats _playerStats`
  - [ ] 3.2 Implement `RecomputeAndApplyBonuses()` private helper
  - [ ] 3.3 Call `RecomputeAndApplyBonuses()` at end of `Equip()` and `Unequip()`
  - [ ] 3.4 Wire `_playerStats` on Player prefab in Inspector
  - [ ] 3.5 Verified — compilation clean

- [ ] Task 4: Extend `PlayerHealth` (AC: 4)
  - [ ] 4.1 Add `[SerializeField] private PlayerStats _playerStats`
  - [ ] 4.2 Apply defense formula in `TakeDamage()`: `effective = Mathf.Max(1f, amount - defense)`
  - [ ] 4.3 Update log message to show raw/effective/defense breakdown
  - [ ] 4.4 Wire `_playerStats` on Player prefab in Inspector
  - [ ] 4.5 Verified — compilation clean

- [ ] Task 5: Update test data assets (AC: 5)
  - [ ] 5.1 Set `strBonus = 3` on `Weapon_TestSword.asset`
  - [ ] 5.2 Set `defBonus = 2` on `Armor_TestHelmet.asset`
  - [ ] 5.3 Set `defBonus = 5, endBonus = 1` on `Armor_TestArmor.asset`

- [ ] Task 6: Write Edit Mode tests (AC: 6)
  - [ ] 6.1 Create `Assets/Tests/EditMode/EquipmentStatEffectsTests.cs`
  - [ ] 6.2 Implement 8 test methods per AC 6

- [ ] Task 7: Play Mode validation (AC: 7)
  - [ ] 7.1 Manual in-editor validation per AC 7 checklist

## Dev Notes

### Zero-Breaking-Change Refactor of `PlayerStats`

The key insight is that `Strength`, `Dexterity`, `Endurance`, and `Mana` stay as public `int` properties with the exact same names. Callers (`PlayerCombat.cs` line 285, `StaminaSystem.cs` line 37, `TrainerNPC.cs` line 93) read these properties and will automatically receive the effective (base + equipment) value without any modification.

The only internal change is that the backing storage splits into `_base*` + `_equip*Bonus`. `UpgradeStat()` increments `_base*` — so a trainer upgrade to STR adds to the base, and equipped items add on top of that. Both stack correctly.

---

### Defense — Equipment-Only Stat

`Defense` has no base value and no trainer upgrade path (no `StatType.Defense` upgrade at trainers — Defense is purely equipment-derived). The `StatType.Defense` addition to the enum is for `GetStat()` completeness only. `TrainerNPC` will never offer a Defense upgrade unless explicitly authored.

---

### Damage Formula — Integer Defense, Float Damage

`EnemyBrain` passes `_config.attackDamage` (a `float` from `CombatConfigSO`) to `TakeDamage`. `PlayerStats.Defense` is an `int`. The subtraction `amount - defense` promotes `defense` to float automatically — no cast needed. The `Mathf.Max(1f, ...)` result is a float, which is correct for `CurrentHealth -= effective`.

---

### `EquipmentSystem` — Call Order in `Equip()` / `Unequip()`

```
1. Mutate _equipped dictionary
2. _onEquipmentChanged?.Raise(true)    ← EquipmentUI refreshes
3. RecomputeAndApplyBonuses()           ← PlayerStats updated, _onStatsChanged fires
```

Step 3 must come after step 1 so the recompute iterates the already-updated dictionary. Step 2 can come before or after 3 — both are fine for prototype. Order as written above is preferred (UI update then stat update).

---

### Project Structure Notes

**Files to MODIFY:**
```
Assets/_Game/ScriptableObjects/Items/WeaponSO.cs           ← add 5 bonus fields
Assets/_Game/ScriptableObjects/Items/ArmorSO.cs            ← add 5 bonus fields
Assets/_Game/Scripts/Player/PlayerStats.cs                 ← refactor internals, add Defense + ApplyEquipmentBonuses
Assets/_Game/Scripts/Inventory/EquipmentSystem.cs          ← add PlayerStats ref + RecomputeAndApplyBonuses
Assets/_Game/Scripts/Player/PlayerHealth.cs                ← add PlayerStats ref + defense formula in TakeDamage
Assets/_Game/Data/Items/Weapon_TestSword.asset             ← set strBonus = 3
Assets/_Game/Data/Items/Armor_TestHelmet.asset             ← set defBonus = 2
Assets/_Game/Data/Items/Armor_TestArmor.asset              ← set defBonus = 5, endBonus = 1
Assets/_Game/Prefabs/Player/Player.prefab                  ← wire _playerStats on EquipmentSystem + PlayerHealth
```

**Files to CREATE:**
```
Assets/Tests/EditMode/EquipmentStatEffectsTests.cs
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/Combat/PlayerCombat.cs     ← reads .Strength; gets effective value automatically
Assets/_Game/Scripts/Combat/StaminaSystem.cs    ← reads .Endurance; gets effective value automatically
Assets/_Game/Scripts/AI/TrainerNPC.cs           ← calls GetStat(); gets effective value automatically
Assets/_Game/Scripts/AI/EnemyBrain.cs           ← calls TakeDamage(float); unchanged call site
```

### References

- Story 7-1 — `EquipmentSystem` structure, `WeaponSO`/`ArmorSO` base definitions
- Story 3.6 — `PlayerStats` existing `UpgradeStat()` and `GetStat()` implementation
- `_bmad-output/gdd.md` §Base Stats — STR/DEX/END/MNA definitions, Gothic-style stat design
- `project-context.md` — PlayerStats is MonoBehaviour on Player prefab; GameEventSO pattern

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
