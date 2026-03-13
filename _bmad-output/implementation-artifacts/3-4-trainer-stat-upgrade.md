# Story 3.4: Trainer Stat Upgrade

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to spend learning points and gold at a trainer NPC to raise my character stats,
so that I can permanently improve my combat capabilities and feel the earned power fantasy.

## Acceptance Criteria

1. **`PlayerStats.cs`** exists at `Assets/_Game/Scripts/Player/PlayerStats.cs`:
   - `namespace Game.Player`, `private const string TAG = "[Progression]";`
   - `[SerializeField] private ProgressionConfigSO _config`
   - `[SerializeField] private GameEventSO_Void _onStatsChanged` — raised when any stat changes
   - Public int properties: `Strength`, `Dexterity`, `Endurance`, `Mana` — each initialized from config base values in `Awake`
   - `Awake()`: null-guard `_config` (error + `enabled = false`)
   - `public void UpgradeStat(StatType stat, int points)` — increments the stat, logs it, raises `_onStatsChanged`
   - `OnGUI` debug overlay at y=350:
     - `$"STR:{Strength} DEX:{Dexterity} END:{Endurance} MNA:{Mana}"`
     - `fontSize = 18`, cached `GUIStyle _guiStyle`

2. **`StatType` enum** defined in `PlayerStats.cs` (same file, same namespace):
   ```csharp
   public enum StatType { Strength, Dexterity, Endurance, Mana }
   ```

3. **`GoldSystem.cs`** exists at `Assets/_Game/Scripts/Economy/GoldSystem.cs`:
   - `namespace Game.Economy`, `private const string TAG = "[Economy]";`
   - `[SerializeField] private int _startingGold = 500` (test scaffolding default — inspector-overridable)
   - `[SerializeField] private GameEventSO_Int _onGoldChanged` — raised on gold change (payload = new total)
   - `public int Gold { get; private set; }` — initialized to `_startingGold` in `Awake`
   - `Awake()`: null-guard warn on `_onGoldChanged` only (missing gold event is non-fatal)
   - `public bool TrySpend(int amount)` — returns false if insufficient; deducts + logs + raises event if sufficient
   - `public void Add(int amount)` — adds gold, logs, raises event
   - `OnGUI` debug overlay at y=370: `$"Gold: {Gold}"`, `fontSize = 18`, cached `_guiStyle`

4. **`ProgressionConfigSO.cs`** is updated with stat base values (new header at the bottom):
   ```csharp
   [Header("Base Stats — Story 3.4")]
   [Tooltip("Starting value for each base stat.")]
   public int baseStrength = 5;
   public int baseDexterity = 5;
   public int baseEndurance = 5;
   public int baseMana = 5;
   ```
   `ProgressionConfig.asset` is **NOT recreated** — Unity Inspector will auto-populate new fields with default values.

5. **`TrainerSO.cs`** exists at `Assets/_Game/ScriptableObjects/NPC/TrainerSO.cs`:
   - `namespace Game.NPC`, `CreateAssetMenu(menuName = "Game/NPC/Trainer")`
   - `public string trainerName`
   - `public StatUpgradeEntry[] upgrades` — array of available stat upgrades
   - `StatUpgradeEntry` is a `[System.Serializable]` struct defined in the same file:
     ```csharp
     [System.Serializable]
     public struct StatUpgradeEntry
     {
         public StatType stat;       // Which stat to upgrade
         public string label;        // Display label (e.g. "Strength +1")
         public int lpCost;          // LP cost per upgrade
         public int goldCost;        // Gold cost per upgrade
         public int maxLevel;        // Max upgrades purchasable (default: 5)
     }
     ```

6. **`Trainer_Master.asset`** exists at `Assets/_Game/Data/NPCs/Trainer_Master.asset`:
   - Type: `TrainerSO`
   - `trainerName = "Master Trainer"`
   - `upgrades` array (4 entries):
     | stat | label | lpCost | goldCost | maxLevel |
     |---|---|---|---|---|
     | Strength | Strength +1 | 1 | 100 | 5 |
     | Dexterity | Dexterity +1 | 1 | 100 | 5 |
     | Endurance | Endurance +1 | 1 | 100 | 5 |
     | Mana | Mana +1 | 1 | 100 | 5 |

