# CG Combat Framework Changelog

## [Unreleased]

### Added
- Editor windows in `Tools/CG Combat Framework`:
    - Combat Studio: edit units, firables, status effects and damage pipelines, search and drag to reorder
    - Pipeline Tester: trace hits through each step of a damage pipeline
    - Database: browse every definition, pipeline and unit prefab by category
    - Combat Doctor: find setup problems across all combat assets and units, with fixes
    - Usage Finder: see where an asset or block type is used, and find unused definitions
    - Firable Lab: simulate a firable's DPS, time to kill and reach, or compare every firable
    - Effect Timeline: play a status effect out tick by tick on a sandbox unit
    - Combat Monitor: watch live units and the combat log (including regen, every built-in event and your own events, with a toggle per category) in play mode, and run debug actions on one or more units
    - Faction Matrix: edit every faction relation in one grid
    - Tag Tree: view and re-parent the tag hierarchy
    - Range Gizmos: draw gizmos for ranges and projectile paths in the Scene view, with per-unit Hide and Solo
- Enhanced inspectors for Unit and its components (turn off in `Tools/CG Combat Framework/Enhanced Inspectors`)
- `[SelectType]` fields draw with category colors, a searchable type picker, one-line summaries and a copy/paste menu
- `[BlockInfo]` attribute to describe a building block (every built-in block has one), and `BlockSummary.Register` for custom summaries
- Compact `ScalableValue` drawer and inline rows for stat and loadout entries
- Type colors for damage types, resource types and factions in block summaries
- `Assets/CG Combat Framework/Find Usages` in the Project window's right click menu
- Regen While Dead on resource entries, Delay From Break Only on Shield Absorb, and `IResource.RestartLossTimer()`
- Add built-in events:
    - Lifecycle: `UnitArriving`, `UnitArrived`, `DespawnScheduled`, `UnitDeparting`, `UnitReleased`
    - Firing: `FiringPhaseChanged` (with a `FiringEndReason` for every built-in fire mode), `FirableCharged`, `FirableEquipped`, `FirableUnequipped`, `CooldownStarted`, `CooldownEnded`, `ChargesChanged`, `AmmoChanged`
    - Resources and stats: `ResourceChanged` (every change, including regen and max changes), `ResourceDepleted`, `ResourceFilled`, `ResourceRegenStarted`, `ResourceRegenStopped`, `StatChanged`
    - Status effects: `StatusEffectStacksChanged`, `StatusEffectTicked`, `StatusEffectTriggered`
    - Health and movement: `Revived`, `Knockback`, `Teleported`
    - Projectiles: `ProjectileLaunched`, `ProjectileBounced`, `ProjectileStopped` (with a `ProjectileStopReason`)
    - Scheduling: `Scheduled`, `ScheduleResolved`, `ScheduleCancelled`. `CombatScheduler.Schedule` returns the key they share
    - Global only: `PauseChanged`, `SessionEnded`
- `IHealthListener`, `IResourceListener` and `IStatListener` for reactions that must run in a fixed order, registered with `AddListener` / `RemoveListener` on `IHealth`, `IReadonlyResource` and `IReadonlyStat`, plus `ListenerList<T>` to keep them in your own implementations
- `EventBus.SubscriberCount`
- Unit logs a warning when it despawns with more handlers on its bus than at its last despawn in editor and dev builds
- `IArrivalSequence` and `IDepartureSequence` for timed spawn and despawn presentation, with Arrival Tags and Arrive On Start on `Unit`
- Despawn On Death component
- `IUnitComponent.OnReleased`, `IUnit.IsArriving` / `IsDeparting` and `IHealth.LastDeath`
- Ignore Dead on Sphere, Box, Cone, Unit and Chain queries
- `CombatServices.Time`, seconds of combat time that pause with combat. `CombatScheduler` runs on it
- Data list on `GameDefinition` for your own `IDefinitionData`, read with `IGameDefinition.Data` / `TryGetData<T>()`

