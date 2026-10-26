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
- Type colors: names of types that have a color (damage types, resource types, factions) show in that color in block summaries across the inspector, Combat Studio and Pipeline Tester. Toggle with Type Colors in the Studio's outline View menu
- Editor windows update live when another asset changes. Editing a type's color or name refreshes the Database tiles and details, Studio summaries, inspector summaries, Unit Creator and Pipeline Tester right away
- `Assets/CG Combat Framework/Find Usages` in the Project window's right click menu
- New editor tools in `Tools/CG Combat Framework` (code in `Editor/Tools`):
    - Combat Doctor: scans every combat asset and unit for problems (missing block types, empty slots, unset references, duplicate or empty IDs, tag parent loops, faction conflicts, firable and effect setup, stat profiles, unused resistances, unit checks), with jump-to and fixes. Each rule can be turned off
    - Usage Finder: where an asset is used, down to the block and field, including mentions of its ID in code and text files; every place a building block type is used; and definitions nothing points at
    - Firable Lab: fires a firable with its real fire mode and gates against a sandbox defender and shows shots per second, damage per shot, DPS, burst DPS, magazine, reload, time to kill and reach, with a damage chart, per-hit breakdown and applied effects. Compare runs every firable side by side in a sortable table
    - Effect Timeline: applies a status effect to a sandbox host one or more times and plays it out tick by tick with its real behaviors and payloads, showing stacks, health, damage, heals, stat changes and an event log. Optional hits between host and source test trigger behaviors
    - Combat Monitor: a play mode window listing live units with health, resources, status effects, tags, stats and loadout, a filterable combat log, damage per second, and debug actions (kill, heal, revive, deal damage, apply effects, add or remove tags, pause combat)
    - Faction Matrix: every faction against every other in one grid. Click a cell to change the relation, with unit counts and one-sided relations shown
    - Tag Tree: the tag hierarchy as a tree. Drag a tag onto another to change its parent (loops are blocked), create child tags, rename, and count usages
    - Range Gizmos: draws the reach of the selected units' firables in the Scene view, in edit and play mode. Shows raycast lines with radius and spread, projectile flight paths with gravity and what each shot would hit first, sphere, cone, box, chain and everyone queries, and follow-up emissions (radial, move origin, weighted random, on hit and on stop). Aim from the unit's aim provider or the mouse, and toggle labels, spread, paths, hit tests and fill from the Combat Ranges overlay. One on/off is shared by the menu, the overlay (hiding it turns ranges off) and the unit inspectors. Hide or Solo firables per unit from the Firable Loadout inspector, or Solo one firable for every unit (even one it doesn't equip) from the overlay or the Studio
- Enhanced inspectors for Unit, Stats, Resources, Health, Status Effects, Firable Loadout, Unit View Binder and Hitbox. Each has a short description of the part. The Unit inspector adds a summary card with problems and fixes and the unit's parts, with one click to add a missing part. Stats and Resources edit the stat profile inline and show how many units share it. Health shows its max and damage pipeline with Edit and Test buttons, and Firable Loadout lists each firable with Studio, Lab and Range buttons. In play mode each inspector has a live section (health and resource bars, stats, status effects, firable state, kill, heal and revive). Turn them off with `Tools/CG Combat Framework/Enhanced Inspectors`
- Combat Monitor: select several units (Ctrl or Shift click, or the Select menu for all, alive, dead or by faction) and deal damage, heal, apply status effects, add or remove tags, kill or revive them all at once, with a group summary of health and damage per second. Selected Units Only in the log follows the whole group
- Combat Monitor: the log keeps recording while the window is closed (Record While Closed in the View menu), stays readable after leaving play mode, and has a History Size option. Regen and decay show when they start and stop and why (reached full, ran out, took damage), with each tick as an optional log filter
- Combat Studio firable cards have a Solo Range button, and Solo Range in Scene is in the right click menu for firables in the Studio and Database
- Delete assets from the Combat Studio, Database and Unit Creator (right click, a Delete button on the asset's card, or the Delete key). The confirm lists what uses the asset, and deleted assets go to the system trash
- Combat Studio cards for firables and status effects have Firable Lab and Effect Timeline buttons. Opening either tool from a menu picks up the firable or status effect you last selected in the Studio, the Database or the Project window, or the one the tool had last time
- Firable Lab, Effect Timeline and Pipeline Tester show a bar above the results with what was last run and when, a flash when it updates, a Live/Paused toggle, and the run and new seed buttons
- Editor performance: finding unit prefabs no longer loads every prefab in the project, unit problem counts are cached, windows ignore changes to non-combat assets, and Database rescans are batched

### Fixed
- `EmitAtPayload` with Emit From Target started at the target's forward vector instead of its position
- Combat Studio no longer collapses the outline when editing a plain array such as Categories or Granted Tags
- Inspector: arrays of building blocks no longer stop growing at 520px (Unity's default cap for array fields) and cut off the blocks and expand arrows below
- Inspector: the expand arrow on blocks inside an array no longer hangs 12px left over the list's drag handle
- Effect Timeline: the host's health no longer regenerates after it dies

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
