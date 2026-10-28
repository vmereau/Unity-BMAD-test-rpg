---
title: 'Block interaction and name tags through walls'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
baseline_commit: 'aa4bcf1'
context: ['{project-root}/_bmad-output/project-context.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `InteractionSystem` sphere-casts with `_raycastMask` (Player.prefab: Interactable + Harvestable only), so walls are never tested — the bed (and any interactable / NPC name tag within range) can be focused and used through a house wall. Wall colliders are fine (`SM_Wall_A_*`, layer Default).

**Approach:** Keep the existing sphere-cast for candidates, then drop every candidate whose line from the camera to its hit point is blocked by a collider on a new `_occlusionMask` (Default only for now). Apply the same filter to the name-tag scan.

## Boundaries & Constraints

**Always:** Occlusion probe ignores triggers (`QueryTriggerInteraction.Ignore`). Probe stops slightly short of the hit point so the floor/surface the target rests on doesn't block it. A sphere-cast hit with `distance == 0` (cast started inside the collider) skips the probe (unoccluded). Pure math goes in `InteractionFocus` with EditMode tests. Non-alloc physics only.

**Ask First:** Adding layers other than Default to the occlusion mask (e.g. Interactable so closed doors hide what's behind them).

**Never:** Changing `_raycastMask`, wall prefabs, layers, or `InteractionConfig` ranges. No per-frame allocations.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Through wall | Bed within 6 m, Default wall between camera and bed | No focus, no prompt, E does nothing | N/A |
| Clear view | Bed in the open, nothing between | Focus + `Sleep` prompt as before | N/A |
| Resting on floor | Pickup lying on a Default floor | Still focusable (probe backoff) | N/A |
| Initial overlap | Sphere-cast hit distance 0 | Treated as visible | N/A |
| Name tag | NPC within 8 m behind a wall | Name tag hidden | N/A |
| Mask unset | `_occlusionMask == 0` | Old behavior (no occlusion) | `Awake` warning |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/World/InteractionSystem.cs` -- interaction + name-tag sphere-casts (Update), `_raycastMask`
- `Assets/_Game/Scripts/World/InteractionFocus.cs` -- static pure helpers tested by `InteractionSystemTests`
- `Assets/Tests/EditMode/InteractionSystemTests.cs` -- EditMode tests for `InteractionFocus`
- `Assets/_Game/Prefabs/Player/Player.prefab` -- holds the `InteractionSystem` serialized masks
- `Assets/_Game/Scripts/World/CLAUDE.md` -- `InteractionSystem` row to document the occlusion rule

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/World/InteractionFocus.cs` -- add `OcclusionProbeLength(float hitDistance, float distanceToPoint, float backoff)` returning 0 when `hitDistance <= 0`, else `max(0, distanceToPoint - backoff)` -- pure, testable probe rule
- [x] `Assets/_Game/Scripts/World/InteractionSystem.cs` -- add `[SerializeField] LayerMask _occlusionMask = 1` (Default) + `Awake` warning when 0; private `IsOccluded(Vector3 origin, RaycastHit hit)` doing a `Physics.Raycast` from `origin` toward `hit.point` for the probe length (backoff const 0.05 m); skip occluded candidates in both the interaction loop and the name-tag loop -- the fix
- [x] `Assets/_Game/Prefabs/Player/Player.prefab` -- set `_occlusionMask` to Default explicitly on the `InteractionSystem` -- serialized value visible/authoritative
- [x] `Assets/Tests/EditMode/InteractionSystemTests.cs` -- tests for `OcclusionProbeLength`: initial overlap → 0, normal → distance − backoff, shorter than backoff → 0
- [x] `Assets/_Game/Scripts/World/CLAUDE.md` -- note the occlusion probe and `_occlusionMask` in the `InteractionSystem` row

**Acceptance Criteria:**
- Given the player outside the starting-town house facing the wall behind which the bed stands, when within 6 m, then no `Sleep` prompt appears and E does nothing.
- Given the player inside the house looking at the bed, when in range, then the `Sleep` prompt and outline appear as before.
- Given all EditMode tests, when run, then they pass.

## Design Notes

Probe toward `hit.point`, not along `ray.direction`: sphere-cast hits are offset from the centre ray by up to `scanRadius`, so a centre-ray test could hit a door frame while the bed is plainly visible through the doorway.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all pass
- Unity MCP `read_console` after compile -- expected: no errors

**Manual checks (if no CLI):**
- Play StartingTown: bed not focusable through the house wall; focusable inside.

## Suggested Review Order

**Occlusion probe**

- Entry point: per-candidate raycast toward the hit point; own collider in front counts as visible.
  [`InteractionSystem.cs:239`](../../Assets/_Game/Scripts/World/InteractionSystem.cs#L239)

- Pure probe-length rule: skip on initial overlap, stop 5 cm short of the target.
  [`InteractionFocus.cs:36`](../../Assets/_Game/Scripts/World/InteractionFocus.cs#L36)

**Applied to both scans**

- Interaction scan drops occluded candidates before angle ranking.
  [`InteractionSystem.cs:149`](../../Assets/_Game/Scripts/World/InteractionSystem.cs#L149)

- Name-tag scan uses the same filter, before dedup.
  [`InteractionSystem.cs:189`](../../Assets/_Game/Scripts/World/InteractionSystem.cs#L189)

**Configuration**

- New mask defaults to Default; Awake warns when empty.
  [`InteractionSystem.cs:21`](../../Assets/_Game/Scripts/World/InteractionSystem.cs#L21)

- Player prefab serializes the mask explicitly.
  [`Player.prefab:396`](../../Assets/_Game/Prefabs/Player/Player.prefab#L396)

**Peripherals**

- Probe-length tests.
  [`InteractionSystemTests.cs:291`](../../Assets/Tests/EditMode/InteractionSystemTests.cs#L291)

- What does / doesn't occlude, documented.
  [`CLAUDE.md`](../../Assets/_Game/Scripts/World/CLAUDE.md)