7. **`TrainerNPC.cs`** exists at `Assets/_Game/Scripts/AI/TrainerNPC.cs`:
   - `namespace Game.AI`, `private const string TAG = "[NPC]";`
   - `[SerializeField] private TrainerSO _trainerData`
   - `[SerializeField] private float _interactionRadius = 3f`
   - **Prototype cross-system direct refs** (Inspector-assigned, see Dev Notes):
     - `[SerializeField] private LearningPointSystem _lpSystem`
     - `[SerializeField] private GoldSystem _goldSystem`
     - `[SerializeField] private PlayerStats _playerStats`
   - `Awake()`: null-guard all four references (error + `enabled = false` if any missing)
   - **Proximity detection:** `Update()` checks `Vector3.Distance(transform.position, playerTransform)` ≤ `_interactionRadius`. Requires `[SerializeField] private Transform _playerTransform` reference.
   - **Input:** uses a local `InputSystem_Actions _input` instance (`Awake`: `new InputSystem_Actions()`, `OnEnable`: `_input.Player.Enable()`, `OnDisable`: `_input.Player.Disable()`)
   - When player is in range + `_input.Player.Interact.WasPressedThisFrame()` → toggle `_menuOpen` bool
   - **`_purchaseCounts[]`** int array (length = upgrades.Length): tracks how many times each stat has been upgraded, initialized in `Awake`
   - **`OnGUI()`** (DEVELOPMENT_BUILD || UNITY_EDITOR):
     - When `_menuOpen == true`, renders centered trainer menu:
       - Title: `$"=== {_trainerData.trainerName} ==="`
       - Shows: `LP: {_lpSystem.CurrentLP}` and `Gold: {_goldSystem.Gold}`
       - For each upgrade: shows `[label] LP:{lpCost} G:{goldCost} ({_purchaseCounts[i]}/{maxLevel})` — grayed out if maxLevel reached or insufficient LP/gold
       - Number keys (1-4) or button click to select upgrade
       - `[E] to close`
     - When player in range but menu closed: shows `"Press E to train"`
   - **`TryPurchaseUpgrade(int index)`**: validates count < maxLevel AND LP >= cost AND gold >= cost → calls `_lpSystem.TrySpendLP(lpCost)` → calls `_goldSystem.TrySpend(goldCost)` → calls `_playerStats.UpgradeStat(stat, 1)` → increments `_purchaseCounts[index]` → logs success
   - Input number keys 1-4 in `OnGUI()` trigger `TryPurchaseUpgrade(0-3)` via `Event.current.keyCode`

8. **`LearningPointSystem.cs`** is updated with `TrySpendLP` method (new public method, no other changes):
   ```csharp
   /// <summary>
   /// Attempts to spend LP. Returns false if insufficient. Called by TrainerNPC (Story 3.4).
   /// </summary>
   public bool TrySpendLP(int cost)
   {
       if (cost <= 0 || CurrentLP < cost) return false;
       CurrentLP -= cost;
       GameLog.Info(TAG, $"Spent {cost} LP. Remaining: {CurrentLP}");
       _onLPChanged?.Raise(CurrentLP);
       return true;
   }
   ```

9. **`OnGoldChanged.asset`** created at `Assets/_Game/Data/Events/OnGoldChanged.asset` (type: `GameEventSO_Int`)
   **`OnStatsChanged.asset`** created at `Assets/_Game/Data/Events/OnStatsChanged.asset` (type: `GameEventSO_Void`)

10. **Scene wiring in `TestScene.unity`**:
    - `PlayerStats` component added to **Player GO** (root) with:
      - `_config = ProgressionConfig.asset`, `_onStatsChanged = OnStatsChanged.asset`
    - `GoldSystem` component added to **ProgressionSystem GO** with:
      - `_startingGold = 500`, `_onGoldChanged = OnGoldChanged.asset`
    - New **`Trainer_Master` GO** placed in the scene (near spawn point, ~5 units from player start):
      - `TrainerNPC` component with all references assigned:
        - `_trainerData = Trainer_Master.asset`
        - `_lpSystem` → LearningPointSystem on ProgressionSystem GO
        - `_goldSystem` → GoldSystem on ProgressionSystem GO
        - `_playerStats` → PlayerStats on Player GO
        - `_playerTransform` → Player GO transform
      - `SphereCollider` trigger (radius 3) for visual feedback only (not used by code — distance check in Update)

