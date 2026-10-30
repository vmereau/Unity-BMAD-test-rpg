---
title: 'Two-handed weapon family + Flamberge'
type: 'feature'
created: '2026-10-30'
status: 'done'
baseline_commit: '58e3fe9a3fb89b3de1ae36517be4571ab6f32def'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/.claude/rules/attack-pipeline.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Only one-handed melee families exist. The user downloaded dedicated two-handed Mixamo clips (idle, one attack, block, stagger, and draw / sheathe each split in two parts) and wants a first two-handed weapon, "Flamberge", that costs significantly more stamina per swing but deals more health and stance damage, sheathed on the back.

**Approach:** Add an `Archetype_TwoHanded` family (right hand, Back socket, own override controller, 2-step combo reusing the single swing, stance 45 + knockdown) and a per-archetype attack-stamina multiplier (2H ×2). Merge each two-part draw / sheathe into one baked `.anim`. Build the Flamberge with the Weapon Creator from `Assets/Hivemind/MedievalWeapons/Art/Models/SM_Flamberge/`. Rename the animation folder `Combat/Sword/` → `Combat/one handed/`.

## Boundaries & Constraints

**Always:** Keep asset GUIDs on the rename (move `.meta` files too). Read `Resolved*` properties at runtime. Two-handed clip events: all four attack events on the swing, plus one `WeaponSwap` per merged draw / sheathe. Default stamina multiplier = 1, so existing weapons (sword, axe, bow) are unchanged.

**Ask First:** Any change to `Humanoid_Template.controller` states or transitions. Any change to the hitbox / stance / damage formulas beyond the stamina multiplier.

**Never:** No second draw / sheathe animator state. No left-hand IK. No AI / NPC two-handed support. No per-weapon offsets in `EquipmentVisuals.cs`. No events authored on Hivemind (vendor) assets.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 2H swing | Flamberge drawn, stamina ≥ 40 | 40 stamina spent, swing plays, hit = base + 25 damage, stance 45 (+Str), may knock down | N/A |
| Low stamina | Flamberge drawn, 20 ≤ stamina < 40 | Attack refused, combo reset | Existing "insufficient stamina" warn |
| Other weapon | Sword / axe / bow / unarmed | Cost stays `attackStaminaCost` (×1) | Null archetype → ×1 |
| Draw / sheathe | R with Flamberge | Merged clip plays; weapon moves back↔hand at the seam (`WeaponSwap`); finishes before the watchdog | Exit net / watchdog unchanged |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs` -- family preset; gets `attackStaminaMultiplier`
- `Assets/_Game/ScriptableObjects/Items/WeaponSO.cs` -- `Resolved*` accessors
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- melee stamina gate (`TryAttack` ~L350), debug `canAttack` (~L992); bow fire keeps the flat cost
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` + its asset -- `weaponSwitchTimeout` (2 → 2.5: merged sheathe = 1.97 s)
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two handed/` -- 2H clips (FBX, Humanoid)
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Sword/` -- to rename `one handed/`
- `Assets/_Game/Editor/WeaponCreatorWindow.cs` + `WeaponModelUtility.cs` -- weapon prefab / SO generation
- `Assets/_Game/Data/Items/Weapons/Archetypes/` -- archetype assets

## Tasks & Acceptance

**Execution:**
- [x] `Combat/Sword/` → `Combat/one handed/` -- `git mv` folder + `.meta`, refresh, verify override refs intact -- requested rename
- [x] `WeaponArchetypeSO.cs`, `WeaponSO.cs` -- add `[Min(0.1f)] attackStaminaMultiplier = 1` + `WeaponSO.ResolvedAttackStaminaMultiplier` (null archetype → 1) -- 2H stamina cost
- [x] `PlayerCombat.cs` -- melee `HasEnough` / `Consume` / debug check use `attackStaminaCost × multiplier` of `_currentWeaponSO` (unarmed ×1) -- apply cost
- [x] `Tests/EditMode/` -- test `ResolvedAttackStaminaMultiplier` (archetype value, null archetype → 1) -- cover resolution
- [x] `two handed/*.fbx.meta` -- via `ModelImporter.clipAnimations`: rename clips (`2hand idle` loopTime on, `2hand attack`, `2hand blocking` loopTime on, `2hand stagger`, draw / sheath parts); attack events HitboxEnable 0.33, HitboxDisable 0.58, ComboWindowOpen 0.58, ComboWindowClose 0.78 (< Attack_1 exit 0.81; from hand-speed sampling) -- import + events
- [x] `two handed/2hand draw.anim`, `2hand sheath.anim` -- editor-baked: concatenate part 1 + part 2 Humanoid curves, 0.1 s gap with Auto tangents at the seam, `WeaponSwap` event at the seam (seconds) -- single-state draw / sheathe
- [x] `two handed/TwoHanded_AnimatorOverride.overrideController` -- on `Humanoid_Template`: Unarmed Idle→2hand idle, Jab + Uppercut Jab→2hand attack, Block Idle→2hand blocking, draw sword→2hand draw, sheath sword→2hand sheath, stagger 1 + 2→2hand stagger -- family clips
- [x] `Data/Items/Weapons/Archetypes/Archetype_TwoHanded.asset` -- Right hand, Back socket, override above, `defaultComboSteps` 2, `stanceDamage` 45, `canKnockDown` true, `attackStaminaMultiplier` 2; `drawnPose` calibrated from the guard pose (hand line), `sheathedPose` hilt over right shoulder, blade down-left -- family preset
- [x] Weapon Creator → `Weapon_Flamberge` (`SwordSO`, `damageBonus` 25) + `Prefabs/Items/Weapons/TwoHanded/Flamberge/` `_Visual` / `_World` + icon; shrink the `Drawn` box to the blade -- the weapon
- [x] `CombatConfigSO` asset -- `weaponSwitchTimeout` 2.5 -- watchdog headroom
- [x] CLAUDE.md files (Combat anims, Controllers, Weapons prefabs, Items, Combat scripts) -- folder rename, 2H clip table / merge recipe, archetype, stamina multiplier -- docs

