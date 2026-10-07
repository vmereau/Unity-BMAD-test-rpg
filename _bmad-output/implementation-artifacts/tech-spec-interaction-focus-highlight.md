---
title: 'Interaction Focus Highlight — Outline & Anchored Prompt'
slug: 'interaction-focus-highlight'
created: '2026-10-07'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C#', 'URP 17.6 (Forward+, RenderGraph)', 'HLSL', 'uGUI + TextMeshPro', 'Unity Test Framework (EditMode)']
files_to_modify:
  - 'Assets/_Game/Game.asmdef'
  - 'Assets/_Game/Scripts/Rendering/InteractionOutlineFeature.cs (new)'
  - 'Assets/_Game/Shaders/InteractionOutlineMask.shader (new)'
  - 'Assets/_Game/Shaders/InteractionOutlineComposite.shader (new)'
  - 'Assets/Settings/PC_Renderer.asset'
  - 'ProjectSettings/TagManager.asset (Outline rendering layer)'
  - 'Assets/_Game/Scripts/World/InteractionHighlight.cs (new)'
  - 'Assets/_Game/Scripts/World/InteractionFocus.cs (new, pure static helpers)'
  - 'Assets/_Game/Scripts/World/InteractionSystem.cs'
  - 'Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs'
  - 'Assets/_Game/ScriptableObjects/Events/GameEventSO_InteractionFocus.cs (new)'
  - 'Assets/_Game/Data/Events/OnInteractionFocusChanged.asset (new)'
  - 'Assets/_Game/Scripts/UI/HUD/InteractionPromptUI.cs (new)'
  - 'Assets/_Game/Prefabs/UI/InteractionPrompt.prefab (new)'
  - 'Assets/_Game/Prefabs/UI/UICanvas.prefab'
  - 'Assets/_Game/Prefabs/Player/Player.prefab'
  - 'Assets/_Game/Prefabs/Items/** (9 ItemPickup prefabs)'
  - 'Assets/Tests/EditMode/InteractionSystemTests.cs'
code_patterns:
  - 'SphereCastNonAlloc focus scan with _previousInteractable change detection'
  - 'GameEventSO<T> channel with runtime-ref payload struct (DoorOpenRequestData pattern), one concrete type per file'
  - 'Awake validation → GameLog.Error + enabled=false; OnDisable null guards'
  - 'URP ScriptableRendererFeature + RecordRenderGraph'
  - 'HUD scripts subscribe to channels in OnEnable/OnDisable, never poll'
test_patterns:
  - 'Tests.EditMode NUnit, pure logic only; class naming [System]Tests'
  - 'InteractionSystemTests currently mirrors private logic in local helpers — replace with direct calls to extracted static InteractionFocus helpers'
---

# Tech-Spec: Interaction Focus Highlight — Outline & Anchored Prompt

**Created:** 2026-10-07

## Overview

### Problem Statement

When several interactables are close together (e.g. many item pickups on the floor), the player
cannot easily tell which one `[E]` will act on:

- `InteractionSystem` shows the world-space `EntityUI` name tag of **every** interactable within
  `nameRange`, so a cluster of items produces a cluster of overlapping names.
- The interaction prompt is an `OnGUI` label drawn at a fixed screen position (55% height) reading
  only `[E] <Verb>` — it does not name its target. `OnGUI` for gameplay UI also violates
  `project-context.md` (OnGUI is deprecated for gameplay UI; the only permitted exception is a
  dev-only overlay under `#if DEVELOPMENT_BUILD || UNITY_EDITOR`, which the current prompt is not).
- Nothing in the 3D world marks the focused object; the only feedback is the crosshair turning yellow.

### Solution

1. **Screen-space outline** on the currently focused interactable, rendered by a URP
   `ScriptableRendererFeature` (Unity 6 RenderGraph API) that draws renderers on a dedicated
   `Outline` Rendering Layer into a mask and composites a constant-width edge.
2. **Opt-in `InteractionHighlight` component** on an interactable's root decides *which* renderers
   get outlined (explicit `Renderer[]`, empty = all child renderers; no component = no outline).
   Lets doors/chests outline only a handle/lock, or nothing, while staying generic for every
   `IInteractable`. Optional per-component color override; default color in `InteractionConfigSO`.
3. **uGUI prompt card** (TextMeshPro) anchored to the focused target on screen, showing the
   `[E]` key badge, the action verb, and the target's name. Replaces the `OnGUI` prompt.
4. **Name-tag declutter:** non-focused names are hidden. Living entities (monsters/NPCs) keep their
   `EntityUI` name + health bar within `nameRange` as today.

### Scope

**In Scope:**

- URP outline renderer feature + shader(s), `Outline` Rendering Layer, registration on `PC_Renderer`
- `InteractionHighlight` component (renderer list, empty → all child renderers, optional color override)
- Default outline color (and width if needed) in `InteractionConfigSO`
- `InteractionSystem` focus-change hooks: enable highlight on new target, clear on previous
  (also on disable / target destroyed / target no longer `CanInteract`)
- uGUI prompt card prefab + script on the HUD canvas, anchored above the target's bounds via
  `Camera.WorldToScreenPoint`, clamped to screen edges, hidden when cursor unlocked / menus open
- Remove the gameplay `OnGUI` prompt from `InteractionSystem`
- Name-tag declutter: non-entity interactables (items, containers, doors) show their name only on the
  focused prompt card. They have no `EntityUI` today, so this needs no change. Entity name + health
  bar behavior in `nameRange` stays as it is.
- Fix the re-interact window: after `Interact()`, force a rescan on the next frame and skip
  destroyed targets, so a double `[E]` press cannot pick up a destroyed item twice
- Add `InteractionHighlight` to existing item pickup prefabs
- EditMode tests where logic is testable (e.g. renderer collection / layer bit toggling, prompt text)

**Out of Scope:**

