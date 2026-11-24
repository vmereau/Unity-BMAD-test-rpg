# Bow — `BowSO` → `ArrowProjectile`

> Read when touching the bow: aim / nock / fire in `PlayerCombat`, `SMB_BowAimState`, `BowAimCorrection`, spread
> (`BowSpreadMath`), `ArrowProjectile` / `ArrowFlightMath`. Hit resolution and stance rules: `CLAUDE.md` in this folder.

---

With a `BowSO` equipped (`EquippedBow`, read from `EquipmentSystem.GetEquipped(Weapon)`, **not** `_currentWeaponSO` —
only set by `OnVisualsRefreshed`, null after a disable/enable), the bow can't block and has no hip-fire:

- **Aim (spec `spec-bow-aim-draw-fire.md`):** Block started / canceled (right click) → `StartAim` / `StopAim`
  (`PlayerStateManager.SetAiming` → `IsAiming` bool → UpperBody layer `BowDrawArrow_State` → looping `BowAim_State`).
  `StartAim` is gated by `CanAim()`. `UpdateBowAim` ends the aim as soon as `CanAim()` fails or the bow is unequipped
  (dodge, jump, R, menu, death, sheathed). `StopAim` also runs on an unblocked hit (`TryReceiveHit`),
  `HandleVisualsRefreshed`, `OnDisable` and at the top of `OnDrawWeaponStarted` (R ends the aim, then switches;
  `CanSwitchWeapon` / `CanAim` also exclude each other and a still-raised block). A held right click never resumes the
  aim (only `Block.started` starts it).