**Acceptance Criteria:**
- Given the renamed folder, when the project reimports, then `Sword_AnimatorOverride` and every sword / axe weapon still resolve their clips (no missing refs).
- Given the Flamberge equipped and sheathed, then it sits on `BackWeaponSocket`; when drawn, then it is in the right hand with the 2H idle / block / swing playing.
- Given the Flamberge drawn, when attacking twice in the combo window, then two swings chain and the second applies the finisher stance multiplier.
- Given a Flamberge hit on an AI, then health damage exceeds the short iron sword's and stance damage is 45-based with knockdown allowed.

## Design Notes

Merged clips: the parts meet at the hand-behind-head frame (draw 1 end RH ≈ (0, 1.90, −0.21), draw 2 start ≈ (0.11, 1.84, −0.13); sheath likewise), so the seam = `WeaponSwap`. Draw / sheathe states are on the upper-body Attack layer and `applyRootMotion` is off, so root drift in the parts is irrelevant. The idle is a shoulder rest while attack / stagger / sheathe start from a front guard — accepted blend snap. The `Stagger_1/2` override also covers the Base-layer stagger (legs play the 2H stagger too).

## Verification

**Commands:**
- Unity `run_tests` EditMode -- expected: all green
- `read_console` errors after refresh -- expected: none

**Manual checks:**
- Play mode: equip the Flamberge, R draw / sheathe (socket swap at the seam), swing ×2, block, take a stagger; tune `drawnPose` / `sheathedPose` / hit window by eye.

## Suggested Review Order

**Stamina cost per family**

- Entry point: melee gate checks and spends the same multiplied cost.
  [`PlayerCombat.cs:350`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L350)

- Cost helper: config cost × archetype multiplier, unarmed ×1.
  [`PlayerCombat.cs:920`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L920)

- Resolution floored at 0.1 because [Min] is Inspector-only.
  [`WeaponSO.cs:55`](../../Assets/_Game/ScriptableObjects/Items/WeaponSO.cs#L55)

- New archetype field, default 1 keeps existing families unchanged.
  [`WeaponArchetypeSO.cs:31`](../../Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs#L31)

- Debug overlay keeps the flat cost when a bow is equipped.
  [`PlayerCombat.cs:997`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L997)

**Two-handed family data**

- Archetype: right hand, back socket, 2 steps, stance 45 + knockdown, ×2 stamina.
  [`Archetype_TwoHanded.asset:22`](../../Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_TwoHanded.asset#L22)

- Override maps idle / attack ×2 / block / stagger ×2 / draw / sheathe to 2H clips.
  [`TwoHanded_AnimatorOverride.overrideController:13`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two%20handed/TwoHanded_AnimatorOverride.overrideController#L13)

- Attack events; ComboWindowClose kept below Attack_1 exit (0.81).
  [`2hand attack.fbx.meta:81`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two%20handed/2hand%20attack.fbx.meta#L81)

**Flamberge**

- SwordSO with damageBonus 25 on the two-handed archetype.
  [`Weapon_Flamberge.asset:32`](../../Assets/_Game/Data/Items/Weapons/TwoHanded/Weapon_Flamberge.asset#L32)

- Drawn hitbox trimmed to the blade only.
  [`Flamberge_Visual.prefab:56`](../../Assets/_Game/Prefabs/Items/Weapons/TwoHanded/Flamberge/Flamberge_Visual.prefab#L56)

**Config, tests, docs**

- Watchdog headroom for the 1.97 s merged sheathe.
  [`CombatConfigSO.cs:90`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L90)

- Multiplier resolution tests (value, default, null archetype).
  [`WeaponArchetypeTests.cs:83`](../../Assets/Tests/EditMode/WeaponArchetypeTests.cs#L83)

- 2H clip timings and the two-part bake recipe.
  [`Combat/CLAUDE.md:47`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L47)

- Archetype + Flamberge mesh frame documented.
  [`Weapons/CLAUDE.md:104`](../../Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md#L104)
