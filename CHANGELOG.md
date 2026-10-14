# CG Combat Framework Changelog

## [Unreleased]

### Fixed
- Package Manager now shows the changelog link (`changeLogUrl` → `changelogUrl` in `package.json`), and repository links point at the renamed `CGCombatFramework` repo

## [v0.1.1] - 2026-10-14

### Changed
- **Breaking:** Built-in unit components (`HealthComponent`, `StatsComponent`, `StatusEffectsComponent`, `FirableLoadout`, `UnitViewBinder`) now implement the `IUnitComponent` lifecycle methods explicitly. Call `Initialize`, `Bind`, `Unbind`, `OnSpawned` and `OnDespawned` through `IUnitComponent` (normally `Unit` does this for you)
- **Breaking:** Internal bookkeeping is no longer public: `EventBus.Clear()`, `TagContainer.Clear()`, `CombatScheduler.Count`, and `UnitRegistry.AssignId` / `Add` / `Remove`
- **Breaking:** Removed `Unit.SetOwner()`
- Built-in components now use `[DisallowMultipleComponent]`

### Fixed
- `RigidbodyKnockback` and `UnitViewBinder` couldn't be added to GameObjects because their class names didn't match their file names
- `HealthComponent` now resets its death and regen state when a pooled unit respawns
- Removed a stray editor-only `using` in `UnitWatcher` that broke player builds

## [v0.1.0] - 2026-10-12

### Added
- Units:
    - capabilities
    - per-life IDs and handles
    - pooled spawning
    - registry
- Stats and resources:
    - modifiers
    - regen rules
    - max from stat
- Damage pipeline (including 10 built-in steps)
- Status effects:
    - stacking modes
    - pooled instances
    - behaviors
    - triggers
    - while conditions
- Firables:
    - 5 built-in fire modes
    - 6 built-in fire gates
    - deliveries
    - projectiles (arc, homing, ricochet)
- Building blocks:
    - payloads
    - queries
    - emissions
    - conditions
    - value scalers
- Presentation helpers:
    - IUnitView
    - UnitViewBinder
    - UnitWatcher
- Tests:
    - EditMode
    - PlayMode
    - Performance

[Unreleased]: https://github.com/Contradictive-Games/CGCombatFramework/compare/v0.1.1...HEAD
[v0.1.1]: https://github.com/Contradictive-Games/CGCombatFramework/compare/v0.1.0...v0.1.1
[v0.1.0]: https://github.com/Contradictive-Games/CGCombatFramework/releases/tag/v0.1.0