11. **Edit Mode tests** at `Assets/Tests/EditMode/TrainerTransactionTests.cs` with ≥ 7 tests:
    - Pure logic helpers (no MonoBehaviour):
      ```csharp
      private bool CanAffordUpgrade(int currentLP, int currentGold, int lpCost, int goldCost)
          => currentLP >= lpCost && currentGold >= goldCost;

      private int SimulateStat(int baseStat, int upgradeCount)
          => baseStat + upgradeCount;

      private bool IsAtMaxLevel(int purchaseCount, int maxLevel)
          => purchaseCount >= maxLevel;
      ```
    - Tests:
      - `CanAfford_WhenBothSufficient_ReturnsTrue()` — LP=5, Gold=500, lpCost=1, goldCost=100 → true
      - `CannotAfford_WhenLPInsufficient_ReturnsFalse()` — LP=0, Gold=500 → false
      - `CannotAfford_WhenGoldInsufficient_ReturnsFalse()` — LP=5, Gold=50 → false
      - `CannotAfford_WhenBothInsufficient_ReturnsFalse()` — LP=0, Gold=0 → false
      - `StatUpgrade_IncrementsFromBase()` — base=5, 1 upgrade → 6
      - `StatUpgrade_AccumulatesCorrectly()` — base=5, 3 upgrades → 8
      - `MaxLevel_BlocksUpgrade_AtCap()` — count=5, maxLevel=5 → IsAtMaxLevel=true
      - `MaxLevel_AllowsUpgrade_BelowCap()` — count=4, maxLevel=5 → IsAtMaxLevel=false

12. No compile errors. All existing 73 Edit Mode tests pass. New total ≥ 81.

13. **Play Mode validation**:
    - Start in TestScene, walk near the Trainer_Master GO → "Press E to train" appears
    - Press E → trainer menu opens showing `STR +1 (LP:1, G:100)` etc., current LP and Gold
    - Purchase Strength +1 → STR overlay at y=350 changes from 5 to 6, LP overlay shows LP-1, Gold shows 490
    - Purchase again × 4 → STR=10, count shows 5/5, option grayed out
    - Attempt to purchase when LP=0 → no change, no crash
    - Console shows `[NPC] Trainer purchased: Strength +1. STR now 6.`
    - No NullReferenceExceptions

## Tasks / Subtasks

- [x] Task 1: Create `PlayerStats.cs` (AC: 1, 2)
  - [x] 1.1 Create `Assets/_Game/Scripts/Player/PlayerStats.cs` — namespace, TAG, fields
  - [x] 1.2 Implement `Awake` null-guard + initialize stats from config base values
  - [x] 1.3 Implement `UpgradeStat(StatType, int)` + logging + raise `_onStatsChanged`
  - [x] 1.4 Add `OnGUI` overlay at y=350 with cached `_guiStyle`
  - [x] 1.5 Define `StatType` enum in same file

- [x] Task 2: Update `ProgressionConfigSO.cs` with stat base values (AC: 4)
  - [x] 2.1 Add `[Header("Base Stats — Story 3.4")]` block with 4 int fields (default = 5)

- [x] Task 3: Create `GoldSystem.cs` (AC: 3)
  - [x] 3.1 Create `Assets/_Game/Scripts/Economy/GoldSystem.cs` — namespace, TAG, fields
  - [x] 3.2 Implement `Awake` + `TrySpend` + `Add` with logging and `_onGoldChanged` raises
  - [x] 3.3 Add `OnGUI` overlay at y=370 with cached `_guiStyle`

- [x] Task 4: Create `TrainerSO.cs` and data asset (AC: 5, 6)
  - [x] 4.1 Create `Assets/_Game/ScriptableObjects/NPC/TrainerSO.cs` with `StatUpgradeEntry` struct
  - [x] 4.2 Create `Assets/_Game/Data/NPCs/Trainer_Master.asset` with 4 stat upgrade entries

- [x] Task 5: Update `LearningPointSystem.cs` with `TrySpendLP` (AC: 8)
  - [x] 5.1 Add `public bool TrySpendLP(int cost)` method with null-guard, LP check, deduct, log, raise event

- [x] Task 6: Create event SOs (AC: 9)
  - [x] 6.1 Create `Assets/_Game/Data/Events/OnGoldChanged.asset` (GameEventSO_Int)
  - [x] 6.2 Create `Assets/_Game/Data/Events/OnStatsChanged.asset` (GameEventSO_Void)

- [x] Task 7: Create `TrainerNPC.cs` (AC: 7)
  - [x] 7.1 Create `Assets/_Game/Scripts/AI/TrainerNPC.cs` — fields, `Awake` null-guards, InputSystem_Actions init
  - [x] 7.2 Implement proximity check in `Update` + `_input.Player.Interact.WasPressedThisFrame()` toggle
  - [x] 7.3 Implement `TryPurchaseUpgrade(int index)` transaction logic
  - [x] 7.4 Implement `OnGUI` trainer menu with number key input
  - [x] 7.5 Implement `OnEnable`/`OnDisable` for input + null guards