- Rarity / quality-driven colors
- Ambient "reveal all loot" mode (Witcher-sense style)
- Gamepad button glyphs / dynamic key rebinding display
- Outline visible through occluders (outline follows the visible silhouette only)
- Prompt card resolution scaling (UICanvas has no CanvasScaler)
- Per-prefab highlight authoring for doors, NPCs, containers beyond what the component enables
  (opt-in, can be done later per prefab)

## Context for Development

### Codebase Patterns

- `InteractionSystem` (`Game.World`) sphere-casts from screen center every `_config.scanInterval`,
  picks the smallest-angle `IInteractable` with `CanInteract`, exposes `CurrentInteractable`, and
  tints `_crosshairImage` on focus change (`best != _previousInteractable`).
- A second sphere cast at `_config.nameRange` shows `EntityUI` (world-space name/health) on every hit.
- `IInteractable` contract: `InteractPrompt`, `NameTag`, `CanInteract`, `Interact()`. Implemented by
  `InteractableObject`, `ContainerInteractable`, `ItemPickup`, `DoorInteractable`, `EntityPresence`
  (+ `NPCPresence`).
- UI rules (`Scripts/UI/CLAUDE.md`): TextMeshPro only, dynamic UI in its own Canvas, CanvasScaler
  1920×1080 Scale With Screen Size, avoid per-frame layout/text changes.
