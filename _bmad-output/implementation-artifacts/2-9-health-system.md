# Story 2.9: Health System

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want enemies and I to have health that depletes on hit,
so that combat has meaningful consequences and the full loop (attack → damage → death) is complete.

## Acceptance Criteria

1. `AIConfigSO.cs` gains a `[Header("Health")]` block and a `[Header("Attack")]` block:
   - `public float baseHealth = 50f`
   - `public float attackRange = 1.8f` — distance at which enemy can land a hit
   - `public float attackCooldown = 2f` — seconds between enemy strikes
   - `public float attackDamage = 10f` — damage applied per enemy hit that is not blocked/dodged

2. `CombatConfigSO.cs` gains:
   - `[Header("Player Health")]`: `public float baseHealth = 100f`
   - `[Header("Attack Damage")]`: `public float attackDamage = 25f`, `public float attackHitRange = 2f`

3. `EnemyHealth.cs` exists at `Assets/_Game/Scripts/AI/EnemyHealth.cs`:
   - `namespace Game.AI`, `private const string TAG = "[Combat]";`
   - `[SerializeField] private AIConfigSO _config`
   - `[SerializeField] private PersistentID _persistentID` (optional, warn if unassigned)
   - `public float CurrentHealth { get; private set; }`
   - `public bool IsDead { get; private set; }`
   - `Awake()`: null-guard `_config` (error + disable); `CurrentHealth = _config.baseHealth`; warn if `_persistentID == null`
   - `public void TakeDamage(float amount)`: guard `IsDead`; reduce health; clamp to 0; if `<= 0` call `Die()`; log every hit
   - `private void Die()`:
     - `IsDead = true`
     - Log `"{gameObject.name} died — registering kill"`
     - Stop NavMeshAgent: `if (TryGetComponent<NavMeshAgent>(out var agent)) agent.isStopped = true;`
     - `_persistentID?.RegisterDeath()` — registers with WorldStateManager and raises the OnEntityKilled event
     - `gameObject.SetActive(false)`
   - `OnGUI` debug overlay (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`) at y=270:
     `$"EnemyHP: {CurrentHealth:F0}/{_config.baseHealth:F0} | Dead:{IsDead}"` — `fontSize = 18`

4. `PlayerHealth.cs` exists at `Assets/_Game/Scripts/Player/PlayerHealth.cs`:
   - `namespace Game.Player`, `private const string TAG = "[Combat]";`
   - `[SerializeField] private CombatConfigSO _config`
   - `[SerializeField] private GameEventSO_Void _onPlayerDied` (optional, warn if unassigned)
   - `public float CurrentHealth { get; private set; }`
   - `public bool IsDead { get; private set; }`
   - `Awake()`: null-guard `_config` (error + disable); `CurrentHealth = _config.baseHealth`; warn if event unassigned
   - `public void TakeDamage(float amount)`: guard `IsDead`; reduce health; clamp to 0; if `<= 0` call `Die()`; log every hit
   - `private void Die()`:
     - `IsDead = true`
     - Log `"Player has died"`
     - `_onPlayerDied?.Raise(true)` — signals save/respawn system (Epic 8 scope)
     - `gameObject.SetActive(false)` — simple death for prototype (respawn in Epic 8)
   - `OnGUI` debug overlay (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`) at y=250:
     `$"PlayerHP: {CurrentHealth:F0}/{_config.baseHealth:F0} | Dead:{IsDead}"` — `fontSize = 18`

