---
title: 'Area 1 mafia buildings and arena overlooks'
type: 'feature'
created: '2026-11-11'
status: 'done'
baseline_commit: '27544dc'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent â€” do not modify unless human renegotiates">

## Intent

**Problem:** The hand-arranged mafia settlement lacks its larger communal building, boss residence and arena viewing spaces. The annotated aerial reference identifies their intended relationship to the square and fight pit.

**Approach:** Extend the saved settlement with a large rough timber multipurpose building at the right-hand marked site, rudimentary public viewing platforms beside the arena, and a better-built timber boss house at the left-hand marked site. Give the boss house an enclosed rear garden and a private platform projecting over the arena, with room for the boss and an eventual announcer.

## Boundaries & Constraints

**Always:** Follow the annotated image's spatial arrangement relative to the existing huts, square, palisade and arena; left/right refer to the image rather than world axes. Face the large building's entrance towards the mafia square as the red arrow indicates. Keep its interior flexible for quarters, warehouse or barracks; the final use remains a later design decision. Make the boss residence visibly better than worker housing through sounder timber framing, a complete roof and a more comfortable porch/overlook, while keeping it below Company quality. Provide accessible entrances, platform access and an enclosed rear area connected to the boss house. Preserve the existing hand-owned settlement and pit, gameplay objects and quest references. Keep both settlement ownership flags enabled. Use the live scene and measured ground heights for placement.

**Ask First:** Fitting the marked buildings requires deleting or moving existing huts, altering the palisade, reshaping the arena or terrain, displacing quest content, or closing an existing circulation route. Minor footprint and height adjustments inside the marked open areas are implementation choices.

**Never:** Rebuild the worker settlement, terrain or unrelated sites. Repurpose the existing lakeside GangHouse or move Brann's stash/routine. Add new NPCs, announcer logic, quests, furniture interactions or final art. Commit to a single gameplay function for the multipurpose building.

</frozen-after-approval>

## Code Map

