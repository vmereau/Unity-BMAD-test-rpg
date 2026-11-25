---
title: 'Torch — off-hand light source'
type: 'feature'
created: '2026-11-25'
status: 'done'
baseline_commit: '853543805d7f39bb2624b8aec004c6f8e9ce0325'
context:
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
  - '{project-root}/Assets/_Game/ScriptableObjects/Items/CLAUDE.md'
  - '{project-root}/.claude/rules/meshy-mcp.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Nights and interiors are dark and the player has no light of their own. The `LeftArm` torch layer and
`OffhandArmPose.TorchHold` (spec-sword-shield-take-combo) are ready, but there is no torch item, model, prefab or light.

**Approach:** Add a `TorchSO` Offhand item with no stats. It is held lit in the left hand whenever it is equipped (alone,
next to a sheathed weapon, or next to a drawn one-hander or fists). While a two-hander or a bow is drawn it is stowed
unlit on the hip. The art comes from Meshy (one image for both the icon and the 3D input, then image-to-3D), and the
flame from the pack's URP torch VFX. The Storekeeper sells it and Hobb's box holds one.

## Boundaries & Constraints

**Always:**
- The torch never burns out, is not used up and gives no stats (no defense or bonuses, no block values: blocking with it
  is the unshielded block).
- Held = equipped and not (a two-hander or bow drawn). Lit = held and not swimming. Stowed = hidden (user, 2026-11-25),
  flame and light off. Sheathing the two-hander or bow puts the torch back in hand, lit.
- A two-hander or bow no longer pushes the torch out of the Offhand slot (it still pushes out a shield), including on
  save restore.
- `LeftArm` weight follows `OffhandArmRule` only while the torch is held. Stowed or unequipped → `None` (fixes the
  deferred "stowed torch still raises the arm" note). Fist grip while held.
- Light: one warm point light (`#E0A35A`), range ≈ 8 m, subtle flicker, soft shadows on, with a serialized toggle.
- Price: buy 5, sell 1. Shop stock 1 (Storekeeper). Hobb's box +1 torch.
- Meshy: image `nano-banana-pro` (9 credits) from the item-icon prompt template, unlit torch head (pitch-soaked rags,
  iron binding), grey background. Then `meshy-t2` smart topology ≈ 3,000 triangles + `meshy-n1` 2K (15 credits). Cap:
  one regeneration each, 50 credits in total.

**Ask First:**
- Show the generated image to the user before spending on the 3D model.
- Show the torch in game (held idle, walking, at night) before tuning poses, light values or writing docs.
- Changes to `Torch Hold.anim`, `LeftArmMask` or the combo clips.

**Never:** NPC torches (deferred); burn time or durability; a flame baked into the model; a torch animation set;
runtime IK; a lit torch on the hip; changes to shield behaviour.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Equip, unarmed | Torch to Offhand, nothing drawn | In the left fist, lit, torch arm in idle / locomotion | Missing hand socket → warn once, hidden |
| 1H drawn | Sword / axe drawn + torch | Held, lit; attacks and block play the clip's arm | Report blade clipping; no pose fix |
| Fists drawn | No weapon, drawn | Held, lit; punches play the clip's arm | N/A |
| 2H / bow drawn | Draw | Torch hidden, unlit, arm layer 0; back in hand, lit, on sheathe | N/A |
| Equip 2H / bow | Torch equipped | Torch stays equipped | N/A |
| Equip shield | Torch equipped | Shield replaces the torch (same slot) | N/A |
| Swim | Enter / leave water | Flame + light off / back on; arm layer 0 (Swim is FullBody) | N/A |
| Save / load | Torch + 2H equipped | Both restored | N/A |
| Buy / sell | Storekeeper | 5 g / 1 g | N/A |

</frozen-after-approval>

## Code Map

- `ScriptableObjects/Items/ShieldSO.cs` -- template for `TorchSO` (held / stow `WeaponPose`s, `CanEquip`)
- `Scripts/Inventory/EquipmentRules.cs` -- slot, conflict and restore rules (`HasOffhandConflict`)
- `Scripts/Inventory/EquipmentVisuals.cs` -- shield visual path (`RefreshShield` / `PlaceShield` / Drawn-Sheathed children); `OffhandArmPose`
- `Scripts/Core/Animations/HandPoseDriver.cs`, `OffhandArmRule.cs` -- `LeftArm` weight and left fist
- `Scripts/UI/Inventory/ItemDetailFormatter.cs` -- category switch
- `Editor/Items/ItemAssetBuilder.cs` -- model folder → material, world prefab, icon (base `ItemSO`)
- `Editor/NPCs/NpcShopEditor.cs` -- Storekeeper stock; `Scenes/StartingTown.unity` `PlayersZone/HobbsBox` -- Hobb's box
- `Multistory Dungeons 2/Prefabs/VFX/Torch_Fire_FX_01.prefab` -- URP torch flame
- `Scripts/Player/PlayerStateManager.cs` -- `IsSwimming`

## Tasks & Acceptance