- [x] Task 8: Wire scene (AC: 10)
  - [x] 8.1 Add `PlayerStats` component to Player GO in TestScene — assign `_config`, `_onStatsChanged`
  - [x] 8.2 Add `GoldSystem` component to ProgressionSystem GO — assign `_startingGold`, `_onGoldChanged`
  - [x] 8.3 Create `Trainer_Master` GO in TestScene — add `TrainerNPC`, assign all refs, place near player start

- [x] Task 9: Edit Mode tests (AC: 11)
  - [x] 9.1 Create `Assets/Tests/EditMode/TrainerTransactionTests.cs` with ≥ 8 tests
  - [x] 9.2 Run all tests — verify 73 prior tests pass + 8 new = ≥ 81 total; actual: 82/82 passed

- [x] Task 10: Play Mode validation (AC: 13) — requires Unity Editor
  - [x] 10.1 Walk to trainer, press E, verify menu opens
  - [x] 10.2 Purchase Strength upgrade, verify STR/LP/Gold overlays update
  - [x] 10.3 Verify max level blocking at 5 purchases
  - [x] 10.4 Verify no NullReferenceExceptions anywhere

## Dev Notes

Story 3.4 introduces the first NPC interaction and gold system, completing the "earn LP → spend at trainer" loop from Epic 3. The implementation is deliberately prototype-scoped: the trainer menu uses OnGUI, the interaction uses proximity+E key, and direct serialized refs bridge the system boundary. These will be superseded by Epic 5's Dialogue system.

### Critical: TrySpendLP Must Be Atomic — Check Before Spending Gold

The trainer transaction must be atomic: either both LP and gold are spent, or neither is. Because `TrySpendLP` is called first, you must check LP availability BEFORE calling `TrySpend(goldCost)` on GoldSystem.

**Correct order in `TryPurchaseUpgrade`:**
```csharp
private void TryPurchaseUpgrade(int index)
{
    var entry = _trainerData.upgrades[index];
    if (_purchaseCounts[index] >= entry.maxLevel)
    {
        GameLog.Warn(TAG, $"{entry.label}: already at max level");
        return;
    }
    // Check both BEFORE spending either (atomicity guard)
    if (_lpSystem.CurrentLP < entry.lpCost)
    {
        GameLog.Warn(TAG, $"{entry.label}: insufficient LP ({_lpSystem.CurrentLP}/{entry.lpCost})");
        return;
    }
    if (_goldSystem.Gold < entry.goldCost)
    {
        GameLog.Warn(TAG, $"{entry.label}: insufficient gold ({_goldSystem.Gold}/{entry.goldCost})");
        return;
    }
    // Both checks passed — spend atomically
    _lpSystem.TrySpendLP(entry.lpCost);
    _goldSystem.TrySpend(entry.goldCost);
    _playerStats.UpgradeStat(entry.stat, 1);
    _purchaseCounts[index]++;
    GameLog.Info(TAG, $"Trainer purchased: {entry.label}. {entry.stat} now {_playerStats.GetStat(entry.stat)}.");
}
```

### Critical: Prototype Cross-System Direct References (Architecture Note)

`TrainerNPC.cs` (in `Scripts/AI/`) holds direct serialized references to `LearningPointSystem` (`Scripts/Progression/`), `GoldSystem` (`Scripts/Economy/`), and `PlayerStats` (`Scripts/Player/`). This violates the cross-system boundary rule from the architecture:

> "Scripts/[System]/ scripts must NEVER directly reference another system's scripts"

This is an **intentional prototype pragmatism** for Epic 3. Rationale:
- The full Dialogue/NPC system (Epic 5) will replace `TrainerNPC.cs` entirely
- Building a full event-request-response pattern for a temporary prototype component creates unnecessary complexity
- The architecture rule primarily targets production systems (combat, AI, UI) — not short-lived prototype coordinators

**Tech debt:** In Epic 5, replace `TrainerNPC.cs` with proper `NPCInteraction.cs` + `DialogueSystem.cs` + `TopicUnlockEvaluator.cs` pattern. LP spending will flow through the dialogue system's consequence system.

### Critical: InputSystem_Actions in TrainerNPC

`TrainerNPC.cs` creates its own `InputSystem_Actions` instance (same pattern as `PlayerController`). This is intentional — each component owns its input lifecycle.

```csharp
private InputSystem_Actions _input;

private void Awake()
{
    // ... other Awake logic ...
    _input = new InputSystem_Actions();
}

private void OnEnable()
{
    if (_input == null) return; // Guard: Awake may disable before OnEnable
    _input.Player.Enable();
}

private void OnDisable()
{
    if (_input == null) return; // Guard: Awake may disable before OnEnable
    _input.Player.Disable();
    _input.Dispose();
}
```