5. `EnemyBrain.cs` is updated to add `Attacking` and `Dead` states:
   - Enum expands to: `private enum EnemyState { Idle, Patrolling, Engaging, Attacking, Dead }`
   - `Awake()` caches:
     - `_enemyHealth = GetComponent<EnemyHealth>()` — null-guard: error + disable if missing
     - `_playerCombat = playerObj?.GetComponent<PlayerCombat>()` — warn if null; enemy can still engage without dealing damage
     - `_playerHealth = playerObj?.GetComponent<PlayerHealth>()` — warn if null
   - `Update()`: add a dead-check at the top (before the switch) — if `_enemyHealth.IsDead && _state != EnemyState.Dead` → call `TransitionToDead()` + `return`
   - `HandleEngage()`: if `distToPlayer <= _config.attackRange` → `TransitionToAttacking()`
   - New `HandleAttack()`:
     - If `_player == null` or `distToPlayer > _config.disengageRange` → `TransitionToPatrol()`
     - If `distToPlayer > _config.attackRange` → `TransitionToEngaging()`  (player moved away mid-swing)
     - `_attackCooldownTimer -= Time.deltaTime`
     - If `_attackCooldownTimer <= 0f` → execute attack
   - Attack execution inside `HandleAttack()`:
     - Reset `_attackCooldownTimer = _config.attackCooldown`
     - `Log.Info(TAG, $"{gameObject.name} attacks player")`
     - If `_playerCombat != null`: `HitResult result = _playerCombat.TryReceiveHit(gameObject)`
     - If `result == HitResult.NotBlocked` and `_playerHealth != null`: `_playerHealth.TakeDamage(_config.attackDamage)`
     - If `result == HitResult.PerfectBlock`: log "Enemy attack staggered by perfect block" (no stagger behavior yet, just log)
     - If `_playerCombat == null` and `_playerHealth != null`: apply damage directly (no block check fallback)
   - `TransitionToAttacking()`:
     - `_state = EnemyState.Attacking`
     - `_agent.isStopped = true` (stop moving to attack in place)
     - `_attackCooldownTimer = 0f` (first attack fires immediately — forces immediate first hit)
     - `GameLog.Info(TAG, $"{gameObject.name} entering attack range — switching to Attacking")`
   - `TransitionToDead()`:
     - `_state = EnemyState.Dead`
     - `_agent.isStopped = true`
     - `GameLog.Info(TAG, $"{gameObject.name} transitioned to Dead state")`
   - `HandleDead()`: no-op (entity is SetActive(false) by EnemyHealth.Die(); this runs at most 1 frame)
   - `OnGUI` at y=220 updated to include attack cooldown:
     `$"Enemy: {_state} | PlayerDist:{distStr} | DetectRange:{_config.detectionRange}m | AtkCD:{_attackCooldownTimer:F1}s"`

6. `PlayerCombat.cs` is updated to add hit detection when an attack fires:
   - In `TryAttack()`, after the stamina is consumed and before the return:
     - Call `ExecuteHitDetection()` with a non-allocating sphere overlap
   - `private void ExecuteHitDetection()`:
     - `int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _config.attackHitRange, _hitBuffer)`
     - For each hit (up to buffer size): if `hit.TryGetComponent<EnemyHealth>(out var health) && !health.IsDead` → `health.TakeDamage(_config.attackDamage)`
     - Log total hits found
   - Add field: `private readonly Collider[] _hitBuffer = new Collider[10]` (pre-allocated, reused per attack)
   - **Layer filter note:** Currently no enemy-specific layer exists (deferred to Epic 3). Use `Physics.DefaultRaycastLayers` mask for now. When enemy layer is added in Epic 3, update mask.

7. `OnEntityKilled.asset` event SO created at `Assets/_Game/Data/Events/OnEntityKilled.asset`:
   - Type: `GameEventSO_String`
   - Assigned to `PersistentID._onEntityKilled` field on `Enemy_Grunt.prefab`
   - Raises when enemy dies — XP system (Epic 3) and Quest system (Epic 5) will subscribe to this

8. `OnPlayerDied.asset` event SO created at `Assets/_Game/Data/Events/OnPlayerDied.asset`:
   - Type: `GameEventSO_Void`
   - Assigned to `PlayerHealth._onPlayerDied` field on `Player.prefab`
   - Raises when player health reaches 0 — Save system (Epic 8) will subscribe

9. Enemy_Grunt prefab updated:
   - `EnemyHealth` component added with `_config = AIConfig.asset` and `_persistentID` wired
   - `EnemyBrain._enemyHealth` auto-resolved via `GetComponent` (no manual wiring needed)

10. Player prefab updated:
    - `PlayerHealth` component added with `_config = CombatConfig.asset`
    - `_onPlayerDied = OnPlayerDied.asset` assigned

11. Edit Mode tests at `Assets/Tests/EditMode/HealthSystemTests.cs` with ≥ 4 tests:
    - Helper: `float ApplyDamage(float currentHealth, float damage) => Mathf.Max(0f, currentHealth - damage)`
    - Helper: `bool IsDead(float currentHealth) => currentHealth <= 0f`
    - `ApplyDamage_ReducesHealth_ByAmount()` — 100 hp, 25 dmg → 75
    - `ApplyDamage_ClampsToZero_WhenDamageExceedsHealth()` — 20 hp, 50 dmg → 0
    - `ApplyDamage_ReturnsZero_WhenDamageExactlyEqualsHealth()` — 50 hp, 50 dmg → 0
    - `IsDead_ReturnsFalse_WhenHealthAboveZero()` — 1 hp → false
    - `IsDead_ReturnsTrue_WhenHealthAtZero()` — 0 hp → true
    - `IsDead_ReturnsTrue_WhenHealthBelowZero_Hypothetical()` — -1 hp → true (formula safety)