**Execution:**
- [x] Meshy -- icon-template image → **user check** → image-to-3D; download into scratchpad, keep FBX + base colour -- art
- [x] `ScriptableObjects/Items/TorchSO.cs` -- `TorchSO : EquipableItemSO`, `heldPose` / `stowPose`, menu Items/Torch -- item type
- [x] `Scripts/Inventory/EquipmentRules.cs` -- Offhand for torches; no conflict with 2H / bow both ways and on restore -- slot rules
- [x] `Scripts/Inventory/EquipmentVisuals.cs` -- torch visual (Drawn = hand + flame + light, Sheathed = hip, hidden for now), pure `ShouldHoldTorch`, `OffhandArmPose` gated on held, swim douse -- placement
- [x] `Scripts/World/TorchLight.cs` (or `Inventory/`) -- flicker + lit / doused switch on the flame -- light
- [x] `ScriptableObjects/Items/...`, `Prefabs/Items/Torch/` -- `Item_Torch` (TorchSO, `TorchHold`, 5 / 1), `_World` pickup + `_Visual` (Drawn: mesh, flame VFX, light; Sheathed: mesh), poses solved in game, icon from the image -- assets
- [x] `Scripts/UI/Inventory/ItemDetailFormatter.cs` -- category "Torch" -- detail panel
- [x] Storekeeper stock +1, `HobbsBox` +1; `ItemCatalog` synced -- availability
- [x] **Play check with the user**, then tune poses and light
- [x] `Assets/Tests/EditMode/` -- `EquipmentRules` torch cases, `ShouldHoldTorch`, lit rule, arm pose gating, formatter category -- regression
- [x] Docs -- `Items/CLAUDE.md`, `Prefabs/Items/CLAUDE.md`, `Scripts/Inventory/CLAUDE.md`, `Core/Animations/CLAUDE.md`, `deferred-work.md` -- knowledge

**Acceptance Criteria:**
- Given the torch bought from the Storekeeper and equipped, at night, then the area around the player is lit and the
  left arm holds it in idle and locomotion.
- Given a sword drawn with the torch, when attacking, then the torch stays in hand, lit, and the sword combo still plays.
- Given the EditMode suite, when run, then it passes.

## Spec Change Log

- **2026-11-25, Meshy (within the agreed cap):** the first `meshy-t2` model, from the diagonal icon image, came out as two
  crossed sticks. An `image_to_image` upright, flat-lit front view of the same torch (9 credits) gave a clean model
  (15 credits): 48 of 50 credits spent. KEEP: the original image as the icon.
- **2026-11-25, user decision after the in-game look:** the stowed torch (two-hander / bow drawn) is hidden instead of
  shown on the hip. `TorchSO.stowPose` (solved: back-left of the belt, head up and back) and the `Sheathed` child stay
  for later.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all pass

**Manual checks:**
- Play mode in StartingTown: equip, then draw and sheathe a sword, a two-hander and a bow; swim; save and load; buy and
  sell; loot Hobb's box. Behaviour as in the I/O matrix, console clean.

## Review (2026-11-25)

Patched: death douses the torch too (`IsSwimming || IsDead`; was relit on a body that died swimming); a drawn left-hand
one-hander also stows it (`ShouldHoldTorch`, tested); `TorchLight.OnEnable` re-applies the lit state and a douse resets
the flicker; stale "on the hip" comments. Deferred (`deferred-work.md`): action-bar instant swap over a held shield, stow at
WeaponSwap rather than the draw start, NPC torch handling. Kept: the direct `PlayerStateManager` → `EquipmentVisuals`
call (same pattern as `PlayerCombat.SetCombatState`); the torch in the Misc filter tab.

## Suggested Review Order

**Held / stowed / lit rule**

- Entry point: held unless a two-hander, bow or left-hand weapon is drawn.
  [`EquipmentVisuals.cs:112`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L112)

- Places the visual: left hand, or hidden on the hip; activates before lighting.
  [`EquipmentVisuals.cs:373`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L373)

- Builds the visual once per torch, applies held / stow poses, finds `TorchLight`.
  [`EquipmentVisuals.cs:329`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L329)

- Re-placed at the draw / sheathe WeaponSwap, like the shield.
  [`EquipmentVisuals.cs:160`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L160)

- Arm pose only while the torch is in hand.
  [`EquipmentVisuals.cs:92`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L92)

**Slot rules**

- Torch takes the Offhand slot.
  [`EquipmentRules.cs:24`](../../Assets/_Game/Scripts/Inventory/EquipmentRules.cs#L24)

- Exempt from the two-hands conflict, on equip and on restore.
  [`EquipmentRules.cs:82`](../../Assets/_Game/Scripts/Inventory/EquipmentRules.cs#L82)

**Douse**

- Swimming or dead → doused; synced on every change, never stuck.
  [`PlayerStateManager.cs:356`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L356)

- Death path sync.
  [`PlayerStateManager.cs:475`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L475)

- Visual side of the douse.
  [`EquipmentVisuals.cs:322`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L322)

**Light**

- Flame particles + point light switch; resets the flicker on douse.
  [`TorchLight.cs:68`](../../Assets/_Game/Scripts/Inventory/TorchLight.cs#L68)

- Re-applies state when a parent re-enables it.
  [`TorchLight.cs:44`](../../Assets/_Game/Scripts/Inventory/TorchLight.cs#L44)

- Perlin flicker and 1 cm light jitter.
  [`TorchLight.cs:96`](../../Assets/_Game/Scripts/Inventory/TorchLight.cs#L96)

**Data and UI**

- Item type: held / stow poses, no stats.
  [`TorchSO.cs:13`](../../Assets/_Game/ScriptableObjects/Items/TorchSO.cs#L13)

- Detail category "Torch".
  [`ItemDetailFormatter.cs:74`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L74)

**Tests**

- Slot rules, held / lit / pose rules, category, flicker.
  [`TorchTests.cs:14`](../../Assets/Tests/EditMode/TorchTests.cs#L14)
