# CG Combat Framework Changelog

## [Unreleased]

### Added
- Combat Studio window (`Tools/CG Combat Framework/Combat Studio`) for Firables, Status Effects and Damage Pipelines: asset list, outline tree, detail panel, templates, and a fix-up banner for missing block types. Double-clicking one of these assets opens it in the Studio
- `[SelectType]` fields now draw with category colors, a searchable type picker, one-line summaries, and a menu for copy, paste, duplicate, move, reset and edit script
- Compact `ScalableValue` drawer (base value with a scalers toggle)
- `BlockSummary.Register(Type, string)` to give custom building blocks their own one-line summary
- `[BlockInfo(description, Example = ...)]` attribute to explain a building block. Every built-in block now has one, shown in the Combat Studio and as inspector tooltips
- Combat Studio: category icons, asset sprites in the list, branch guides and selected-path highlight in the outline, drag-to-reorder for block lists, cozy/compact list view with Display Name / ID / File Name labels, search by block contents, Problems Only and Uses filters, outline filter, empty-state create prompt, and keyboard shortcuts (Delete, Ctrl+C/V/D, Alt+Up/Down)
- Combat Studio: drag to reorder in the outline and the recipe as well as block lists, an outline View menu (branch guides, icons, field labels, summaries, compact rows, about box, examples, recipe, drag handles, drag to reorder), and a Test button on damage pipelines
- Unit Creator window (`Tools/CG Combat Framework/Unit Creator`): a step by step wizard that builds a unit prefab from an archetype or an existing unit, creating its stat profile, damage pipeline and any missing stat, resource and slot types, plus an overview of every unit prefab with editable cards, part toggles, hitboxes, problem checks and fixes
- Pipeline Tester window (`Tools/CG Combat Framework/Pipeline Tester`): runs a hit between two sandbox units and traces each damage step, with seeded or forced rolls, step toggles, drag to reorder (and apply the order to the asset), a distribution over many hits, a health preview, and an order explorer that tries every order of the steps
- Database window (`Tools/CG Combat Framework/Database`): every definition, pipeline and unit prefab by category in a grid or sortable list, with a Name / ID / File toggle, tile sizes, color swatches, duplicate ID and missing icon filters, a details panel, and create, duplicate and copy ID actions
- Inline inspector rows for stat entries and loadout entries

### Fixed
- `EmitAtPayload` with Emit From Target started at the target's forward vector instead of its position
- Combat Studio no longer collapses the outline when editing a plain array such as Categories or Granted Tags

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