12. No compile errors. All existing 47 Edit Mode tests pass. New total: ≥ 53.

13. Play Mode validation:
    - Player attacks (LMB) deal damage to enemy → EnemyBrain stops when health reaches 0
    - Enemy patrols, engages, then attacks player when within attack range
    - Player takes damage from enemy hits; blocking/dodging prevent damage per HitResult
    - Perfect block returns PerfectBlock result (no damage, "staggered" log only for now)
    - Enemy dies → disappears (`SetActive(false)`) → does not respawn after scene reload (PersistentID)
    - Player dies → player disappears, console shows "Player has died" log

## Tasks / Subtasks

- [ ] Task 1: Expand config SOs (AC: 1, 2)
  - [ ] 1.1 Edit `AIConfigSO.cs` — add `baseHealth`, `attackRange`, `attackCooldown`, `attackDamage` fields
  - [ ] 1.2 Edit `CombatConfigSO.cs` — add `baseHealth`, `attackDamage`, `attackHitRange` fields
  - [ ] 1.3 Verify `AIConfig.asset` and `CombatConfig.asset` show new fields in Inspector with correct defaults

- [ ] Task 2: Create `EnemyHealth.cs` (AC: 3)
  - [ ] 2.1 Create `Assets/_Game/Scripts/AI/EnemyHealth.cs`
  - [ ] 2.2 Implement `TakeDamage()` and `Die()` with NavMeshAgent stop + PersistentID death + SetActive(false)
  - [ ] 2.3 Add `OnGUI` debug overlay at y=270

- [ ] Task 3: Create `PlayerHealth.cs` (AC: 4)
  - [ ] 3.1 Create `Assets/_Game/Scripts/Player/PlayerHealth.cs`
  - [ ] 3.2 Implement `TakeDamage()` and `Die()` with event raise + SetActive(false)
  - [ ] 3.3 Add `OnGUI` debug overlay at y=250

- [ ] Task 4: Expand `EnemyBrain.cs` — Attacking + Dead states (AC: 5)
  - [ ] 4.1 Add `Attacking`, `Dead` to enum; add new private fields (`_enemyHealth`, `_playerCombat`, `_playerHealth`, `_attackCooldownTimer`)
  - [ ] 4.2 Update `Awake()` to cache `EnemyHealth`, `PlayerCombat`, `PlayerHealth`
  - [ ] 4.3 Add dead-check at top of `Update()`
  - [ ] 4.4 Update `HandleEngage()` to trigger attack transition when within `attackRange`
  - [ ] 4.5 Implement `HandleAttack()` with cooldown, `TryReceiveHit()`, and `TakeDamage()` calls
  - [ ] 4.6 Implement `TransitionToAttacking()`, `TransitionToDead()`, `HandleDead()`
  - [ ] 4.7 Update `OnGUI` label at y=220 to include attack cooldown display

- [ ] Task 5: Update `PlayerCombat.cs` — add hit detection (AC: 6)
  - [ ] 5.1 Add `_hitBuffer` pre-allocated collider array field
  - [ ] 5.2 Implement `ExecuteHitDetection()` using `Physics.OverlapSphereNonAlloc`
  - [ ] 5.3 Call `ExecuteHitDetection()` from `TryAttack()` after stamina is consumed

- [ ] Task 6: Create event assets and wire them (AC: 7, 8)
  - [ ] 6.1 Create `Assets/_Game/Data/Events/OnEntityKilled.asset` (GameEventSO_String via right-click → Game/Events/String Event)
  - [ ] 6.2 Assign `OnEntityKilled.asset` to `PersistentID._onEntityKilled` on `Enemy_Grunt.prefab`
  - [ ] 6.3 Create `Assets/_Game/Data/Events/OnPlayerDied.asset` (GameEventSO_Void via right-click → Game/Events/Void Event)