Note the `OnDisable` null guard from CLAUDE.md "Unity Lifecycle Gotcha". If `Awake` disables the component due to a missing reference, `_input` is never assigned and `OnDisable` must guard against it.

### Critical: PlayerStats.cs Lives in Scripts/Player/, NOT Scripts/Progression/

Even though stats are unlocked through progression, `PlayerStats.cs` is a **player data container** and belongs in `Scripts/Player/` alongside `PlayerHealth.cs`. The progression system raises the stat (via `TrainerNPC` calling `UpgradeStat`), but the stat values belong to the player entity.

`StatType` enum lives inside `PlayerStats.cs` in `namespace Game.Player`. `TrainerNPC.cs` will need `using Game.Player;` to reference it.

### Critical: GoldSystem.cs vs. Epic 6 GoldSystem

This `GoldSystem.cs` is intentionally minimal — a test stub with `_startingGold = 500` hardcoded in the inspector. Epic 6 will replace it with the full `GoldSystem` that handles looting, shops, and bribe payments. The interface (`TrySpend`, `Add`, `Gold` property) is designed to be compatible so Epic 6 can expand on this foundation without breaking Story 3.4's wiring.

### Critical: ProgressionConfigSO Base Stats — Do NOT Modify ProgressionConfig.asset Manually

Adding 4 new fields to `ProgressionConfigSO.cs` will add them to `ProgressionConfig.asset` automatically when Unity reimports. The default values (`baseStrength = 5`, etc.) will be serialized on first domain reload. Do NOT manually recreate `ProgressionConfig.asset` — this would lose the existing XP/level/LP configuration.

### Critical: GoldSystem Scripts/Economy/ Folder Must Exist

Check if `Assets/_Game/Scripts/Economy/` exists before creating `GoldSystem.cs`. The architecture defines it, but if it's missing, create the folder and `.meta` file first. Similarly for `Scripts/AI/` (should already exist from Epic 2's `EnemyBrain.cs`).

### Critical: OnGUI Number Key Input Pattern

For the trainer menu number key input:
```csharp
private void OnGUI()
{
    if (!_menuOpen) return;
    // ... draw menu ...
    Event e = Event.current;
    if (e.type == EventType.KeyDown)
    {
        if (e.keyCode == KeyCode.Alpha1) TryPurchaseUpgrade(0);
        else if (e.keyCode == KeyCode.Alpha2) TryPurchaseUpgrade(1);
        else if (e.keyCode == KeyCode.Alpha3) TryPurchaseUpgrade(2);
        else if (e.keyCode == KeyCode.Alpha4) TryPurchaseUpgrade(3);
    }
}
```
`Event.current` is only valid inside `OnGUI()` — do NOT use it in `Update()`.

### Critical: _purchaseCounts Initialized in Awake

`_purchaseCounts` must be sized to `_trainerData.upgrades.Length` in `Awake`. If `_trainerData` is null, the Awake null-guard will disable the component before this runs — so the array init can happen after the null check:

```csharp
private void Awake()
{
    if (_trainerData == null || _lpSystem == null || _goldSystem == null || _playerStats == null || _playerTransform == null)
    {
        GameLog.Error(TAG, "TrainerNPC: required reference(s) not assigned — component disabled");
        enabled = false;
        return;
    }
    _purchaseCounts = new int[_trainerData.upgrades.Length];
    _input = new InputSystem_Actions();
}
```

### Critical: PlayerStats.GetStat Must Exist for Logging

`TrainerNPC.cs` calls `_playerStats.GetStat(entry.stat)` for log output. Implement `GetStat` in `PlayerStats.cs`:

```csharp
public int GetStat(StatType stat)
{
    return stat switch
    {
        StatType.Strength  => Strength,
        StatType.Dexterity => Dexterity,
        StatType.Endurance => Endurance,
        StatType.Mana      => Mana,
        _ => 0
    };
}
```

### OnGUI Overlay Stack After This Story

