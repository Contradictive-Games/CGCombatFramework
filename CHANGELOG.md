# CG Combat Framework Changelog

## [Unreleased]

### Added
- Editor windows in `Tools/CG Combat Framework`:
    - Combat Studio: edit firables, status effects and damage pipelines as an outline, with templates, search and drag to reorder
    - Unit Creator: build unit prefabs from an archetype or an existing unit, and edit every unit from one overview
    - Pipeline Tester: trace a hit through each step of a damage pipeline
    - Database: browse every definition, pipeline and unit prefab by category
    - Combat Doctor: find setup problems across all combat assets and units, with fixes
    - Usage Finder: see where an asset or block type is used, and find unused definitions
    - Firable Lab: simulate a firable's DPS, time to kill and reach, or compare every firable
    - Effect Timeline: play a status effect out tick by tick on a sandbox unit
    - Combat Monitor: watch live units and the combat log (including regen) in play mode, and run debug actions on one or more units
    - Faction Matrix: edit every faction relation in one grid
    - Tag Tree: view and re-parent the tag hierarchy
    - Range Gizmos: draw firable ranges and projectile paths in the Scene view, with per-unit Hide and Solo
- Enhanced inspectors for Unit and its components, with problem checks, quick fixes and live state in play mode (turn off in `Tools/CG Combat Framework/Enhanced Inspectors`)
- `[SelectType]` fields draw with category colors, a searchable type picker, one-line summaries and a copy/paste menu
- `[BlockInfo]` attribute to describe a building block (every built-in block has one), and `BlockSummary.Register` for custom summaries
- Compact `ScalableValue` drawer and inline rows for stat and loadout entries
- Type colors for damage types, resource types and factions in block summaries
- Delete assets from the Studio, Database and Unit Creator (they go to the system trash)
- `Assets/CG Combat Framework/Find Usages` in the Project window's right click menu

### Fixed
- `EmitAtPayload` with Emit From Target started at the target's forward vector instead of its position
- Arrays of building blocks no longer get cut off at 520px in the inspector
- Expand arrows on blocks in an array no longer overlap the list's drag handle

## [v0.2.0] - 2026-10-25

### Changed
- **Breaking** Removed `ChancePayload` (use `ConditionalPayload` ("If ... Else in menu) with `ChanceCondition`)
- **Breaking** Removed `invert` field on `HasTagCondition` (use `NotCondition` wrapper)
- **Breaking** Removed `ApplyToSourcePayload` and `ApplyToOwnerPayload`(payloads now have `Subject` field)
- **Breaking** Combined `EmitAtTarget` and `EmitFromSource` into a single `EmitAt`
- **Breaking** Combined `ApplyPayloadsBehavior`, `TickPayloadsBehavior`, and `RemovePayloadsBehavior` into single `PayloadsBehavior`
- **Breaking** Emissions all now use an array of emissions, removed `MultiEmission` (references must be re-assigned)
- **Breaking** Remove `count` field from `ProjectileEmission` (use `RepeatEmission` + `ProjectileEmission`)
- **Breaking** Combined `SelfQuery`, `OwnerQuery`, `DirectTargetQuery` into `UnitQuery`
- Renamed `EmitFromEmission` menu option to `Flow/Move Origin`
- Renamed `PayloadsEmission` menu option to `Payloads At Point`
- Added `DisplayName` field to `GameDefinition`, remove inherited classes' `Name` & `DisplayName` fields
- `IUnitComponent` now has default no-op methods (except for `Initialize`)

### Added
- Most payloads now have a `Subject` field, and added an `Owner` option
- Added a `Emissions.EmitAll(IEmission[], in DeliveryContext)` function
- Make names for `Subject` fields more consistent, add `FormerlySerializedAs` to prevent breaking changes
- Add `PointAt` function to DeliveryContext

### Fixed


## [v0.1.2] - 2026-10-24

### Added
- Reload on start/complete events on AmmoGate
- Raycast queries now check is unit is dead, add `bool ignoreDead`
- Add duplicate ID checker tool for GameDefinitions on the toolbar `Tools/CG Combat Framework/Validate Definition IDs`

### Fixed
- Package Manager now shows the changelog link (`changeLogUrl` → `changelogUrl` in `package.json`), and repository links point at the renamed `CGCombatFramework` repo
- `ChargesGate` can now `AddCharge` successfully
- Builds no longer broken from incorrect namespaces
- `ChainQuery` no longer infinitely jumps
- Fix some emissions and damage steps not being serialized
- Tickables that are pending removed are no longer ticked
- `CombatScheduler` now cleared when `EndSession` is called on `CombatServices`


## [v0.1.1] - 2026-10-24

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

[Unreleased]: https://github.com/Contradictive-Games/CGCombatFramework/compare/v0.2.0...HEAD
[v0.2.0]: https://github.com/Contradictive-Games/CGCombatFramework/compare/v0.1.2...v0.2.0
[v0.1.2]: https://github.com/Contradictive-Games/CGCombatFramework/compare/v0.1.1...v0.1.2
[v0.1.1]: https://github.com/Contradictive-Games/CGCombatFramework/compare/v0.1.0...v0.1.1
[v0.1.0]: https://github.com/Contradictive-Games/CGCombatFramework/releases/tag/v0.1.0