- [ ] Task 7: Update prefabs (AC: 9, 10)
  - [ ] 7.1 Add `EnemyHealth` component to `Enemy_Grunt.prefab`; assign `_config = AIConfig.asset`; wire `_persistentID`
  - [ ] 7.2 Add `PlayerHealth` component to `Player.prefab`; assign `_config = CombatConfig.asset`; assign `_onPlayerDied = OnPlayerDied.asset`

- [ ] Task 8: Edit Mode tests (AC: 11)
  - [ ] 8.1 Create `Assets/Tests/EditMode/HealthSystemTests.cs` with ≥ 6 tests
  - [ ] 8.2 Run all tests via Unity Test Runner — all 53+ green

- [ ] Task 9: Play Mode validation (AC: 13) — requires Unity Editor
  - [ ] 9.1 Enemy attacks player at close range — player HP decreases in OnGUI overlay
  - [ ] 9.2 Player attacks enemy (LMB) — enemy HP decreases; verify sphere overlap range feels correct
  - [ ] 9.3 Block/dodge prevents damage (verify per HitResult)
  - [ ] 9.4 Kill enemy (3-4 hits) — enemy disappears; reload scene → enemy stays gone (PersistentID)
  - [ ] 9.5 Player health reaches 0 → player disappears; "Player has died" in console
  - [ ] 9.6 No NullReferenceExceptions in console

## Dev Notes

Story 2.9 completes Epic 2 by adding the final combat loop elements: mutual health pools, enemy attack behavior, player hit detection from attacks, and death/persistence. This story builds directly on the `EnemyBrain` state machine (Story 2.8), the `PlayerCombat.TryReceiveHit` API (Story 2.5), and `PersistentID.RegisterDeath()` (Story 2.8).

### Critical: EnemyBrain Expansion — State Machine Changes

The `EnemyState` enum in `EnemyBrain.cs` currently only has `{ Idle, Patrolling, Engaging }`. Story 2.9 adds `Attacking` and `Dead`.

**Attacking state transition logic:**
- `HandleEngage()` transitions to `Attacking` when `distToPlayer <= _config.attackRange`
- `HandleAttack()` transitions back to `Engaging` when `distToPlayer > _config.attackRange` (player moved away)
- `HandleAttack()` transitions to `Patrolling` when `distToPlayer > _config.disengageRange`
- `_attackCooldownTimer = 0f` in `TransitionToAttacking()` means the first hit lands on the very first attack-cycle frame — correct for snappy enemy feel

**Dead state handling:**
`EnemyHealth.Die()` calls `gameObject.SetActive(false)` which immediately stops all MonoBehaviour `Update()` calls. The `Dead` state transition is mostly defensive code that would only run on the same frame as death before SetActive fires. The dead-check at top of `Update()` handles the edge case of death being registered mid-frame.

**NavMeshAgent stop in Die():**
`EnemyHealth.Die()` uses `TryGetComponent<NavMeshAgent>()` to stop the agent before deactivation. This prevents the NavMeshAgent from throwing warnings when the agent is removed from the NavMesh mid-path.

### Critical: Player Attack Hit Detection

```csharp
// Pre-allocated buffer — add as class field (never 'new' per call)
private readonly Collider[] _hitBuffer = new Collider[10];

private void ExecuteHitDetection()
{
    int hitCount = Physics.OverlapSphereNonAlloc(
        transform.position, _config.attackHitRange, _hitBuffer);

    int damaged = 0;
    for (int i = 0; i < hitCount; i++)
    {
        if (_hitBuffer[i].TryGetComponent<EnemyHealth>(out var health) && !health.IsDead)
        {
            health.TakeDamage(_config.attackDamage);
            damaged++;
        }
    }

    if (damaged > 0)
        GameLog.Info(TAG, $"Attack hit {damaged} target(s)");
}
```

**Prototype limitations of this approach:**
- Hit triggers on input frame, not on animation active frames — feels instant. Fine for prototype; animation-driven hit timing is Epic 8 polish scope.
- No layer mask filter (enemy layer deferred to Epic 3). Sphere may catch unintended colliders — mitigated by checking `TryGetComponent<EnemyHealth>`.
- `attackHitRange = 2f` should feel right for melee with `engageStoppingDistance = 1.5f` (enemy stops 1.5m away, player reach is 2m).

### Critical: Cross-System Reference Pattern (EnemyBrain → Player Components)