### Changed
- **Breaking** Drop `EffectContext` from `ResourceChange`, add `IUnit Source`
- **Breaking** Removed every public C# event: `IUnit.OnSpawned` / `OnDespawned`, `Firable.OnFired` / `OnBlocked`, `FirableLoadout.OnEquipped` / `OnUnequipped`, `IHealth.OnDamaged` / `OnHealed` / `OnDied` / `OnRevived`, `IReadonlyResource.OnChanged` / `OnDepleted`, `IReadonlyStat.OnChanged`, `StatusEffectsComponent.OnApplied` / `OnRemoved` and `CombatServices.OnPauseChanged`. Subscribe to  matching bus event, or use listener interface when order matters
- **Breaking** Custom `IHealth` must now implement `AddListener` / `RemoveListener` and call their listeners after raising `DiedEvent` and `RevivedEvent`
- `CombatTicker` no longer looks up pending removals for every tickable when nothing is pending, and `ResourceComponent` ticks its resources from a flat array
- Improve Event Bus performance
- Improve `TagContainer` performance
- Improve `Unit.TryGet` performance
- Improve `StatBlock`, `ResourceComponent` and `FirableLoadout` lookup performance
- `CombatTag.Parent` is now a property, serialized as `parent`
- **Breaking** Events now must implement `ICombatEvent`
- **Breaking** Event fields follow one convention: `Unit` (the unit whose bus it's raised on) first, then `Source` (who caused it) or `Target` (who it was done to)
    - Damage and heal events: `SourceUnit` / `TargetUnit` → `Unit` + `Source` (received) or `Unit` + `Target` (dealt)
    - `FiredEvent`, `FireBlockedEvent`: `Owner` → `Unit`. Status effect events: `Host` / `Owner` → `Unit`
    - `DiedEvent` gains `Unit` and `Killer`, `KilledUnitEvent` gains `Unit` and `Victim`
    - Reload events hold the runtime `Firable` instead of its `FirableDefinition`
- **Breaking** Removed `ChannelEvent` and `ChannelEndReason`. Use `FiringPhaseChangedEvent` and `FiringEndReason`
- `IHealth.Revive` takes an optional `EffectContext`, so `RevivedEvent` knows who revived the unit
- Add `CombatTag[] ArrivalTags` to `SpawnData`
- `Stat`, `StatBlock`, `Resource` and `ResourceEntry.CreateRuntime` take an optional owner to raise their events on. Without one they raise nothing
- **Breaking** `TagContainer` takes its owner in its constructor (`null` for one that raises nothing), and its events also reach the global bus
- Editor sandbox units (Effect Timeline, Firable Lab, Pipeline Tester) no longer raise on the global bus (`EventBus.ForwardsToGlobal`)
- **Breaking** Sphere, Box, Cone and Unit queries skip dead units by default (turn off Ignore Dead for revives)
- Everyone query's Alive Only renamed to Ignore Dead
- Queries skip despawned units, including ones still departing
- `UnitViewBinder` unbinds views when the unit is released instead of when it despawns
- Unit clears its tags when it's released instead of when it spawns
- Pooled units return to the pool after their departure finishes

### Performance

Compared with v0.2.0. Medians from `Tests/Performance`, Unity 6000.3 editor (Mono). Tag rows hold 8 tags.

| Operation | v0.2.0 | Unreleased | 
|---|---|---|
| Event raise, no listeners | 7.3 ns | 2.9 ns |
| Event raise, one handler | 15.2 ns | 4.5 ns |
| Event raise, ten handlers | 30.2 ns | 19.6 ns |
| `GameEvents.Raise` to a unit and the global bus | 22.3 ns | 9.6 ns |
| `TagContainer.Has`, held tag | 19.4 ns | 8.2 ns |
| `TagContainer.Has`, parent of a held tag | 226 ns | 8.3 ns |
| `TagContainer.Has`, tag not held | 235 ns | 6.2 ns |
| `TagContainer.HasAny`, 3 tags not held | 710 ns | 17.5 ns |
| `Unit.TryGet`, found | 21.2 ns | 8.7 ns |
| `Unit.TryGet`, not found | 18.9 ns | 7.1 ns |
| `GetStat` | 52.1 ns | 24.7 ns |
| `IStatProvider.GetValue` | 26.4 ns | 15.1 ns |
| Damage pipeline, all 10 built-in steps | 410 ns | 363 ns |
| Combat tick, 200 burning units | 33.3 µs | 26.2 µs |
| Resource tick, 200 units with 3 full resources | 195.2 µs | 179.6 µs |
| Regen tick, 400 resources with no owner | 47.6 µs | 34.0 µs |

### Fixed
- Spawning pooled unit threw `NullReferenceException`, because its tags didn't exist until `Awake`
- Pooled unit's starting tags were counted twice in first life
- `ReloadCompletedEvent` was raised before magazine refilled
- Effect Timeline: a Kill payload on host raises `DiedEvent`
- `EmitAtPayload` with Emit From Target started at target's forward vector instead of its position
- Arrays of building blocks no longer get cut off at 520px in the inspector
- Expand arrows on blocks in an array no longer overlap the list's drag handle
- Resources other than health no longer regenerate while their unit is dead
- Shield Absorb: hits on an empty shield now restart its Delay After Loss
- `PooledUnitSpawner.Spawn` allocated on every call

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