- `project-context.md`: OnGUI deprecated for gameplay UI.
- URP 17.6 (Unity 6.6): renderer features must use the RenderGraph API (`RecordRenderGraph`).
- **Investigation findings (Step 2):**
  - `InteractionSystem` lives on the **Player root** (`Prefabs/Player/Player.prefab`). The crosshair
    `Image` is `UICanvas/Crosshair` inside the nested `UICanvas.prefab`. `UICanvas` has **no
    CanvasScaler** (removed in story 6-1). A `CanvasScaler` only works on a root canvas, so a nested
    prompt canvas cannot scale by itself. The prompt card gets a nested child `Canvas` (dynamic UI
    isolation rule) and uses fixed pixel sizes, like the rest of the HUD. Resolution scaling is out
    of scope.
  - `PC_RPAsset`: MSAA off (`m_MSAA: 1`), HDR on, depth texture required. Because MSAA is off,
    the outline mask can share the camera depth attachment without an MSAA mismatch.
  - Config asset: `Assets/_Game/Data/Config/InteractionConfig.asset`. None of the 9 item prefabs is
    a variant of `Base Item`; each one wraps a different model prefab.
  - `Game.Core.GameConstants` holds structural string constants (e.g. layer names). The global
    outline color property name goes there.
  - **`EntityUI` is only on `Entity_base.prefab`.** No item, container or door has one, so today only
    entities show a name tag in `nameRange` and items show **no name at all**. "Hide non-focused
    names" is therefore already true for items. The prompt card is where item names first appear.
    The entity name-range loop stays unchanged.
  - `ItemPickup` (`Game.Inventory`): `InteractPrompt` = `"Pick Up"` (or `_promptOverride`),
    `NameTag` = `_item?.itemName ?? "item"`, `CanInteract => true`, `Interact()` destroys the GO.
    The focused target can be destroyed between scans, so highlight and prompt must handle a destroyed
    `UnityEngine.Object` (`(Component)x == null`).
  - Item prefabs using `ItemPickup` (9): `Items/Base Item`, `Armors/Armor`, `Armors/Helmet`,
    `TestItem_Health_Potion`, `TestItem_Mana_Potion`, `Tomes/Tome_PowerStrike`,
    `Weapons/OneHandedSword/big sword/big sword_World`, `Weapons/Swords/Axe/Axe_World`,
    `Weapons/Swords/SwordBase/SwordBase_World`. Several have no renderer on the root and get their
    meshes from nested model prefabs (e.g. Multistory Dungeons `Potion_01`), so "empty list → all
    child renderers" is required.
  - Interactable layer = 8 (on the `Base Item` root).
  - **Cross-system rule:** `project-context.md` requires `GameEventSO<T>` channels between systems.
    `World → UI` must go through a new channel. Precedent: `DoorOpenRequestData` carries a runtime
    scene ref in a payload struct. Memory rule: each concrete `GameEventSO_*` type lives in its own
    file. Event assets go in `Assets/_Game/Data/Events/` and are named `On<EventName>`.
  - Screen/menu state: `UIScreenManager` + `IScreenPanel`. `CursorManager.IsLocked` is the existing
    gate used by `InteractionSystem.LateUpdate`. The prompt hides when the cursor is unlocked.
  - **URP:** `PC_Renderer` has one feature (SSAO) and `m_RenderingMode: 2` (Forward+).
    `TagManager.m_RenderingLayers` contains only `Default`, so an `Outline` layer has to be added
    (index 1). `Game.asmdef` references only InputSystem, TMP and Cinemachine. It must add
    `Unity.RenderPipelines.Universal.Runtime` and `Unity.RenderPipelines.Core.Runtime`.
  - No `Assets/_Game/Shaders/` folder and no `Scripts/Rendering/` folder exist yet (clean slate). The
    project has no custom shaders. Rule: URP shaders only, no built-in pipeline shaders.
  - `Tests.EditMode.asmdef` references `Game`, `Game.Editor`, TMP. `InteractionSystemTests.cs` has
    16 tests that mirror private InteractionSystem logic in local helpers, including
    `ResolvePrompt` → "OnGUI prompt resolution". These must be updated when the prompt moves.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/World/InteractionSystem.cs` | Focus detection, current OnGUI prompt, name-range scan |
| `Assets/_Game/Scripts/World/IInteractable.cs` | Interactable contract |
| `Assets/_Game/Scripts/Inventory/ItemPickup.cs` | Item interactable (`NameTag`, `InteractPrompt`) |
| `Assets/_Game/Scripts/UI/EntityUI.cs` | World-space name / health bar |
| `Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs` | Interaction tuning values |
| `Assets/Settings/PC_Renderer.asset` | URP renderer — outline feature registration |
| `Assets/_Game/Scripts/UI/CLAUDE.md` | UI canvas / TMP rules |
| `_bmad-output/implementation-artifacts/tech-spec-name-range-interaction-prompt-polish.md` | Prior spec that introduced nameRange + OnGUI prompt |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs` | Channel base (`Raise`/`AddListener`/`RemoveListener`) |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_DoorOpenRequest.cs` | Payload-struct-with-runtime-ref pattern to copy |
| `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` | HUD script pattern (channel subscription, CanvasGroup fades, Awake validation) |
| `Assets/_Game/Scripts/UI/HUD/CLAUDE.md` | HUD script/channel conventions |
| `Assets/_Game/Prefabs/CLAUDE.md` | Player / UICanvas prefab structure |
| `Assets/_Game/Prefabs/UI/UICanvas.prefab` | HUD canvas (Crosshair) — prompt card host |
| `Assets/_Game/Game.asmdef` | Needs URP runtime references |
| `ProjectSettings/TagManager.asset` | `m_RenderingLayers` — add `Outline` |
| `Assets/Tests/EditMode/InteractionSystemTests.cs` | Existing tests to refactor onto extracted helpers |
| `_bmad-output/project-context.md` | Event-channel, logging, OnGUI, test rules |

### Technical Decisions

- **Outline technique:** screen-space, Rendering Layer mask + full-screen edge/dilate composite
  (chosen over inverted-hull and rim glow for quality and mesh independence; works on skinned meshes).
- **Highlight is opt-in** via `InteractionHighlight`; absence means prompt-only. Kept separate from
  `IInteractable` so the interface does not change.
- **Prompt card is uGUI + TMP**, anchored to the target in screen space — required by project rules
  and enables layout, fades, resolution scaling.
- **Entities keep name + health bar** in `nameRange`; only non-entity interactables lose their
  unfocused name tag (their name moves to the prompt card when focused). Investigation showed those
  interactables have no `EntityUI` today, so no name-tag code change is needed for them.
- **World → UI via a new channel** `GameEventSO_InteractionFocus` (`OnInteractionFocusChanged`).
  The payload struct holds the focused `Component` (runtime ref, or null for "no focus"), the verb
  and the name. `InteractionSystem` raises it when the target changes **or** when the target's
  prompt/name changes (e.g. a corpse becoming lootable). The prompt card never polls the system.
- **Outline color via a global shader property** (`Shader.SetGlobalColor`). Only one target is ever
  focused, so the per-component override + config default resolve to a single global color. Outline
  width (pixels) lives in the renderer feature settings.
- **Outline pass design:**
  1. Draw renderers whose `renderingLayerMask` has the `Outline` bit into a temporary R8 mask, using
     an override unlit material. Depth-test against camera depth so only the visible silhouette shows.
  2. Full-screen composite (`Blitter` / `Blit.hlsl` `Vert`) samples the mask in a ring of radius
     `width` and colors pixels where the dilated mask > 0 and the mask == 0. Blend this over the
     camera color at `BeforeRenderingPostProcessing`.
  Shaders are referenced through serialized `Shader` fields on the feature, so they ship in builds.
- **Outline toggle** = OR/AND-NOT the `Outline` rendering-layer bit on the target's renderers.
  Materials are never swapped. `InteractionHighlight` caches its renderers in `Awake`.
- **Pure logic is extracted** into a static `InteractionFocus` class (prompt formatting,
  change detection, layer-bit math) so EditMode tests call production code instead of mirrors.
- New code locations: `Assets/_Game/Scripts/Rendering/` (namespace `Game.Rendering`, in the `Game`
  assembly) and `Assets/_Game/Shaders/`.

## Implementation Plan

### Tasks

#### Phase A — Foundations

- [x] Task 1: Add the `Outline` rendering layer
  - File: `ProjectSettings/TagManager.asset`
  - Action: Add a rendering layer named `Outline` at index 1 (bit `1 << 1` = 2) through Project
    Settings → Tags and Layers → Rendering Layers. Verify `m_RenderingLayers` now lists
    `Default, Outline`.
  - Notes: Use the Editor UI, or MCP `execute_code` + `AssetDatabase.SaveAssets`. Do not raw-edit
    the YAML while the editor is open.

- [x] Task 2: Reference URP from the game assembly
  - File: `Assets/_Game/Game.asmdef`
  - Action: Append `"Unity.RenderPipelines.Universal.Runtime"` and
    `"Unity.RenderPipelines.Core.Runtime"` to `references`.
  - Notes: Also update the reference list in the "Assembly Setup" section of `Assets/_Game/CLAUDE.md`.

- [x] Task 3: Add the outline color property name constant
  - File: `Assets/_Game/Scripts/Core/GameConstants.cs`
  - Action: Add under a new `// --- Rendering ---` header:
    `public const string INTERACTION_OUTLINE_COLOR_PROPERTY = "_InteractionOutlineColor";`

- [x] Task 4: Extract pure focus logic into `InteractionFocus`
  - File: `Assets/_Game/Scripts/World/InteractionFocus.cs` (new, namespace `Game.World`, `public static class`)
  - Action: Implement:
    - `bool IsAlive(IInteractable i)` → `i is UnityEngine.Object o ? o != null : i != null`
      (Unity fake-null for destroyed components).
    - `bool HasFocusChanged(IInteractable prev, string prevVerb, string prevName, IInteractable next, string nextVerb, string nextName)`
      → `!ReferenceEquals(prev, next) || prevVerb != nextVerb || prevName != nextName`.
    - `string ResolveVerb(IInteractable i)` → `IsAlive(i) ? i.InteractPrompt ?? "" : ""`;
      `string ResolveName(IInteractable i)` → same with `NameTag`.
    - `uint WithLayer(uint mask, uint bits)` → `mask | bits`; `uint WithoutLayer(uint mask, uint bits)` → `mask & ~bits`.
    - `Color ResolveOutlineColor(bool hasOverride, Color overrideColor, Color defaultColor)`.
    - `Color SelectCrosshairColor(bool hasTarget, Color defaultColor, Color highlightColor)`.
  - Notes: No Unity lifecycle and no state, so it can be tested in EditMode.

