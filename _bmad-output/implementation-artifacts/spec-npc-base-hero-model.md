---
title: 'NPC base-hero model prefab'
type: 'feature'
created: '2026-11-13'
status: 'done'
baseline_commit: 'dc58a22469e9900acbdcbc18efc9914f86abab02'
context:
  - '{project-root}/Assets/_Game/Prefabs/Entities/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Humanoid NPCs render the Mixamo dummy (`Idle.fbx`, `Beta_Surface` / `Beta_Joints`) as their `Character`. Its skeleton is not the canonical base hero, so NPCs can't wear the fitted armor (`Armor_CompanyGrunt`) or share the player's weapon sockets. That blocks NPC equipment (goal B, deferred).

**Approach:** Build a reusable humanoid model prefab from the player's base-hero model. It contains the body and Head SMRs, the player's sockets, the fist hitbox and a ragdoll. Nest it in `NPC_base Variant` in place of the dummy `Character`. The player model is a placeholder until real NPC bodies exist.

## Boundaries & Constraints

**Always:**
- The model prefab mirrors the player's `Character` contract: Humanoid avatar from `rigged test hero with textures.fbx`, body SMR `base_hero` (mesh `base_hero_body_baked`), a separate `Head` SMR (`base_hero_head_baked`) bound to the same bones, and the same materials / rendering layer as the player.
- Sockets are copied from `Player.prefab` with the same parent bones and local offsets: `WeaponSocket` (+ `UnarmedHitbox`), `LeftWeaponSocket`, `UndrawnWeaponSocket`, `BackWeaponSocket`, `HelmetSocket`.
- The `UnarmedHitbox` setup matches the NPC's current one: active GO, disabled SphereCollider r 0.25, `WeaponHitbox` with the same reach settings, **no Rigidbody**.
- The ragdoll matches today's 11 bodies (Hips, Spine, Head, upper/lower arms and legs) with the same masses, colliders, joints and layer 0. It is copied by bone name from the current NPC `Character`.
- The variant keeps all its wiring, retargeted to the new model: `HumanoidAnimationBridge._animator`, `EntityAnimationEventReceiver` on the Animator GO, `EntityMeleeAttacker` `Unarmed` → the new `UnarmedHitbox`, the `Hitbox` (Layer 7) child + `_transformsToPinToHips`. Animator: controller `Humanoid_Base`, Apply Root Motion off, culling unchanged.
- Model changes are made through the Editor API (`PrefabUtility`), never by hand-editing prefab YAML.

**Ask First:**
- Any change to `Player.prefab`, or to an NPC scene instance beyond dropping its orphaned `Beta_*` overrides.
- Retuning combat ranges (`AttackRange`, stopping distance, hitbox reach) if NPC jabs no longer land.

