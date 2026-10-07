# CLAUDE.md — Assets/_Game/Scripts/Rendering

> Loaded when Claude accesses files in this folder. Custom URP renderer features.
> Namespace: `Game.Rendering` (compiles into the `Game` assembly, which references
> `Unity.RenderPipelines.Universal.Runtime` + `Unity.RenderPipelines.Core.Runtime`).

---

## What's here

| File | Role |
|------|------|
| `InteractionOutlineFeature` | `ScriptableRendererFeature` registered on `Assets/Settings/PC_Renderer.asset` (after SSAO). Draws a constant-width screen-space outline around renderers on the `Outline` rendering layer — i.e. the focused interactable (`Game.World.InteractionHighlight`). |

Shaders live in `Assets/_Game/Shaders/`:

| Shader | Role |
|--------|------|
| `Hidden/Game/InteractionOutlineMask` | Override material for the mask pass — writes 1 into an R8 mask. `ZWrite Off`, `ZTest LEqual` against the camera depth, so only the visible silhouette counts (no outline through walls). |
| `Hidden/Game/InteractionOutlineComposite` | Full-screen blit (`Blit.hlsl` `Vert`). Samples the mask on 12 directions at `_OutlineWidth` and half of it; colors pixels outside the mask, alpha-blended over camera color. Interior stays clean. Color = global `_InteractionOutlineColor` (`GameConstants.INTERACTION_OUTLINE_COLOR_PROPERTY`, set by `InteractionSystem`). Texel size comes from `_OutlineMaskTexelSize` set in C#. |

---

## Rules

- **RenderGraph only** (`RecordRenderGraph`). Do not implement the legacy `Execute` path — URP 17.6 runs RenderGraph.
- **Rendering-layer contract:** the feature's `outlineLayer` and `InteractionConfig.outlineRenderingLayer` must both be `Outline` (TagManager rendering layer index 1, bit `2`). If they diverge, highlighted objects get the bit but nothing draws.
- **Shaders are serialized refs** on the feature (`maskShader`, `compositeShader`) so they ship in builds — never `Shader.Find`. A missing ref logs `GameLog.Error` once in `Create()` and the feature does nothing.
- **Early-out:** the pass is always enqueued (so `requiresIntermediateTexture` doesn't flip camera targets on every focus change), but `RecordRenderGraph` adds no passes unless `InteractionHighlight.AnyHighlighted` — the Frame Debugger shows no `InteractionOutline` passes when nothing is focused, in Play or Edit mode. Preview and Reflection cameras are always skipped.
- The mask shares the camera depth attachment: dimensions and MSAA must match. MSAA is off on `PC_RPAsset`; if it is enabled later, set the mask's `msaaSamples` to match the depth.
- Only renderers whose materials expose `UniversalForward`, `UniversalForwardOnly` or `SRPDefaultUnlit` passes appear in the mask (URP Lit/Unlit are fine).
- Width is in screen pixels (`widthPixels`, default 3) — it gets relatively thinner at 4K. Transparent materials outline their full mesh shape.

---

## Verifying in the editor (MCP)

With the editor unfocused, Play mode barely advances frames. To test without focus: drive
`InteractionSystem.Update` via reflection (set `_scanTimer` high first), then render
`Camera.main` into a `RenderTexture` with `cam.Render()` and compare a highlighted vs
non-highlighted frame.