- **Nock:** `SMB_BowAimState.OnStateEnter` → the Animator GO's `IBowNockListener` (`AnimationEventReceiver` →
  `OnBowAimEntered()`; NPCs: `EntityAnimationEventReceiver` → `EntityArcher`) sets `_arrowNocked` (ignored when not aiming).
  The `bowNockTimeout` watchdog forces it if that enter is lost — a shot on a **forced** nock skips `PlayBowFire`
  (the layer isn't in `BowAim_State`, so `BowFire` would latch and replay the release + re-nock on the next enter).
- **Shot:** `TryAttack` requires `IsAiming && _arrowNocked`, then `TryFireBow` (returns bool). Success clears the nock
  and fires `BowFire` (`BowFire_State` → `BowDrawArrow_State` → re-nock at the aim enter). A refused shot keeps the nock.
  Ignored while a melee swing is still playing (`IsAttacking`).
- **Aim correction (spec `spec-bow-aim-spine-correction.md`):** the Mixamo archer clips are side-on (clip hips +90°,
  spine −90°), but on the masked UpperBody layer the hips come from locomotion, so no fixed body yaw lines the bow up.
  `BowAimCorrection` (Player root, and `NPC_base Variant` root for NPC archers, `LateUpdate`) reads the same-GO
  `IBowAimSource` (`PlayerCombat`: crosshair point on the camera ray; `EntityArcher`: target chest on the chest → target ray —
  spec-ai-ranged-combat-style) and blends a weight toward `IsBowAiming` (`aimBlendSpeed`, unscaled — same as
  the aim camera). It then turns Spine → Chest → UpperChest (yaw about world up, then pitch about the line's horizontal
  right axis) so the **chest-locked arrow line** points at the source's aim point (player: same ray as the shot; false
  without a bow or camera). Each bone takes `AimCorrectionMath.BoneStep` = what's left ÷ bones left (re-measured per
  bone), clamped to `aimCorrectionMaxAnglePerBone`, plus one residual pass on UpperChest (the hand origin moves with
  it). Blend is linear: `(1 − weight)` of the animated-pose delta is held back. Remaining yaw > 120° (target behind the
  line, e.g. aim started while running at the camera) fades the correction out by 160°. Aim points closer than
  `aimCorrectionMinDistance` past the bow are pushed out along the source's view ray. Bone writes only — no animator params.
  Math is covered by `AimCorrectionMathTests`.
  - **`[DefaultExecutionOrder(200)]`:** `CinemachineBrain` runs at **100** (its MonoImporter order). A tie leaves the
    camera pose a frame stale.
  - **No stacking:** the component caches the animated local rotations and the ones it wrote. If a bone still holds its
    own write (the Animator didn't evaluate — paused, culled), it restores the animated pose before correcting again.
    `OnDisable` does the same.
  - **The arrow line is calibrated, not measured live.** A live right-hand → left-hand line swings during `Draw arrow` /
    `Fire arrow`, when the hand goes to the quiver. `_arrowLineLocal` / `_arrowOriginLocal` = left − right hand / the right
    hand, in UpperChest local space, from `Aim Overdraw` sampled at 50 % on the Player prefab (still valid for the
    wrist-baked `Bow Aim Overdraw.anim`: its offsets turn the hand, not move it — spec-bow-hand-grip)
    (`PrefabUtility.LoadPrefabContents` + `clip.SampleAnimation(root, t)`). Re-calibrate if the bow clips or the
    character model change. Selected in play, the component draws the line as a cyan gizmo.
  - Stance: `PlayerController` faces camera yaw + `PlayerConfigSO.aimStanceYaw` (45°); the
    correction makes up the rest.

- **Spread (spec `spec-bow-aim-spread-arrow-drop.md`):** `_aimProgress` 0 (cone `aimSpreadMaxAngle`, 4° in `Data/Config/CombatConfig.asset`; code default 5°) → 1 (0°, perfect).
  Full until the nock; then `BowSpreadMath.StepProgress` shrinks it over `aimSpreadShrinkDuration` (1.5 s) while still, or
  widens it back (`aimMoveWidenMultiplier` × that rate) while the CharacterController's horizontal speed >
  `aimMoveSpeedThreshold`. Reset on `StartAim` and after every shot. Scaled time. The shot ray = `SampleCone` (uniform
  over the disk) around the crosshair ray, through `GetAimPoint(maxDistance, spreadDeg)`, then `ClampToCone` around bow → centre point (a sampled ray grazing a near
  edge would otherwise bend the shot far past the cone); `TryGetAimPoint` (spine correction) keeps the centre ray. The
  camera (`AimCamera`, shared with the ring) is re-resolved lazily after a reload. `TryGetAimSpread` feeds the HUD ring (`AimSpreadUI`). Math: `BowSpreadMathTests`.

Gate order: `CanAttack()` (drawn) → `fireCooldown` → stamina `HasEnough` → ammo `CountOf > 0` (else `OnCombatNotification`
"Out of arrows" toast) → `Consume` stamina (checked) → `TryConsume` ammo → fire. A refused shot spends nothing.

- Aim = crosshair ray (`ViewportPointToRay(0.5, 0.5)`) on Default + CharacterHitbox, skipping the player's own colliders,
  hits closer than the player along the ray (wall behind the OTS camera) and non-hurtbox triggers; no hit → `maxAimDistance`.
  Launch origin = `EquipmentVisuals.ActiveWeaponGO`.
- **Triggers count only on the CharacterHitbox layer** (aim and arrow) — `GetComponentInParent<IDamageable>()` on any trigger
  would let an NPC's perception / interaction sphere take the hit.
- Damage is snapshotted at fire time: `DamageMath.WeaponRaw(bow.damageBonus, Dexterity)` (the bow's `scalingStat`);
  the target's armor applies on impact.
- `ArrowProjectile`: no Rigidbody; integrates its own velocity. Straight for `BowSO.gravityDelay` (0.25 s), then
  `ArrowFlightMath.GravityFactor` ramps `Physics.gravity × gravityScale` (0.5) in over `gravityRampTime` (0.5 s) — at
  40 m/s ≈ 0 m drop at 10 m, 0.03 m at 20 m, 0.65 m at 40 m, 2.5 m at 60 m. Faces its velocity every frame; per-frame `SphereCastNonAlloc`
  along this frame's displacement (closest valid hit — results are unsorted).
  `IDamageable` → the shooter's `onHit` callback (player: `PlayerCombat.ResolveHit`, same `TryReceiveHit` / sneak rule as
  melee; NPC archers: `EntityArcher`, faction-filtered — `Scripts/AI/RANGED_EQUIPMENT.md` → Ranged), destroyed. Solid collider →
  spawns `ammoItem.worldItemPrefab` kinematic at the impact pose (a normal pickup, not saved), **moved into the hit
  collider's scene** — `Instantiate` targets the active scene (Core), where a reload would leave a duplicate arrow. **Prefab frame: root = arrow
  tip, shaft along −Z** — `Projectile_Arrow` and `Item_Arrow` share it so the stuck arrow lines up.