- `docs/unity screens/blockout/mafia zone boss house, mob quarters or warehouse, arena mafia spectators plaftorm.PNG`: authoritative annotated placement reference.
- `Assets/_Game/Scenes/StartingTown.unity`: live mafia huts under `Area1_Blockout/TentCamp/MafiaZone`; hand-owned fight pit and existing quest GangHouse.
- `Assets/_Game/Editor/World/Area1WorkerHut.cs`: existing rough boarded walls, enterable shells and timber roof proportions.
- `Assets/_Game/Editor/World/Area1CampInterior.cs`: existing better-built shell, foundations and terrain-following entry ramp.
- `Assets/_Game/Art/Materials/Blockout_Timber.mat`, `Blockout_Canvas.mat`, `Blockout_Stone.mat`, `Blockout_Crop.mat`: established blockout palette.
- `Assets/_Game/Scenes/CLAUDE.md`, `docs/World/Areas/area-001-company-encampment.md`: scene ownership and area descriptions.

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scenes/StartingTown.unity`: measure the marked open spaces, terrain, adjacent housing and pit access; record a scene backup and baseline object inventory before mutation.
- [x] `Assets/_Game/Editor/World/Area1MafiaBlockout.cs`: add a narrowly scoped editor authoring operation for the new mafia additions, using existing geometry helpers/materials where suitable. Guard against replacing pre-existing named additions; keep later hand edits authoritative. Use editable ordinary scene geometry without a runtime generator. Place all additions under dedicated MafiaZone children and register editor undo.
- [x] `Assets/_Game/Scenes/StartingTown.unity`: author `MafiaQuarters`, `ArenaSpectatorPlatforms`, `BossHouse`, `BossGarden` and `BossOverlook` in the annotated sites. Build collidable floors, supports, entrances and safe access; preserve public square circulation and the arena opening. Include simple seating/announcer standing space on the boss overlook and restrained garden markers.
- [x] `Assets/_Game/Scenes/StartingTown.unity`: verify views, ground contact, collision and circulation; rebake the existing terrain NavMesh through the project's asset-preserving editor bake path and save the affected scene/navigation asset.
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `docs/World/Areas/area-001-company-encampment.md`: document new hand-owned structures and distinguish BossHouse from the existing GangHouse. Record validation evidence and any remaining manual checks in this spec.

**Acceptance Criteria:**
- Given the annotated aerial image, when viewing the completed area overhead, then the large square-facing building, rough spectator platforms, boss house and enclosed rear overlook occupy the corresponding marked spaces.
- Given the mafia square, when approaching the large building, then its main doorway faces the square, has at least 2.1 m headroom and leads into an unobstructed flexible interior.
- Given the public spectator platforms, when reaching them from the mafia side, then their access is traversable and a standing spectator sees the fighting floor without the platform sealing the arena entrance.
- Given the boss house, when following its rear access, then the enclosed garden connects to a private projecting overlook with space for two occupants and a useful view of the fighting floor.
- Given ground-level views, when comparing worker huts, mafia communal building and boss residence, then the boss house reads as more comfortable timber construction without Company-grade grandeur.
- Given the saved baseline, when comparing unaffected roots and terrain, then existing huts, tarp bays, gameplay references, quest GangHouse, palisade and pit geometry retain their authored state.

## Spec Change Log

- Implementation validation, 2026-11-12: the compact communal footprint preserves both adjacent hut approaches and the fire benches. Boss stairs meet the north edge of the porch landing at z=13.4; extending them beneath that landing created an excessive riser. Stair posts remain vertical, with stepped privacy panels and matching navigation volumes. Keep the raised floor, terrain/rock preservation and all five dedicated roots.

## Design Notes

The current scene, rather than historical layout plots, owns this settlement. Keep the new subroots hand-owned too. The source image is a placement reference, not a request to reconstruct its camera or terrain. Sample foundations at footprint corners; platform supports may descend to the bank while their deck projects above it. Use entry ramps or risers below the player's 0.3 m step offset, with clear secondary passages around 1.5 m where space permits. Check palisade proximity and roof access so the new buildings do not create a route over the Company boundary.

## Verification

- Check Unity compilation and console after authoring code changes.
- Capture comparable overhead and ground-level screenshots of all new structures; inspect arena sightlines from public and private spectator eye positions.
- Check colliders and walking access from the square to both buildings, the garden and both viewing areas; inspect baked NavMesh connectivity and perform focused player traversal where practical.
- Compare baseline inventory and transforms for existing housing/props, terrain data and quest references. Inspect the final diff for unrelated changes.
- Confirm the saved NavMesh reference persists and no duplicate bake asset appears. Record actual checks separately from manual follow-up; do not claim play verification from images or NavMesh alone.

### Recorded evidence (2026-11-12)

- Saved `StartingTown.unity` contains all five additions. The one-time command was invoked a second time and refused before mutation (947 descendant transforms before/after that check). No runtime generator or new NPC/gameplay component was added.
- Before mutation, captured 9,581 existing GameObjects and 29,220 components. After saving, compared all 29,778 original Unity YAML records against baseline `27544dc`: zero removed records; only MafiaZone transform `2144582101` differs, solely by five added child links. All existing huts, props, quest references, pit rocks and unrelated roots retain their serialized content. Baseline scene backup is reproducible from that commit.
- Terrain SHA256 remains `6CFDBC5750EDE2E96B6174EABDE590CD34AD0A34A753FF5DBFCD9C9EEE9A7CAB`; Core remains `324A8DD77ED0561FBAF27CA7D942F8B9D61D2026914B17FDB0DC1F2EE56A5981`. ProjectSettings and both layout ownership flags remain unchanged.
- Rebuilt navigation using `NavMeshAssetManager.StartBakingSurfaces` on `Terrain_StartingArea`; the existing `NavMesh-Terrain_StartingArea.asset` remains assigned, with no duplicate bake asset.
- Play-mode verification drove the real player CharacterController (1.7 m height, 0.3 m radius, 0.3 m step offset) with collision and downward motion along baked path corners. Eight routes passed from the square: quarters, public south deck, public north deck, boss house, boss garden, boss overlook, existing hut 25 approach and existing hut 27 approach. Endpoints were within 0.2 m horizontally and 0.4 m vertically. This tests capsule traversal, not manual keyboard/camera feel.
- Arena entry remains walkable. A direct baked-corner walk snagged on an unchanged rim-rock collider near (45.82,31.5); an explicit detour via (43,34.8), (52.5,36), then (52.5,34) passed with the same capsule. Existing render-mesh/collider disagreement is deferred rather than editing the hand-owned rocks.
- Overhead, entrance, boss compound and standing-eye arena captures were inspected. Doors provide 2.3 m/2.4 m headroom, risers stay at or below 0.24 m, and privacy screens protect the raised approach near the palisade. Exhaustive jumping exploits and seated camera comfort remain human playtest considerations.
- Unity compiled the authoring script successfully. The editor restart produced Burst-server connection errors unrelated to this source; no authoring compilation error remained. A broad live GlobalObjectId comparison stalled the editor and required an approved restart; the final preservation check uses saved-file comparison instead. The temporary recovery hook was removed and the temporary background-play setting restored.
- `git -c core.whitespace=-blank-at-eol diff --check` passes. Default whitespace checks report Unity's serialized empty `m_Name: ` fields; existing scene records were not reformatted to suppress these.

Captures:
- [Overview](../../docs/unity%20screens/blockout/mafia-zone-overview.png)
- [Quarters entrance](../../docs/unity%20screens/blockout/mafia-quarters-entrance.png)
- [Boss house, garden and overlook](../../docs/unity%20screens/blockout/mafia-boss-house-garden-overlook.png)
- [Boss arena view](../../docs/unity%20screens/blockout/mafia-boss-arena-view.png)
- [Public arena view](../../docs/unity%20screens/blockout/mafia-public-arena-view.png)

### Review outcome

Three independent reviewers (adversarial, edge cases, acceptance) returned no actionable findings. The acceptance auditor independently confirmed preservation of every original scene record except the five added parent links. The existing arena collider/navigation mismatch is recorded in deferred work.

## Suggested Review Order

- One-time entry validates ownership and refuses to overwrite hand edits.
  [Area1MafiaBlockout.cs:24](../../Assets/_Game/Editor/World/Area1MafiaBlockout.cs#L24)

- Raised boss compound clears the existing bank and rim rocks.
  [Area1MafiaBlockout.cs:66](../../Assets/_Game/Editor/World/Area1MafiaBlockout.cs#L66)

- Stairs meet the landing edge; vertical screens preserve capsule clearance.
  [Area1MafiaBlockout.cs:275](../../Assets/_Game/Editor/World/Area1MafiaBlockout.cs#L275)

- Scene instructions distinguish the new boss residence from the quest gang house.
  [CLAUDE.md:103](../../Assets/_Game/Scenes/CLAUDE.md#L103)

- Recorded evidence separates capsule checks, preserved content, and remaining human playtests.
  [spec-area-1-mafia-zone.md:72](spec-area-1-mafia-zone.md#L72)

