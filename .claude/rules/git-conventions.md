# Git Conventions for Unity-BMAD-test-rpg

## Conventional Commits

All commits must follow the [Conventional Commits](https://www.conventionalcommits.org/) specification.

### Format

```
<type>(<scope>): <short description>

[optional body]

[optional footer(s)]
```

### Types

| Type       | When to use                                                      |
|------------|------------------------------------------------------------------|
| `feat`     | A new feature                                                    |
| `fix`      | A bug fix                                                        |
| `docs`     | Documentation only changes (CLAUDE.md, specs, BMAD artifacts)    |
| `style`    | Formatting only — no logic change                                |
| `refactor` | Code change that neither fixes a bug nor adds a feature          |
| `perf`     | A code change that improves performance                          |
| `test`     | Adding or correcting tests (`Tests.EditMode` / `Tests.PlayMode`) |
| `chore`    | Unity/package upgrades, project settings, tooling, config        |
| `revert`   | Reverts a previous commit                                        |

### Scopes (project-specific)

Pick the system the change belongs to. Game code lives under `Assets/_Game/`.

| Scope        | Area |
|--------------|------|
| `player`     | `Scripts/Player/` — movement, camera, state manager, player animation |
| `progression`| `Scripts/Player/Progression/` — XP, levels, learning points, skills |
| `combat`     | `Scripts/Combat/` — attacks, blocking, weapons, hitboxes, stamina |
| `ai`         | `Scripts/AI/`, `Scripts/Core/Animations/` — entity brains, health, factions, AI animation |
| `inventory`  | `Scripts/Inventory/` — items, equipment, action bar, pickups, trading |
| `world`      | `Scripts/World/` — interaction, containers, doors/locks, `PersistentID` |
| `dialogue`   | Dialogue graph, dialogue nodes, NPC dialogue data |
| `quest`      | `Scripts/Quest/`, `Data/Quests/` — quest SOs, steps, quest events |
| `ui`         | `Scripts/UI/`, `Prefabs/UI/` — HUD, screens, menus, toasts |
| `core`       | `Scripts/Core/` — `WorldStateManager`, facts, `GameLog`, `CursorManager`, scene loading |
| `npc`        | NPC data folders, NPC prefabs, memories |
| `scenes`     | Scene layout, terrain, lighting, navmesh |
| `assets`     | Art, animations, models, materials, imported packs |
| `editor`     | `Scripts/Editor/`, dev tools (`Game.DevTools`) |
| `build`      | Unity version, packages, project settings |
| `bmad`       | `_bmad-output/` — sprint status, stories, tech specs, planning docs |
| `claude`     | CLAUDE.md files, `.claude/` commands, skills, rules |

If a change spans several systems, use the dominant one, or omit the scope for broad changes.

### Rules

1. **Subject line**: imperative, present tense, lowercase, no trailing period, max 72 chars
2. **Body**: wrap at 100 chars, explain *what* and *why*, not *how*
3. **Breaking changes**: add `!` after the type/scope, e.g. `refactor(ai)!:`, and a `BREAKING CHANGE:` footer
   (use for renames that break prefab/scene script references or serialized fields)
4. **Multiple changes**: if changes span multiple types, split into separate commits when practical. If not, use the most prominent type.
5. **Unity assets**: commit `.meta` files together with their asset — never one without the other.

### Examples

```
feat(combat): add perfect-block parry window

fix(ai): skip warning-range validation for passive entities

feat(world): add lockable doors with skill-gated unlock

refactor(ai): split EntityAnimationBridge into driver and bridge

chore(build): upgrade Unity 6.3 to 6.6

docs(bmad): reconcile sprint status with shipped tech specs

docs(claude): add Entity prefab layer rules
```

### Branch naming (optional reference)

```
feat/<short-description>
fix/<short-description>
chore/<short-description>
```

### Co-author footer

When Claude generates the commit, append a co-author footer naming the model that actually
generated it (e.g. `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`).