`EnemyBrain` (in `Game.AI`) references `PlayerCombat` (in `Game.Combat`) and `PlayerHealth` (in `Game.Player`). This breaks the "no direct cross-system references" architecture rule — but this pattern was explicitly designed in Story 2.5:

> _"Called by enemy attack code when a hit connects with the player. Returns PerfectBlock/Blocked/NotBlocked. Story 2.7 implements the caller."_ — PlayerCombat.TryReceiveHit() docstring

The `TryReceiveHit()` API was intentionally designed as a direct call interface for the enemy. For prototype scope, direct tag-lookup + `GetComponent` in `Awake()` is acceptable. Epic 3 or 4 may refactor to a `PlayerRegistry` ScriptableObject or event-based hit notification if multiple enemy types need this.

**Caching pattern in EnemyBrain.Awake():**
```csharp
var playerObj = GameObject.FindGameObjectWithTag("Player");
if (playerObj != null)
{
    _player = playerObj.transform;
    _playerCombat = playerObj.GetComponent<PlayerCombat>();
    _playerHealth = playerObj.GetComponent<PlayerHealth>();
    if (_playerCombat == null) GameLog.Warn(TAG, "PlayerCombat not found on Player");
    if (_playerHealth == null) GameLog.Warn(TAG, "PlayerHealth not found on Player");
}
```
Both are warn-only (enemy can still patrol/engage without being able to deal damage).

### Critical: PersistentID.RegisterDeath() Chain

The call chain when an enemy dies:
```
EnemyHealth.TakeDamage() → health <= 0 → Die()
  → TryGetComponent<NavMeshAgent>() → agent.isStopped = true
  → _persistentID?.RegisterDeath()
      → WorldStateManager.Instance?.RegisterKill(_guid)    ← registers the kill
      → _onEntityKilled?.Raise(_guid)                      ← broadcasts via event SO
  → gameObject.SetActive(false)
```

`_onEntityKilled` on `PersistentID` is the `OnEntityKilled.asset` SO created in Task 6. In this story no other system subscribes, but the channel is live for Epic 3 (XP) and Epic 5 (Quests).

### Critical: GameEventSO_Void Usage

