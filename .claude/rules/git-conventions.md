# Git Conventions for test-game

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
| `docs`     | Documentation only changes                                       |
| `style`    | Formatting, missing semicolons, etc. — no logic change           |
| `refactor` | Code change that neither fixes a bug nor adds a feature          |
| `perf`     | A code change that improves performance                          |
| `test`     | Adding or correcting tests                                       |
| `chore`    | Build process, tooling, dependency updates, config changes       |
| `ci`       | Changes to CI/CD configuration files and scripts                 |
| `revert`   | Reverts a previous commit                                        |

### Scopes (project-specific)

Use scopes to narrow the area of the commit. Common scopes for this project:

- `game` — core game logic (`src/game/`)
- `screens` — screen/UI management (`src/screens/`)
- `data` — game data, configs (`src/data/`)
- `main` — entry point (`src/main.js`)
- `styles` — CSS/styling (`styles/`)
- `build` — build config (vite, package.json)
- `assets` — static assets
- `docs` — documentation files
- `claude` — Claude commands and configuration

### Rules

1. **Subject line**: imperative, present tense, lowercase, no trailing period, max 72 chars
2. **Body**: wrap at 100 chars, explain *what* and *why*, not *how*
3. **Breaking changes**: add `!` after the type/scope, e.g. `feat(game)!:`, and a `BREAKING CHANGE:` footer
4. **Multiple changes**: if changes span multiple types, split into separate commits when practical. If not, use the most prominent type.

### Examples

```
feat(game): add enemy respawn mechanic

fix(screens): resolve blank screen on mobile resize

chore(build): upgrade vite to v6

docs(claude): add git conventions reference

style(main): reformat imports

refactor(data): extract level config into separate files

feat(game)!: redesign collision system

BREAKING CHANGE: collision API no longer supports legacy hitbox format
```

### Branch naming (optional reference)

```
feat/<short-description>
fix/<short-description>
chore/<short-description>
```

### Co-author footer

When Claude generates the commit, append:

```
Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
```