- [x] Task 5: Add highlight settings to the interaction config
  - File: `Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs`
  - Action: Add
    ```csharp
    [Header("Highlight")]
    public Color outlineColor = new Color(1f, 0.85f, 0.45f, 1f);
    public RenderingLayerMask outlineRenderingLayer;
    ```
  - Then in `Assets/_Game/Data/Config/InteractionConfig.asset` set `outlineRenderingLayer` = `Outline`.

- [x] Task 6: Add the focus-changed event channel
  - File: `Assets/_Game/ScriptableObjects/Events/GameEventSO_InteractionFocus.cs` (new, namespace `Game.Core`, own file)
  - Action:
    ```csharp
    [System.Serializable]
    public struct InteractionFocusData
    {
        public Component target;  // runtime scene ref passed through Raise() — NOT stored in any SO asset; null = no focus
        public string verb;       // IInteractable.InteractPrompt
        public string name;       // IInteractable.NameTag
    }

    [CreateAssetMenu(menuName = "Game/Events/Interaction Focus", fileName = "NewInteractionFocusEvent")]
    public class GameEventSO_InteractionFocus : GameEventSO<InteractionFocusData> { }
    ```
  - Then create `Assets/_Game/Data/Events/OnInteractionFocusChanged.asset` from that menu.

#### Phase B — Outline rendering

- [x] Task 7: Mask shader
  - File: `Assets/_Game/Shaders/InteractionOutlineMask.shader` (new)
  - Action: `Shader "Hidden/Game/InteractionOutlineMask"`, `Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }`,
    one pass with `ZWrite Off`, `ZTest LEqual`, `Cull Back`. HLSL includes
    `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`. The vertex shader uses
    `TransformObjectToHClip(positionOS)` and the fragment returns `half4(1,1,1,1)`.