Looking at `GameEventSO.cs`: `GameEventSO_Void` is defined as `GameEventSO<bool>` (uses bool payload as a signal). To raise it: `_onPlayerDied?.Raise(true)`. Listeners subscribe with `Action<bool>` (ignore the bool value, it's just a signal).

```csharp
// Correct raise pattern for GameEventSO_Void:
_onPlayerDied?.Raise(true);

// Listener pattern (future SaveSystem in Epic 8):
[SerializeField] private GameEventSO_Void _onPlayerDied;
private void OnEnable() => _onPlayerDied.AddListener(HandlePlayerDied);
private void OnDisable() => _onPlayerDied.RemoveListener(HandlePlayerDied);
private void HandlePlayerDied(bool _) { /* _ = unused signal bool */ }
```

### OnGUI Debug Overlay Stack (Complete After This Story)

```
[y=50]   Stamina: 80 / 100                                                  ← StaminaSystem
[y=70]   Combat: [Ready]                                                    ← PlayerCombat
[y=100]  Combo: step 0 | closed                                             ← PlayerCombat
[y=130]  Block: lowered | PB: closed                                        ← PlayerCombat
[y=160]  State: Airborne:False | Blocking:False | Attacking:False           ← PlayerStateManager
[y=190]  Dodge: ready | CanDodge:True                                       ← DodgeController
[y=220]  Enemy: Patrolling | PlayerDist:10.2m | DetectRange:8m | AtkCD:0.0s ← EnemyBrain (updated)
[y=250]  PlayerHP: 85/100 | Dead:False                                      ← PlayerHealth (NEW)
[y=270]  EnemyHP: 50/50 | Dead:False                                        ← EnemyHealth (NEW)
```

### Attack Damage Values (Prototype Tuning)

| Source | Value | Notes |
|--------|-------|-------|
| Player attack damage | 25f | Kills enemy in 2 hits (50 HP / 25 = 2) — very aggressive, adjust if too fast |
| Player attack range | 2f (sphere radius) | Engages ~2m diameter; feels generous for melee |
| Enemy attack damage | 10f | Kills player in 10 hits (100 HP / 10 = 10) — survivable with block/dodge |
| Enemy attack range | 1.8f | Enemy stops at 1.5m (engageStoppingDistance), attacks at 1.8m — small overlap = responsive |
| Enemy attack cooldown | 2f | One attack every 2 seconds — gives player time to react |

These are starting values. All configurable in `AIConfig.asset` and `CombatConfig.asset` via Inspector during playtesting.

### Known Prototype Limitations

1. **No attack animations for enemy.** Enemy attacks but plays no animation. Placeholder capsule only. Actual attack animations come in Epic 8.
2. **No hit feedback.** No damage numbers, no hit flash, no camera shake. All Epic 8 polish.
3. **No respawn.** Player dies → scene must be reloaded to reset. Respawn/checkpoint system is Epic 8.
4. **Instant hit detection.** Player attack sphere fires on input frame, not on animation active frames. Future: fire hit detection from an animation event or timeline frame in Epic 8.
5. **No enemy stagger.** `HitResult.PerfectBlock` just logs "staggered" but does not interrupt enemy's attack cooldown. Stagger behavior can be added as a `Staggered` state in a follow-up.
6. **Multiple enemy OnGUI overlap.** EnemyHealth y=270 label would overlap for multiple enemies (same as EnemyBrain y=220). Acceptable for single-enemy prototype.

### Architecture Compliance

| Rule | Compliance |
|------|-----------|
| All code under `Assets/_Game/` | ✅ All new scripts in `_Game/Scripts/` |
| GameLog only — no Debug.Log | ✅ All logging via `GameLog.Info/Warn/Error(TAG, ...)` |
| Null-guard in Awake | ✅ All SerializedField refs null-guarded |
| No GetComponent/Camera.main in Update | ✅ All refs cached in Awake (one TryGetComponent in Die() is fine — called once) |
| Non-allocating physics queries | ✅ `OverlapSphereNonAlloc` with pre-allocated buffer |
| Config SOs for all tunable values | ✅ health, damage, range in AIConfigSO / CombatConfigSO |
| Event SOs for cross-system notification | ✅ OnEntityKilled.asset, OnPlayerDied.asset created and wired |
| PersistentID on every enemy | ✅ Already on Enemy_Grunt from Story 2.8; OnEntityKilled event now assigned |
| Cross-system direct refs (EnemyBrain → PlayerCombat/PlayerHealth) | ⚠️ Intentional prototype design per TryReceiveHit API contract from Story 2.5 |
| OnEnable/OnDisable for event subscriptions | N/A — no new event subscriptions in MonoBehaviours this story |
| State machines: enum + switch | ✅ EnemyBrain state machine expanded with Attacking/Dead |

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Scripts/AI/EnemyHealth.cs              ← NEW enemy health component
Assets/_Game/Scripts/AI/EnemyHealth.cs.meta
Assets/_Game/Scripts/Player/PlayerHealth.cs         ← NEW player health component
Assets/_Game/Scripts/Player/PlayerHealth.cs.meta
Assets/_Game/Data/Events/OnEntityKilled.asset       ← NEW event SO (GameEventSO_String)
Assets/_Game/Data/Events/OnEntityKilled.asset.meta
Assets/_Game/Data/Events/OnPlayerDied.asset         ← NEW event SO (GameEventSO_Void)
Assets/_Game/Data/Events/OnPlayerDied.asset.meta
Assets/Tests/EditMode/HealthSystemTests.cs          ← NEW Edit Mode tests
Assets/Tests/EditMode/HealthSystemTests.cs.meta
```

**Files to MODIFY:**
```
Assets/_Game/ScriptableObjects/Config/AIConfigSO.cs       ← Add health + attack fields
Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs   ← Add player health + attack damage fields
Assets/_Game/Scripts/AI/EnemyBrain.cs                     ← Add Attacking/Dead states + attack logic
Assets/_Game/Scripts/Combat/PlayerCombat.cs               ← Add hit detection (OverlapSphereNonAlloc)
Assets/_Game/Prefabs/Enemies/Enemy_Grunt.prefab           ← Add EnemyHealth component; wire OnEntityKilled event
Assets/_Game/Prefabs/Player/Player.prefab                 ← Add PlayerHealth component; wire OnPlayerDied event
```

**Scripts/AI/ after this story:**
```
Assets/_Game/Scripts/AI/
├── EnemyBrain.cs     ← Updated (Attacking + Dead states, attack logic)
└── EnemyHealth.cs    ← NEW Story 2.9
```

**Scripts/Player/ after this story:**
```
Assets/_Game/Scripts/Player/
├── PlayerController.cs    ← Unchanged
├── PlayerAnimator.cs      ← Unchanged
├── CameraController.cs    ← Unchanged
└── PlayerHealth.cs        ← NEW Story 2.9
```

**Data/Events/ after this story:**
```
Assets/_Game/Data/Events/
├── OnEntityKilled.asset   ← NEW Story 2.9 (GameEventSO_String)
└── OnPlayerDied.asset     ← NEW Story 2.9 (GameEventSO_Void)
```

### References

- Epic 2 story 9 ("As a player, enemies and I have health that depletes on hit"): [Source: _bmad-output/epics.md#Epic 2: Combat System]
- Epic 2 scope ("hit detection, health system, death"): [Source: _bmad-output/epics.md#Epic 2: Combat System]
- Architecture — EnemyHealth.cs location: [Source: _bmad-output/game-architecture.md#Directory Structure] → `Scripts/AI/EnemyHealth.cs`
- Architecture — PlayerHealth.cs location: [Source: _bmad-output/game-architecture.md#Directory Structure] → `Scripts/Player/PlayerHealth.cs`
- Architecture — AIConfigSO for attack intervals: [Source: _bmad-output/game-architecture.md#Configuration Management] → "patrol speed, engagement range, attack intervals"
- Architecture — Novel Pattern 2 Permanent Entity (death flow): [Source: _bmad-output/game-architecture.md#Novel Pattern 2: Permanent Entity Pattern]
- Architecture — OnEntityKilled event channel: [Source: _bmad-output/game-architecture.md#Event System] → payload `string persistentID`, raised by CombatSystem, heard by WorldStateManager/QuestSystem
- Architecture — OnPlayerDied event channel: [Source: _bmad-output/game-architecture.md#Event System] → heard by SaveSystem, UI
- Architecture — State machine Attacking/Dead: [Source: _bmad-output/game-architecture.md#State Transitions] → `EnemyState { Idle, Patrolling, Engaging, Attacking, Dead }`
- Architecture — Non-allocating physics: [Source: _bmad-output/project-context.md#Performance Rules] → `Physics.OverlapSphereNonAlloc`
- TryReceiveHit API designed for enemy caller: [Source: _bmad-output/implementation-artifacts/2-4-5-6 story notes / PlayerCombat.cs] → docstring "Called by enemy attack code when a hit connects with the player"
- Story 2.8 — EnemyBrain attack/dead states deferred: [Source: _bmad-output/implementation-artifacts/2-8-enemy-ai-patrol-engage.md#Known Limitations]
- Story 2.8 — WorldStateManager OnEntityKilled wiring deferred: [Source: _bmad-output/implementation-artifacts/2-8-enemy-ai-patrol-engage.md#WorldStateManager Minimal Stub Pattern]
- Story 2.8 — PersistentID.RegisterDeath() pattern (call chain): [Source: _bmad-output/implementation-artifacts/2-8-enemy-ai-patrol-engage.md]
- GameEventSO_Void is GameEventSO<bool>: [Source: Assets/_Game/ScriptableObjects/Events/GameEventSO.cs]
- Project-context: No magic numbers: [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- Project-context: OverlapSphereNonAlloc preferred: [Source: _bmad-output/project-context.md#Performance Rules]
- Project-context: Awake/Start lifecycle split: [Source: _bmad-output/project-context.md#Engine-Specific Rules]
- Project-context: DontDestroyOnLoad + duplicate guard (WorldStateManager): [Source: _bmad-output/project-context.md#Config & Data Anti-Patterns]
- Existing EnemyBrain.cs (current state): [Source: Assets/_Game/Scripts/AI/EnemyBrain.cs]
- Existing PlayerCombat.cs (TryReceiveHit API): [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs]
- Existing AIConfigSO.cs (fields to extend): [Source: Assets/_Game/ScriptableObjects/Config/AIConfigSO.cs]
- Existing CombatConfigSO.cs (fields to extend): [Source: Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs]
- Existing PersistentID.cs (RegisterDeath() method): [Source: Assets/_Game/Scripts/World/PersistentID.cs]
- Test count after 2.8: 47 tests: [Source: _bmad-output/implementation-artifacts/2-8-enemy-ai-patrol-engage.md#Completion Notes List]
- OnGUI overlay y-positions confirmed: [Source: Assets/_Game/Scripts/Combat/StaminaSystem.cs, PlayerCombat.cs, DodgeController.cs, EnemyBrain.cs]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