```
[y=50]   Stamina: 80 / 100                                                  ← StaminaSystem
[y=70]   Combat: [Ready]                                                    ← PlayerCombat
[y=100]  Combo: step 0 | closed                                             ← PlayerCombat
[y=130]  Block: lowered | PB: closed                                        ← PlayerCombat
[y=160]  State: Airborne:False | Blocking:False | Attacking:False           ← PlayerStateManager
[y=190]  Dodge: ready | CanDodge:True                                       ← DodgeController
[y=220]  Enemy: Patrolling | PlayerDist:10.2m | ...                         ← EnemyBrain
[y=250]  PlayerHP: 85/100 | Dead:False                                      ← PlayerHealth
[y=270]  EnemyHP: 50/50 | Dead:False                                        ← EnemyHealth
[y=290]  XP: 50 | Kills: 1                                                  ← XPSystem
[y=310]  Level: 1 / 6 | Next LvUp: 100 XP                                  ← LevelSystem
[y=330]  LP: 0                                                              ← LearningPointSystem
[y=350]  STR:5 DEX:5 END:5 MNA:5                                           ← PlayerStats (NEW)
[y=370]  Gold: 500                                                          ← GoldSystem (NEW)
```

TrainerNPC's menu appears only when trainer is active — it's a centered menu overlay, not a corner label, so no y-position conflict.

### Architecture Compliance Checklist

| Rule | This Story |
|------|-----------|
| All code under `Assets/_Game/` | ✅ All new files in correct folders |
| GameLog only — no Debug.Log | ✅ `GameLog.Info/Warn/Error` with `[NPC]`, `[Economy]`, `[Progression]` TAGs |
| Null-guard in Awake | ✅ All 4 new components null-guard their required refs |
| Config SOs for tunable values | ✅ `ProgressionConfigSO` for base stats; `_startingGold` is `[SerializeField]` |
| No cross-system direct refs (except noted) | ⚠️ TrainerNPC prototype exception — documented as tech debt |
| Event subscribe in OnEnable/OnDisable | ✅ N/A (no event subscriptions in new components; existing LearningPointSystem unchanged) |
| No magic numbers | ✅ Default costs in TrainerSO asset; base values in config |
| GUIStyle cached | ✅ Per-component `_guiStyle` initialized on first `OnGUI` call |
| OnDisable null guard for InputSystem | ✅ TrainerNPC: `if (_input == null) return;` |
| `StatType` in correct namespace | ✅ `namespace Game.Player` — TrainerNPC imports `using Game.Player;` |

### Previous Story Learnings (Story 3.3)