- [x] Task 8: Composite shader
  - File: `Assets/_Game/Shaders/InteractionOutlineComposite.shader` (new)
  - Action: `Shader "Hidden/Game/InteractionOutlineComposite"`, one pass with `ZWrite Off`,
    `ZTest Always`, `Cull Off`, `Blend SrcAlpha OneMinusSrcAlpha`. Includes Core.hlsl and
    `Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl`, and uses `#pragma vertex Vert`.
    The fragment:
    - `center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r`; if `center > 0.5` return `0` (the interior stays clean).
    - Sample 12 directions evenly spaced on a circle of radius `_OutlineWidth * _BlitTexture_TexelSize.xy`,
      plus the same 12 at half radius. Take the max as `edge`.
    - Return `half4(_InteractionOutlineColor.rgb, edge * _InteractionOutlineColor.a)`.
    - Declare `float _OutlineWidth;` and `half4 _InteractionOutlineColor;` (a global, set from C#).

- [x] Task 9: URP renderer feature
  - File: `Assets/_Game/Scripts/Rendering/InteractionOutlineFeature.cs` (new, namespace `Game.Rendering`)
  - Action: `public class InteractionOutlineFeature : ScriptableRendererFeature` with a nested
    `[Serializable] Settings`:
    - `RenderingLayerMask outlineLayer`
    - `Shader maskShader`, `Shader compositeShader` (serialized refs, so the shaders ship in builds)
    - `[Range(1f, 8f)] float widthPixels = 3f`
    - `RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing`
    - `Create()`: if either shader is null → `GameLog.Error` and leave the materials null.
      Otherwise `CoreUtils.CreateEngineMaterial` both, then create the pass.
    - `AddRenderPasses`: skip if the materials are null, if `cameraType` is `Preview` or `Reflection`,
      if `outlineLayer == 0`, or if `Application.isPlaying && !InteractionHighlight.AnyHighlighted`.
      Otherwise enqueue.
    - `Dispose(bool)`: `CoreUtils.Destroy` both materials.
  - The nested `InteractionOutlinePass : ScriptableRenderPass` implements `RecordRenderGraph`:
    1. Get `UniversalResourceData`, `UniversalCameraData`, `UniversalRenderingData`, `UniversalLightData`.
       If `resourceData.isActiveTargetBackBuffer` → return (no intermediate texture).
    2. Mask texture: copy `cameraData.cameraTargetDescriptor`, set `graphicsFormat = R8_UNorm`,
       `depthStencilFormat = None`, `msaaSamples = 1`. Create it with
       `UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_InteractionOutlineMask", true)`.
    3. Raster pass "InteractionOutline Mask": build a `RendererListDesc` with shader tags
       `UniversalForward`, `UniversalForwardOnly` and `SRPDefaultUnlit`, `renderQueueRange = RenderQueueRange.all`,
       `renderingLayerMask = outlineLayer`, `overrideMaterial = mask material`, `sortingCriteria = CommonOpaque`.
       Then `builder.UseRendererList`, `SetRenderAttachment(mask, 0)`,
       `SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read)`.
       The render function does `cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0)` followed by `cmd.DrawRendererList`.
    4. Raster pass "InteractionOutline Composite": `builder.UseTexture(mask)`,
       `SetRenderAttachment(resourceData.activeColorTexture, 0)`. The render function does
       `material.SetFloat("_OutlineWidth", widthPixels)` then
       `Blitter.BlitTexture(cmd, mask, new Vector4(1, 1, 0, 0), compositeMaterial, 0)`.
  - Notes: Add `private const string TAG = "[Outline]";`. RenderGraph only. Do not implement the legacy `Execute` path.

- [x] Task 10: Register the feature on the PC renderer
  - File: `Assets/Settings/PC_Renderer.asset`
  - Action: Add `InteractionOutlineFeature` after SSAO via the Renderer asset inspector, or via MCP
    `execute_code` (`ScriptableRendererData.rendererFeatures.Add` + `AssetDatabase.AddObjectToAsset` +
    `SetDirty` + `SaveAssets`). Set `outlineLayer = Outline`, assign both shaders, and set `widthPixels = 3`.
    Skip `Mobile_Renderer`: the target is PC only.

#### Phase C — Highlight component and InteractionSystem

- [x] Task 11: `InteractionHighlight` component
  - File: `Assets/_Game/Scripts/World/InteractionHighlight.cs` (new, namespace `Game.World`)
  - Action:
    - Serialized fields:
      - `[Tooltip("Meshes to outline. Empty = all MeshRenderer/SkinnedMeshRenderer children.")] [SerializeField] private Renderer[] _targets;`
      - `[SerializeField] private bool _overrideColor;`
      - `[SerializeField] private Color _color = Color.white;`
    - `public bool HasColorOverride => _overrideColor; public Color ColorOverride => _color;`
    - `public static bool AnyHighlighted => s_activeCount > 0;` backed by `private static int s_activeCount`.
      Reset it in `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`
      so it survives Enter Play Mode with domain reload disabled.
    - `Awake`: if `_targets` is null or empty, collect `GetComponentsInChildren<Renderer>(true)` and
      keep only `MeshRenderer` and `SkinnedMeshRenderer`. If nothing remains → `GameLog.Warn(TAG, ...)`.
    - `public void SetHighlighted(bool on, uint layerBits)`: return if the state is unchanged or
      `layerBits == 0`. For each non-null renderer, set `r.renderingLayerMask = on ? InteractionFocus.WithLayer(...) : InteractionFocus.WithoutLayer(...)`.
      Store `_appliedBits` and update `s_activeCount`.
    - `OnDisable`: if highlighted → `SetHighlighted(false, _appliedBits)` (destroyed or pooled targets
      never leave a stale bit or count).

- [x] Task 12: Wire focus, highlight and the event into `InteractionSystem`
  - File: `Assets/_Game/Scripts/World/InteractionSystem.cs`
  - Action:
    - Add `[SerializeField] private GameEventSO_InteractionFocus _onFocusChanged;`. In `Awake`, if it is null
      → `GameLog.Warn` and continue: the outline still works, there is just no prompt card.
    - Add the fields `_focusedHighlight`, `_focusedVerb`, `_focusedName`, `_outlineBits`, and
      `static readonly int OutlineColorId = Shader.PropertyToID(GameConstants.INTERACTION_OUTLINE_COLOR_PROPERTY)`.
    - `Awake`: `_outlineBits = _config.outlineRenderingLayer.value`. If it is 0 → `GameLog.Warn` ("outline disabled").
    - `Update`: after `best` is selected, compute `verb = InteractionFocus.ResolveVerb(best)` and
      `name = InteractionFocus.ResolveName(best)`. Replace the `if (best != _previousInteractable)`
      block with `if (InteractionFocus.HasFocusChanged(_previousInteractable, _focusedVerb, _focusedName, best, verb, name)) ApplyFocus(best, verb, name);`.
    - New `private void ApplyFocus(IInteractable next, string verb, string name)`:
      1. If `_focusedHighlight != null` → `SetHighlighted(false, _outlineBits)`.
      2. If the target changed, `_focusedHighlight = next != null ? ((Component)next).GetComponent<InteractionHighlight>() : null`.
         Only call `GetComponent` when the target changes, not when just the verb or name changes.
      3. If `_focusedHighlight != null` → `Shader.SetGlobalColor(OutlineColorId, InteractionFocus.ResolveOutlineColor(...config.outlineColor))`
         followed by `SetHighlighted(true, _outlineBits)`.
      4. Set `CurrentInteractable`, `_previousInteractable`, `_focusedVerb` and `_focusedName`, and set the
         crosshair color via `InteractionFocus.SelectCrosshairColor`.
      5. `_onFocusChanged?.Raise(new InteractionFocusData { target = next as Component, verb = verb, name = name })`.
    - `LateUpdate`: guard with `InteractionFocus.IsAlive(CurrentInteractable)`. After `Interact()`, set
      `_scanTimer = _config.scanInterval` to force a rescan next frame (this closes the double-pickup window).
    - `OnDisable`: as the **first** statement (before the `_input` guard) call `ClearFocus()`. It does
      `ApplyFocus(null, "", "")` when `CurrentInteractable != null` or `_focusedHighlight != null`, and is
      null-safe when `Awake` disabled the component (`_crosshairImage` may be null, so guard it).
    - **Delete** `OnGUI()` and `_promptStyle`.
  - Notes: The interaction gizmos and the name-range loop stay unchanged.

#### Phase D — Prompt card UI

- [x] Task 13: `InteractionPromptUI` script
  - File: `Assets/_Game/Scripts/UI/HUD/InteractionPromptUI.cs` (new, namespace `Game.UI`)
  - Action:
    - Serialized fields: `GameEventSO_InteractionFocus _onFocusChanged`, `RectTransform _card`,
      `CanvasGroup _canvasGroup`, `TMP_Text _verbText`, `TMP_Text _nameText`,
      `Vector2 _screenOffset = new Vector2(0f, 24f)`, `float _screenMargin = 16f`, `float _fadeSpeed = 10f`.
      Add `private const string TAG = "[InteractionPrompt]";`.
    - `Awake`: validate `_card`, `_canvasGroup`, `_verbText`, `_nameText` and `Camera.main` → `GameLog.Error` +
      `enabled = false`. Cache `_parentRect = (RectTransform)_card.parent` and set `_canvasGroup.alpha = 0`.
      Warn if `_onFocusChanged` is null.
    - `OnEnable`/`OnDisable`: `AddListener`/`RemoveListener` (null-guarded). `OnDisable` also sets alpha to 0.
    - `HandleFocusChanged(InteractionFocusData d)`: store `_target = d.target` and
      `_anchorCollider = d.target != null ? d.target.GetComponentInChildren<Collider>() : null`.
      Set the texts **only here**: `_verbText.text = d.verb`, `_nameText.text = d.name`, and
      `_nameText.gameObject.SetActive(!string.IsNullOrEmpty(d.name))`.
    - `LateUpdate`:
      - Compute the anchor as the top-center of `_anchorCollider.bounds` (`center + up * extents.y`), or
        `_target.transform.position` without a collider.
      - `screen = _camera.WorldToScreenPoint(anchor)`.
      - `visible = _target != null && CursorManager.IsLocked && screen.z > 0`.
      - If visible: `pos = ClampToScreen((Vector2)screen + _screenOffset, _card.rect.size, _card.pivot, new Vector2(Screen.width, Screen.height), _screenMargin)`.
        Convert it with `RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, pos, null, out local)`
        and assign `_card.anchoredPosition` only if it moved by more than 0.5 px.
      - `alpha = Mathf.MoveTowards(alpha, visible ? 1 : 0, _fadeSpeed * Time.unscaledDeltaTime)`, written only when it changes.
    - `public static Vector2 ClampToScreen(Vector2 pos, Vector2 size, Vector2 pivot, Vector2 screen, float margin)`:
      clamp so that `[pos - size*pivot, pos + size*(1-pivot)]` stays inside `[margin, screen - margin]` on each axis.
  - Notes: The `[E]` key label is authored text in the prefab (gamepad glyphs and rebinding display are
    out of scope). No `GraphicRaycaster`.

- [x] Task 14: Prompt card prefab, placed in the HUD
  - File: `Assets/_Game/Prefabs/UI/InteractionPrompt.prefab` (new), `Assets/_Game/Prefabs/UI/UICanvas.prefab`
  - Action:
    - Build this hierarchy:
      ```
      InteractionPrompt (RectTransform stretch-full; Canvas — nested, no overrideSorting; CanvasGroup; InteractionPromptUI)
      └── Card (RectTransform anchors center, pivot (0.5, 0); Image bg rgba(0,0,0,0.6) raycastTarget off;
               HorizontalLayoutGroup padding 10/14, spacing 10, childAlignment MiddleLeft; ContentSizeFitter preferred×preferred)
          ├── KeyBadge (Image rgba(1,1,1,0.15) 32×32 via LayoutElement; child TMP "E" bold 18, centered)
          └── Texts (VerticalLayoutGroup spacing 0, childAlignment MiddleLeft)
              ├── VerbText (TMP bold 20, white)
              └── NameText (TMP 17, color #E8D9A8)
      ```
    - Set `raycastTarget = false` on every Graphic.
    - Add the prefab as a child of `UICanvas` directly after `Crosshair` and before `Menus`, so menus
      draw over it.
    - Wire `_onFocusChanged` → `OnInteractionFocusChanged.asset` and wire the texts, card and canvas group.
  - Notes: If the canvas is created via MCP, apply the World Space quirk fix (`renderMode = 0`). A
    nested canvas inherits the root's render mode, but check it anyway.

- [x] Task 15: Wire the event on the Player
  - File: `Assets/_Game/Prefabs/Player/Player.prefab`
  - Action: On the root `InteractionSystem`, set `_onFocusChanged` → `OnInteractionFocusChanged.asset`.

#### Phase E — Content, tests, docs

- [x] Task 16: Add `InteractionHighlight` to item pickups
  - Files: `Assets/_Game/Prefabs/Items/Base Item.prefab`, `Armors/Armor.prefab`, `Armors/Helmet.prefab`,
    `TestItem_Health_Potion.prefab`, `TestItem_Mana_Potion.prefab`, `Tomes/Tome_PowerStrike.prefab`,
    `Weapons/OneHandedSword/big sword/big sword_World.prefab`, `Weapons/Swords/Axe/Axe_World.prefab`,
    `Weapons/Swords/SwordBase/SwordBase_World.prefab`
  - Action: Add `InteractionHighlight` to the root (the same GO as `ItemPickup`), with `_targets` empty and no override.
  - Notes: Use MCP `manage_prefabs` / `manage_components`. If you raw-edit the YAML, refresh with
    `if_dirty` only (root CLAUDE.md quirk).

- [x] Task 17: Update the EditMode tests
  - File: `Assets/Tests/EditMode/InteractionSystemTests.cs`
  - Action:
    - Delete the mirror helpers `ShouldHighlight`, `SelectCrosshairColor`, `ResolvePrompt` and `HasStateChanged`,
      and rewrite the existing tests to call `InteractionFocus.*` directly. Keep `IsPromptCandidate`
      (the gate is still inline in `Update`).
    - Update the class doc comment: remove the OnGUI reference.
    - Give `StubInteractable` a configurable `NameTag`.
    - Add these tests:
      - `FocusChange_SameTargetVerbChanged_Changed`, `FocusChange_SameTargetNameChanged_Changed`,
        `FocusChange_SameTargetSameText_NoChange`
      - `ResolveVerb_Null_ReturnsEmpty`, `ResolveName_ReturnsNameTag`, `ResolveVerb_NullPrompt_ReturnsEmpty`
      - `IsAlive_Null_False`, `IsAlive_PlainObject_True`
      - `WithLayer_SetsBit_PreservesOthers`, `WithoutLayer_ClearsBit_PreservesOthers`, `WithLayer_Idempotent`
      - `OutlineColor_NoOverride_UsesDefault`, `OutlineColor_Override_UsesOverride`
  - New file: `Assets/Tests/EditMode/InteractionPromptUITests.cs`, with
    `ClampToScreen_Inside_Unchanged`, `ClampToScreen_OffLeft_ClampedToMargin`,
    `ClampToScreen_OffRightTop_ClampedToMargin`, `ClampToScreen_PivotBottomCenter_UsesPivot`.

- [x] Task 18: Update the documentation
  - Files and changes:
    - `Assets/_Game/Scripts/World/CLAUDE.md`: add `InteractionFocus` and `InteractionHighlight` rows, plus a rule:
      "Outline is opt-in: add `InteractionHighlight` to an interactable root; list only the meshes to outline
      (e.g. a door handle); no component = prompt-only".
    - `Assets/_Game/Scripts/UI/HUD/CLAUDE.md`: an `InteractionPromptUI` row and the `OnInteractionFocusChanged` channel.
    - New `Assets/_Game/Scripts/Rendering/CLAUDE.md`: the feature, the rendering layer contract, the shaders,
      the `AnyHighlighted` early-out, and the RenderGraph-only rule.
    - Root `CLAUDE.md`: a folder index row for Rendering.
    - `Assets/_Game/CLAUDE.md`: the asmdef references.
    - `Assets/_Game/Prefabs/CLAUDE.md`: `InteractionPrompt` in the UICanvas tree, and the rule that world
      items carry `InteractionHighlight`.
    - `_bmad-output/project-context.md` (line ~251): replace the OnGUI interaction-prompt exception with
      "Interaction prompt is uGUI (`InteractionPromptUI`) driven by `OnInteractionFocusChanged`".

### Acceptance Criteria

- [x] AC 1: Given an item pickup with `InteractionHighlight` within `interactionRange`, when the player
  aims at it, then within one scan interval a colored outline of about 3 px appears around its visible
  silhouette, and the interior of the mesh is not tinted.
- [x] AC 2: Given a focused item, when the player aims away so no interactable is in range, then the
  outline disappears, and the item's renderers no longer have the `Outline` bit in `renderingLayerMask`.
- [x] AC 3: Given two items close together, when focus moves from item A to item B, then only B is
  outlined and the prompt card moves to B and shows B's name.
- [x] AC 4: Given a focused item, when the prompt card is shown, then it reads `[E]` + `Pick Up` + the
  item's `itemName`, sits above the item's collider top, and fades in.
- [ ] AC 5: Given the focused item is near a screen edge, when the card would overflow, then the card
  is clamped inside a 16 px margin and stays fully visible.
- [ ] AC 6: Given a focused interactable with **no** `InteractionHighlight` (e.g. a door), when it is
  focused, then no outline renders but the prompt card shows its verb and name, and no error or
  warning is logged.
- [ ] AC 7: Given an `InteractionHighlight` whose `_targets` lists only one child mesh, when it is
  focused, then only that mesh is outlined.
- [ ] AC 8: Given an `InteractionHighlight` with `_overrideColor` enabled, when it is focused, then the
  outline uses the override color. Without the override, it uses `InteractionConfig.outlineColor`.
- [x] AC 9: Given the player presses `[E]` on a focused item, when it is picked up and destroyed, then
  the outline and prompt card disappear on the next frame, and pressing `[E]` again within 0.2 s does
  not add a second copy to the inventory.
- [ ] AC 10: Given a focused interactable, when the inventory or any menu opens (cursor unlocked), then
  the prompt card hides, and it reappears when the menu closes while still aimed at the target.
- [ ] AC 11: Given a living monster or NPC within `nameRange`, when it is not focused, then its
  `EntityUI` name and health bar still show exactly as before.
- [ ] AC 12: Given a dead entity that becomes lootable while focused (its verb changes from "" to "Loot"),
  when the next scan runs, then the card updates to `[E] Loot` without the player re-aiming.
- [ ] AC 13: Given no interactable is highlighted in Play mode, when a frame renders, then the Frame
  Debugger shows no `InteractionOutline` passes (the `AnyHighlighted` early-out).
- [x] AC 14: Given `InteractionSystem` is disabled or the Player is destroyed while an item is focused,
  then the item's `Outline` bit is cleared and `AnyHighlighted` is false.
- [ ] AC 15: Given the feature has a missing shader reference, when the renderer initializes, then
  `GameLog.Error` is logged once and rendering continues without the outline (no exceptions).
- [x] AC 16: Given the project, when searching `Assets/_Game/Scripts/World/InteractionSystem.cs`, then
  `OnGUI` no longer exists, and all EditMode tests pass.
- [ ] AC 17: Given a Windows x64 development build, when it runs, then the outline renders (shaders are
  not stripped) and the prompt card works.

## Additional Context

### Dependencies

- URP 17.6 RenderGraph API (`com.unity.render-pipelines.universal` 17.6.0, already installed).
- TextMeshPro (already referenced by `Game.asmdef`).
- No new packages. No dependency on other in-progress specs.
- The `Outline` rendering layer (Task 1) must exist before the config asset (Task 5) and the feature
  settings (Task 10) can select it.

### Testing Strategy

- **EditMode (automated):** `InteractionSystemTests` (refactored, plus about 13 new tests on
  `InteractionFocus`) and `InteractionPromptUITests` (4 tests on `ClampToScreen`). Run them via MCP
  `run_tests` (EditMode). Rendering and lifecycle are not unit-tested, per the project testing rules.
- **Manual (Play mode, TestScene / StartingTown):**
  1. Drop 4–5 item pickups close together and sweep the crosshair across them (AC 1–4).
  2. Walk to a screen edge with an item focused (AC 5).
  3. Focus a door and a container (AC 6). Temporarily add `InteractionHighlight` listing only a door
     handle to check AC 7, then revert it.
  4. Double-tap `[E]` on an item and check the inventory count (AC 9).
  5. Open the inventory while focused (AC 10).
  6. Approach a spider (AC 11), kill it while aiming at it (AC 12).
  7. Open the Frame Debugger with nothing focused, then with something focused (AC 13).
  8. Disable `InteractionSystem` in the inspector while focused (AC 14).
- **Build check:** a development build launches with a visible outline (AC 17).

### Notes

- **High risk: RenderGraph depth attachment.** Binding `activeDepthTexture` as a read-only depth
  attachment next to a separately created R8 color target requires matching dimensions. MSAA is off
  today. If MSAA is enabled later, the mask's `msaaSamples` must match the depth's. If the binding
  fails in Forward+ (validation error in the console), the fallback is a mask pass with `ZTest Always`,
  which outlines through occluders. Note that in the spec if you use it.
- **Risk: `SRPDefaultUnlit` / shader tag coverage.** Renderers whose materials use only custom
  LightMode tags would not appear in the mask. The imported asset-pack materials (Multistory Dungeons)
  are URP Lit, so this should be fine. Verify on the potion prefabs.
- **Known limitations:**
  - The outline width is in screen pixels, so it gets relatively thinner at 4K.
  - Transparent materials are outlined by their mesh shape.
  - The prompt card does not scale with resolution (UICanvas has no CanvasScaler).
- **Future (out of scope):**
  - Rarity/category colors through the existing `_overrideColor`, or a category→color table in the config.
  - Authoring highlights for doors, chests and NPCs (handle or lock meshes only).
  - Gamepad glyphs via `GetBindingDisplayString`.
  - A Witcher-sense "reveal all loot" mode that could reuse the same rendering layer.
  - Keyboard key text read from the binding.

## Implementation Notes

Deviations from the plan, all deliberate:

- **Renderer list:** built with `RenderingUtils.CreateDrawingSettings` + `FilteringSettings { renderingLayerMask }`
  + `RendererListParams` → `renderGraph.CreateRendererList` (the URP 17.6 idiom used by its own passes), not
  `RendererListDesc`. Same filtering and override-material behavior.
- **Mask texel size:** the composite reads `_OutlineMaskTexelSize`, which C# sets from the mask descriptor. It does not
  rely on `_BlitTexture_TexelSize`, which `Blitter` does not reliably populate.
- **Intermediate texture:** the pass sets `requiresIntermediateTexture = true`, so `PC_Renderer`'s
  `IntermediateTextureMode: Auto` never renders straight to the backbuffer.
- **Prompt placement:** `UICanvas` has no root-level `Crosshair`. It lives under `UICanvas/Game`, and `Game` is
  drawn **after** `Menus`. `InteractionPrompt` sits under `Game` right after `Crosshair`. Menus therefore don't draw
  over it, but the card hides whenever the cursor is unlocked (any menu open), so it never overlaps a menu.
  `Prefabs/CLAUDE.md` tree corrected.
- **`PC_Renderer.asset` reserialized:** saving it ran URP's asset upgrade (`m_AssetVersion` 2 → 3). The SSAO
  feature's shader/blue-noise refs left the YAML because URP 17.6 loads them from pipeline resources. SSAO is still active.
- `Tome_PowerStrike.prefab` lost two stale `PersistentID` fields (`_guid`, `_onEntityKilled`) on reserialize. Those
  fields no longer exist in code.
- **Verification (editor unfocused, so the play loop was throttled):** `InteractionSystem.Update` was driven via
  reflection and `Camera.main` rendered to a RenderTexture. Confirmed: a 3 px outline ring with a clean interior
  (AC 1), focus A→B moves the bit (AC 3), aiming away clears the bit and `AnyHighlighted` (AC 2), disabling the
  system clears it (AC 14), destroying the target drops focus on the next scan and nulls the prompt target (AC 9),
  and the prompt text reads `Pick Up` / `Health Potion`. After the review fixes, the card's bottom-center sits exactly at the anchor + 24 px offset (AC 4). Not covered by a hands-on play pass: the remaining
  ACs (5, 6, 7, 8, 10, 11, 12, 13, 15) and the build check (17). See the Testing Strategy manual list.

## Review Notes

- Adversarial review completed (a separate reviewer that saw only the diff).
- Findings: 16 total, 9 fixed, 7 skipped.
- Resolution approach: auto-fix (findings classified as real).
- Fixed:
  - **F1 (Critical):** `ScreenPointToLocalPointInRectangle` returns a point relative to the parent pivot, which is (0,0)
    on the nested instance. The point is now converted into the card's anchor space before being written to
    `anchoredPosition`.
  - **F2:** while the cursor is unlocked, `InteractionSystem` treats the target as unfocused. The outline and
    crosshair tint now hide along with the card.
  - **F4 / F5:** the pass is always enqueued and returns early in `RecordRenderGraph`. The camera no longer
    switches between backbuffer and intermediate rendering on focus changes, and edit mode pays nothing.
  - **F7:** the "in-place verb change" comment and test now describe the reachable case, a door's lock prompt
    changing. A corpse with `!CanInteract` is never focused.
  - **F8:** `LayoutRebuilder.ForceRebuildLayoutImmediate(_card)` runs in the focus handler, so the clamp uses the
    new card width on the same frame.
  - **F10:** an `InteractionHighlight` with no renderers no longer counts toward `AnyHighlighted`.
  - **F13:** new test `IsAlive_DestroyedComponent_False`, using a destroyed `ItemPickup`.
  - **F14:** removed the second mask clear.
- Skipped:
  - **F3:** the outline layer is set in two places. The contract is documented in `Scripts/Rendering/CLAUDE.md`.
  - **F6:** MSAA. It is off today and documented as a risk.
  - **F9:** the card anchors to the first child collider.
  - **F11:** the trailing `l` on `m_RendererFeatureMap` is Unity's 64-bit compact-array suffix. Verified to parse.
  - **F12:** thin-silhouette sampling gaps come from the chosen technique.
  - **F15:** the outline is drawn before post-processing, as the spec chose.
  - **F16:** the shader property strings are private to the feature.
- EditMode: 464/464 passed after the fixes.

