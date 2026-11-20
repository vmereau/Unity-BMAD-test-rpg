# Execute stomp — F (`Execute` action)

> Read when touching `PlayerExecution`, `IExecutable`, `ExecuteMath` or the `ExecuteImpact` / `ExecuteEnd` clip events.
> Spec: spec-execute-unconscious-npc.

---

`PlayerExecution` (Player root) finishes off an **unconscious NPC** (`IExecutable`, implemented by `EntityHealth` — rules in
`Scripts/AI/CLAUDE.md` → Unconscious). No lock-on: lock-on is hostile-only and knockouts only happen to non-hostiles.

- **Target** (only while `PlayerStateManager.CanExecute()`: in combat, any weapon incl. bow; not busy / airborne / swimming /
  attacking / blocking / aiming / dodging / switching). A focused interactable decides (`OnInteractionFocusChanged`): its
  own `IExecutable` if in reach, else **no target**. With no focus: `OverlapSphereNonAlloc` (`executeRange` +
  `executeScanPadding`) on CharacterHitbox, nearest in reach. In reach = `CanBeExecuted` + `ExecuteMath.IsCandidate`
  (`executeRange` on the ground plane, `executeConeHalfAngle`) + `HasAccess` (`executeVerticalReach`, and no
  `executeOcclusionMask` hit between the player and the chest **except the body's own colliders** — humanoid ragdoll bones
  are solid Default colliders and the chest point lies inside them; a plain `Linecast` rejected every body).
  `CurrentTarget` / `CurrentTargetPoint` are polled by the HUD prompt, so the `[F]` row always matches what F does.
- **F started** → re-scan → `ExecuteMath.AlignStance` (keeps the approach bearing, or the facing when standing on the body;
  puts `executeFootOffset` — the clip's landing foot, measured (−0.45, 0, 0.88) — on the chest) → `SetExecuting(true)`; the
  player slides with the CharacterController and turns over `executeAlignDuration`, with `Physics.IgnoreCollision` against
  the body's solid colliders for the whole stomp (its limbs stopped the slide short). `IsExecuting` is part of `IsBusy`;
  dodge / E / action bar / tabs / lock-on rotation check it themselves.
- **Kill = the `ExecuteImpact` clip event only** (`AnimationEventReceiver` → `OnExecuteImpact` → `target.Execute`). Before
  it — checked every `Update` **and again in the event** (a hit resolved later in the frame) — cancel on: F released,
  `!CanContinueExecute` (dead, incapacitated, held, airborne, swimming, cursor unlocked), any damage
  (`PlayerHealth.DamageRevision` changed — a heal in the same frame can't hide it), `SaveSystem.IsLoading` (a kill during
  the async reload would land in the restored facts), or the target no longer executable (getting up). Cancel =
  `IsExecuting` false, 0.2 s blend out, the NPC stays down. A foot farther than `executeImpactTolerance` from the chest only
  logs a warning (retune the offset). After the impact, release does nothing.
- **End:** `ExecuteEnd` event, or the watchdog (`executeClipDuration` + `executeWatchdogMargin`, warns). Events earlier than
  `executeMinEventDelay` are ignored: a just-cancelled clip blending out can still fire its impact. `PlayerAnimationDriver`
  re-pushes `IsExecuting` every frame (a controller swap resets parameters).
- Test the stomp timing in play with `EditorApplication.Step()` in a paused editor — an unfocused editor barely ticks.