- `LearningPointSystem.cs` is confirmed at `Assets/_Game/Scripts/Progression/LearningPointSystem.cs`. `TrySpendLP` is NOT yet implemented — add it in Task 5.
- `OnLPChanged.asset` (GameEventSO_Int) is live and raises on LP change. `TrySpendLP` must also raise it.
- Test pattern: pure formula helpers, no MonoBehaviour instantiation. The `TrainerTransactionTests.cs` must follow this (test `CanAffordUpgrade`, `SimulateStat` as pure helper methods).
- 73 Edit Mode tests total as of Story 3.3. Code review corrected dev agent's initial claim of 74.
- The `ProgressionSystem` GO in `TestScene.unity` already has `XPSystem`, `LevelSystem`, `LearningPointSystem` — add `GoldSystem` here too. The Player GO already has `PlayerController`, `PlayerAnimator`, `PlayerHealth` — add `PlayerStats` there.

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Scripts/Player/PlayerStats.cs                            ← NEW stat container
Assets/_Game/Scripts/Player/PlayerStats.cs.meta
Assets/_Game/Scripts/Economy/GoldSystem.cs                           ← NEW minimal gold tracker
Assets/_Game/Scripts/Economy/GoldSystem.cs.meta
Assets/_Game/Scripts/AI/TrainerNPC.cs                                ← NEW trainer interaction
Assets/_Game/Scripts/AI/TrainerNPC.cs.meta
Assets/_Game/ScriptableObjects/NPC/TrainerSO.cs                      ← NEW trainer data SO
Assets/_Game/ScriptableObjects/NPC/TrainerSO.cs.meta
Assets/_Game/Data/NPCs/Trainer_Master.asset                          ← NEW trainer data instance
Assets/_Game/Data/NPCs/Trainer_Master.asset.meta
Assets/_Game/Data/Events/OnGoldChanged.asset                         ← NEW event SO (GameEventSO_Int)
Assets/_Game/Data/Events/OnGoldChanged.asset.meta
Assets/_Game/Data/Events/OnStatsChanged.asset                        ← NEW event SO (GameEventSO_Void)
Assets/_Game/Data/Events/OnStatsChanged.asset.meta
Assets/Tests/EditMode/TrainerTransactionTests.cs                     ← NEW Edit Mode tests (≥8 tests)
Assets/Tests/EditMode/TrainerTransactionTests.cs.meta
```

**Files to MODIFY:**
```
Assets/_Game/Scripts/Progression/LearningPointSystem.cs              ← Add TrySpendLP method
Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs        ← Add base stat fields
Assets/_Game/Scenes/TestScene.unity                                  ← Add PlayerStats, GoldSystem, Trainer_Master GO
```

**Files NOT to modify:**
```
Assets/_Game/Data/Config/ProgressionConfig.asset    ← Unity auto-updates with new field defaults
Assets/_Game/Scripts/Progression/XPSystem.cs        ← unchanged
Assets/_Game/Scripts/Progression/LevelSystem.cs     ← unchanged
Assets/_Game/Data/Events/OnLPChanged.asset          ← reused; TrySpendLP still raises it
```

**Scripts/Progression/ after this story:**
```
Assets/_Game/Scripts/Progression/
├── XPSystem.cs               ← Story 3.1 (unchanged)
├── LevelSystem.cs            ← Story 3.2 (unchanged)
└── LearningPointSystem.cs    ← Story 3.3 (+ TrySpendLP added in 3.4)
```
(StatSystem.cs: Story 3.6)

**Scripts/Player/ after this story:**
```
Assets/_Game/Scripts/Player/
├── PlayerController.cs    ← Epic 1
├── PlayerAnimator.cs      ← Epic 1
├── CameraController.cs    ← Epic 1
├── PlayerHealth.cs        ← Story 2.9
└── PlayerStats.cs         ← NEW Story 3.4
```

**Data/Events/ after this story:**
```
Assets/_Game/Data/Events/
├── OnEntityKilled.asset   ← Story 2.9 (GameEventSO_String)
├── OnPlayerDied.asset     ← Story 2.9 (GameEventSO_Void)
├── OnXPGained.asset       ← Story 3.1 (GameEventSO_Int)
├── OnLevelUp.asset        ← Story 3.2 (GameEventSO_Int, payload=newLevel)
├── OnLPChanged.asset      ← Story 3.3 (GameEventSO_Int, payload=newLPTotal)
├── OnGoldChanged.asset    ← NEW Story 3.4 (GameEventSO_Int, payload=newGoldTotal)
└── OnStatsChanged.asset   ← NEW Story 3.4 (GameEventSO_Void)
```

**Scripts/Economy/ after this story:**
```
Assets/_Game/Scripts/Economy/
└── GoldSystem.cs          ← NEW Story 3.4 (minimal; full Economy in Epic 6)
```

### References

- Epic 3, Story 4 ("As a player, I can spend learning points and gold at a trainer to raise stats"): [Source: _bmad-output/epics.md#Epic 3: Progression & Stats]
- GDD — Base stats (Strength, Dexterity, Endurance, Mana): [Source: _bmad-output/gdd.md#Base Stats]
- GDD — "Trainers are the primary gold sink — skills cost LP + gold": [Source: _bmad-output/gdd.md#Economy]
- GDD — Trainer upgrade cost table: [Source: _bmad-output/gdd.md#Progression (Learning Point System)]
- Architecture — `TrainerSO` in `ScriptableObjects/NPC/`: [Source: _bmad-output/game-architecture.md#Decision 6]
- Architecture — Cross-system communication via `GameEventSO<T>` only: [Source: _bmad-output/game-architecture.md#Architectural Boundaries]
- Architecture — `GoldSystem.cs` location: `Scripts/Economy/`: [Source: _bmad-output/game-architecture.md#Project Structure]
- Architecture — `PlayerStats.cs` location: `Scripts/Player/`: [Source: _bmad-output/game-architecture.md#Project Structure]
- Architecture — Event naming `On + EventName`: [Source: _bmad-output/game-architecture.md#Naming Conventions]
- Story 3.3 — `TrySpendLP` planned, not implemented yet: [Source: _bmad-output/implementation-artifacts/3-3-learning-points.md#Critical: Story 3.4 Integration Point]
- Story 3.3 — 73 tests total (corrected): [Source: _bmad-output/implementation-artifacts/3-3-learning-points.md#Code Review Notes]
- Story 3.3 — `OnLPChanged.asset` (GameEventSO_Int) live; TrySpendLP must raise it: [Source: _bmad-output/implementation-artifacts/3-3-learning-points.md#Dev Notes]
- CLAUDE.md — OnDisable null guard for InputSystem_Actions: [Source: CLAUDE.md#Unity Lifecycle Gotcha: OnDisable Before OnEnable]
- CLAUDE.md — `InputSystem_Actions.cs` embeds full JSON; new actions need both files updated: [Source: CLAUDE.md#InputSystem_Actions.cs Embeds the Full JSON] (not applicable here — no new actions added)
- project-context.md — NEVER use Debug.Log: [Source: _bmad-output/project-context.md#Logging — MANDATORY]
- project-context.md — Config SOs for all tunable values: [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- project-context.md — `FindFirstObjectByType` (not deprecated `FindObjectOfType`): [Source: _bmad-output/project-context.md#Unity 6 Specific]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Fixed missing `using Game.Progression;` in `PlayerStats.cs` (CS0246 on first compile).
- `_trainerData` ref was lost after scene save — reassigned via MCP after reload.
- SphereCollider properties (radius/isTrigger) not applied at GO creation time — set separately.
- `_onLPChanged` on LearningPointSystem was null in scene — fixed while wiring.

### Completion Notes List

- Implemented all 10 tasks across 7 new/modified source files and 1 scene.
- `PlayerStats.cs` (namespace `Game.Player`) holds STR/DEX/END/MNA with `UpgradeStat` + `GetStat` + OnGUI overlay at y=350.
- `GoldSystem.cs` (namespace `Game.Economy`) holds gold with `TrySpend`/`Add` + OnGUI overlay at y=370.
- `TrainerSO.cs` (namespace `Game.NPC`) defines trainer data with `StatUpgradeEntry` struct.
- `Trainer_Master.asset` configured with 4 upgrades (LP:1, G:100, maxLevel:5 each).
- `LearningPointSystem.cs` extended with `TrySpendLP` (atomic LP spend, raises `_onLPChanged`).
- `TrainerNPC.cs` (namespace `Game.AI`) implements proximity+E-key menu, atomic transaction, number keys 1-4, OnDisable null guard.
- `OnGoldChanged.asset` and `OnStatsChanged.asset` created.
- TestScene wired: `PlayerStats` on Player, `GoldSystem` on ProgressionSystem, `Trainer_Master` GO placed clear of Enemy_Grunt patrol path; sphere visual mesh added for in-scene visibility.
- 82/82 Edit Mode tests pass (73 prior + 8 new in `TrainerTransactionTests.cs` + 1 extra).
- Play Mode: proximity prompt, E-key toggle, stat purchase, max-level blocking all functional.

### File List

Assets/_Game/Scripts/Player/PlayerStats.cs           ← modified in code review (M4: UpgradeStat guard)
Assets/_Game/Scripts/Player/PlayerStats.cs.meta
Assets/_Game/Scripts/Economy/GoldSystem.cs           ← modified in code review (M1: Add guard)
Assets/_Game/Scripts/Economy/GoldSystem.cs.meta
Assets/_Game/Scripts/AI/TrainerNPC.cs                ← modified in code review (M2: null _input after Dispose)
Assets/_Game/Scripts/AI/TrainerNPC.cs.meta
Assets/_Game/ScriptableObjects/NPC/TrainerSO.cs
Assets/_Game/ScriptableObjects/NPC/TrainerSO.cs.meta
Assets/_Game/Data/NPCs/Trainer_Master.asset
Assets/_Game/Data/NPCs/Trainer_Master.asset.meta
Assets/_Game/Data/Events/OnGoldChanged.asset
Assets/_Game/Data/Events/OnGoldChanged.asset.meta
Assets/_Game/Data/Events/OnStatsChanged.asset
Assets/_Game/Data/Events/OnStatsChanged.asset.meta
Assets/Tests/EditMode/TrainerTransactionTests.cs
Assets/Tests/EditMode/TrainerTransactionTests.cs.meta
Assets/_Game/Scripts/Progression/LearningPointSystem.cs
Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs
Assets/_Game/Scenes/TestScene.unity

## Change Log

- 2026-03-13: Story 3.4 implemented — trainer stat upgrade system. Added PlayerStats, GoldSystem, TrainerSO, TrainerNPC, LearningPointSystem.TrySpendLP, event SOs, Trainer_Master data asset, TestScene wiring, and 8 new Edit Mode tests.
- 2026-03-13: Trainer_Master GO repositioned in TestScene (away from Enemy_Grunt patrol path); sphere visual added for scene visibility.
- 2026-03-13: Code review — 5 medium issues fixed: GoldSystem.Add guard against negative/zero amounts (M1); TrainerNPC.OnDisable now nulls _input after Dispose to prevent ObjectDisposedException on re-enable (M2); TrainerTransactionTests expanded with 6 boundary/guard tests covering exact-equal affordability and new validation guards (M3); PlayerStats.UpgradeStat guard against non-positive points (M4); Dev Record test count corrected — 8 story tests + 6 review tests = 14 new, total ≥ 87 (M5).
