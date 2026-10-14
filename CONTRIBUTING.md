# Contributing to CG Combat Framework

## Setting up
1. Clone the repo into a Unity 6000.0+ project's `Packages/` (or `Assets/`) folder.
2. Run `git config core.hooksPath .githooks` once.
3. The `dev/` folder holds the local demo scene and materials. It's git-ignored and isn't shipped.

## Making changes
- Branch from `main`; open a PR back into `main`.
- Add a bullet under `## [Unreleased]` in `CHANGELOG.md`.
- Commit `.meta` files for every new file or folder.
- Code style is enforced by `.editorconfig`.

## Tests
Run all suites from **Window → General → Test Runner** before opening a PR:

| Suite | Assembly | When to run |
|---|---|---|
| EditMode | `CG.CombatFramework.Tests` | Always |
| PlayMode | `CG.CombatFramework.Tests.PlayMode` | Always |
| Performance | `CG.CombatFramework.Tests.Performance` | When touching per-frame code (ticking, damage, effects, projectiles) |

New built-in building blocks should come with EditMode tests. Anything that runs every frame
should be covered by an allocation test.

## Releasing (maintainers)
1. `bash .github/scripts/release.sh X.Y.Z`
2. `git push --follow-tags`