**Never:**
- Equipment, armor or weapons on NPCs, fight style, loot rules, faction moves (goal B, `deferred-work.md`).
- Per-NPC faces or bodies. Unpacking the scene NPC Brann to swap his model.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Idle / walk | Any StartingTown NPC | Base-hero body + head rendered, animated by `Humanoid_Base` | N/A |
| Fist fight | NPC brawls the player | Jab combo plays, hits land at the 1.3 m stopping distance | If hits whiff → Ask First (retune) |
| Death | NPC killed | Ragdoll falls, `Hitbox` / `InteractionCollider` follow the hips, corpse lootable | N/A |
| Knockout | Brawl to 0 HP | Unconscious → getting up plays, no ragdoll | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` -- `Character` = nested `Idle.fbx` instance (Animator + receiver, ragdoll, sockets, `UnarmedHitbox`, added `Hitbox` L7) → replaced
- `Assets/_Game/Prefabs/Player/Player.prefab` -- source of the `Head` SMR, body mesh / material override and socket offsets (read only)
- `Assets/_Game/Art/Characters/Humanoids/models/test base hero/rigged test hero with textures.fbx` -- model + avatar
- `Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs` -- ragdoll = every Rigidbody under the Animator; pins transforms to hips
- `Assets/_Game/Scenes/StartingTown.unity` -- NPC instances. Cook, Storekeeper, RacketGuard, Warden and TentMate carry orphaned `Beta_Joints` / `Beta_Surface` position overrides

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Prefabs/Characters/Humanoid_BaseHero.prefab` -- create from the FBX through MCP `execute_code`: set the body SMR mesh / material like the player's; add `Head` (bones remapped by name), the sockets + `UnarmedHitbox` and the ragdoll copied from the current NPC `Character`; Animator avatar = base-hero avatar -- reusable NPC model
- [x] `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` -- replace the nested `Character` with a `Humanoid_BaseHero` instance named `Character`. Re-add `EntityAnimationEventReceiver`, the `Hitbox` child (same capsule / layer) and the Animator settings, then retarget the four root references -- NPCs use the new model
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- drop the orphaned `Beta_*` overrides on the NPC instances and save the scene -- clean overrides
- [x] `Assets/Tests/EditMode/NpcModelPrefabTests.cs` -- load `NPC_base Variant` and assert: Animator avatar from the base-hero FBX, `Head` + `base_hero` SMRs, the 5 sockets, 11 ragdoll Rigidbodies, `UnarmedHitbox` without Rigidbody, bridge / attacker / pin references under the new `Character` -- regression guard
- [x] `Assets/_Game/Prefabs/Entities/CLAUDE.md`, `Assets/_Game/Art/Characters/Humanoids/CLAUDE.md` -- document the model prefab, the swap steps and the Brann gap -- docs

**Acceptance Criteria:**
- Given StartingTown in play mode, when any `NPC_base Variant` NPC is visible, then it shows the base-hero body and head (no Beta dummy) and the console shows no new errors or warnings from NPC setup.
- Given an NPC at idle, when compared with the player, then the HP bar / speech anchor heights still sit above its head.
- Given the EditMode test suite, when run, then `NpcModelPrefabTests` and all existing tests pass.

## Design Notes

The two skeletons have near-identical proportions (hips 1.03 m dummy vs 1.01 m hero, head 1.59 vs 1.63). So the ragdoll colliders and joints are copied 1:1 by bone name, with no scaling. The jab reach should be unchanged; still verify it in play.

The model prefab doesn't include the L7 `Hitbox` or the receiver. Those are entity concerns and stay in the variant, so the model can later serve other humanoids.

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: all pass
- MCP `read_console` after the scene load and a play session -- expected: no new errors

**Manual checks:**
- Play: brawl an NPC (jabs land), kill one (ragdoll + loot), and knock one out (gets up).

## Suggested Review Order

**Model prefab (built in-editor, inspect in Prefab Mode)**

- Contract for the new NPC body: what the model owns vs what the variant adds.
  [`CLAUDE.md:35`](../../Assets/_Game/Prefabs/Entities/CLAUDE.md#L35)

- Added Head SMR bound by bone name to the hero skeleton.
  [`Humanoid_BaseHero.prefab:108`](../../Assets/_Game/Prefabs/Characters/Humanoid_BaseHero.prefab#L108)

- Fist hitbox copied from the old NPC (no Rigidbody, MeshFilter dropped).
  [`Humanoid_BaseHero.prefab:303`](../../Assets/_Game/Prefabs/Characters/Humanoid_BaseHero.prefab#L303)

**Variant swap**

- Re-added L7 hurtbox under the new Character; pinned to hips on death.
  [`NPC_base Variant.prefab:69`](../../Assets/_Game/Prefabs/Entities/Humanoids/NPC_base%20Variant.prefab#L69)

**Tests & docs**

- Root wiring retargeted into the new model (bridge, attacker, pins).
  [`NpcModelPrefabTests.cs:111`](../../Assets/Tests/EditMode/NpcModelPrefabTests.cs#L111)

- Ragdoll guard: 11 bodies, authored kinematic, joints connected inside the model.
  [`NpcModelPrefabTests.cs:95`](../../Assets/Tests/EditMode/NpcModelPrefabTests.cs#L95)

- Drift warning: player body changes aren't propagated to the NPC model.
  [`CLAUDE.md:97`](../../Assets/_Game/Art/Characters/Humanoids/CLAUDE.md#L97)
