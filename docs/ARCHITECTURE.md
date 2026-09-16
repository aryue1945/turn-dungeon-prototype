# Architecture

Reviewed against main `147df77146dd0ad7c53355e9c77da119f29f5b00` on 2026-09-15 (UTC). Status describes source inspection, not a runtime test.

## Current implementation

| Source | Responsibility and remaining coupling |
| --- | --- |
| `Scripts/Game/Main.cs` | Builds map, actors, mod spawn pool, UI and camera; delegates the player's action and each enemy's turn to `TurnResolver` and narrates/renders the result; still owns the enemy loop, victory/death checks and restart directly. Keeps a `GameState` in sync (AddEnemy/RemoveDefeatedEnemies/CompleteTurn/SetStatus) but does not yet read from it to decide anything. |
| `Scripts/Game/GameState.cs` | Engine-independent aggregate: `Map` (DungeonMap), `Player`/`Enemies` (ActorState references), `TurnNumber`, `Status` (RunStatus). A thin container Main updates each turn - not a rules engine, and nothing reads it yet. See target shape below. |
| `Scripts/Game/TurnResolver.cs` | Milestone-2 work. `ResolvePlayerAction` resolves attack -> dig -> move/bump for one player input against `IPlayerTurnActor` (Player already satisfies it) and returns a typed `PlayerActionOutcome`; fully engine-independent and unit tested. `ResolveEnemyAction` is thinner: it still calls `Enemy.TakeTurn` (a concrete Godot node, movement behaviors unchanged) and only extracts the door-opening step Main used to do inline, returning `EnemyActionOutcome` (resulting position, door opened). Not unit tested - needs a real Enemy/Player node. |
| `Scripts/Actors/Player.cs` | Godot input, weapon/tool state, facing indicator display; owns an `ActorState`. `ActorState` is authoritative for GridPosition/health/facing (pixel Position and indicator rotation are derived from it via `PlaceAt`/`Move`/`SetFacingDirection`); Player stays authoritative for which `WeaponDefinition`/`DiggingToolDefinition`/`AttackState` are equipped, and pushes their ids/reference into `ActorState` (`EquipWeapon`/`EquipDiggingTool`/`_Ready`) so a reader does not need the node. |
| `Scripts/Actors/ActorState.cs` | Engine-independent unique instance id (`Guid`), definition id (`string`, required), GridPosition, health, facing (`Vector2`), one enemy-behavior flag (`HasPreparedMove`, formerly private on `ChasePlayerBehavior`), equipment ids (`WeaponId`/`ToolId`, Player-only for now) and an `Attack` reference (the same `AttackState` instance the owning Player/Enemy mutates, not a copy). Player and Enemy both treat an instance as authoritative. |
| `Scripts/Actors/Enemy.cs` | Holds monster definition and behavior instance; owns an `ActorState` (GridPosition, health, facing, prepared-move flag, Attack reference) via `Configure`. Pixel movement sync, labels, facing indicator and QueueFree lifecycle remain node-based; attack execution itself already runs through `AttackResolver` in grid coordinates. |
| `Scripts/Dungeon/DungeonMap.cs` | Engine-independent cells, integer GridPosition, zones, durability, IsOpen, and disabled dynamic terrain timers/randomness. |
| `Scripts/Dungeon/DungeonGenerator.cs`, `ZoneTemplate.cs` | Seeded connected layout and transformed room templates. |
| `Scripts/Dungeon/DigResolver.cs` | Engine-independent tool damage against destructible terrain. |
| `Scripts/Dungeon/DungeonRenderer.cs` | Cell-to-node lookup, initial render and RefreshCell; loads visuals from map state. |
| `Scripts/Combat/AttackDefinition.cs`, `AttackResolver.cs` | Shared attack configuration, per-actor preparation and damage resolution through ICombatant. `ICombatant.GridPosition` and `AttackResolver`'s offset math are integer grid coordinates now, not pixel Vector2. |
| `Scripts/Weapons/WeaponDefinition.cs`, `Scripts/Equipment/EquipmentDefinition.cs` | Two weapon definitions and independent digging tool, both with a stable `Id` (`IEquipment.Id`) alongside `Name`. Player holds the definitions directly (needed for their behavior/stats) and mirrors just the ids into `ActorState.WeaponId`/`ToolId`. |
| `Scripts/Monsters/*` | Stable monster IDs, attack-pattern IDs, behavior factories and sprite loading. `IEnemyMovementHost`/`IEnemyMovementBehavior` use GridPosition for position and read/write `HasPreparedMove` through the host (backed by ActorState) instead of a private field; behaviors are now stateless. Still reference Player, Godot direction vectors and visual methods (`SetFacingIndicatorVisible`, etc). |
| `Scripts/Modding/MonsterModLoader.cs` | File/JSON parsing and definition validation; loaded definitions join the built-in spawn list. |

Terrain queries are already unified: Main injects its DungeonMap-backed IsWallAt delegate into Enemy.Configure. Do not reintroduce scene-wall scanning.

Digging, door visual removal, changed-cell rendering, and keyboard weapon selection are implemented. Player and Enemy both keep a unique instance id, definition id, GridPosition, health, facing, equipment ids/Attack reference and (for Enemy) the chaser's prepared-move flag in an `ActorState`; combat and occupancy (`ICombatant.GridPosition`, `AttackResolver`, Main's `IsWallAt`/enemy-occupancy sets/spawn-cell selection) all work in `GridPosition` now, not pixel `Vector2` - only node `Position`/indicator rotation (rendering/camera) stay pixel-based, synced from each actor's `ActorState`. Door-opening is now a `TurnResolver` responsibility (`DungeonMap.OpenDoor` called from both `ResolvePlayerAction` and `ResolveEnemyAction`); Main's old `OpenDoorAt` helper is gone. `GameState` exists and mirrors the run (map, player/enemy ActorState references, turn number, status) accurately, but Main still owns every decision - GameState is not yet a dependency of any rule. NEXT_STEPS milestone 1 is complete; milestone 2 has both a player and an enemy slice: `TurnResolver.ResolvePlayerAction` fully owns the player's attack/dig/move rules and returns a typed outcome, tested without any Godot node via `IPlayerTurnActor`; `TurnResolver.ResolveEnemyAction` wraps `Enemy.TakeTurn` plus door-opening but does not yet return a rich outcome (movement behaviors are still void/callback-based) and needs a real node to test. The enemy loop itself, victory/death checks and `GameState`-as-input are still Main's job.

## State authority and target responsibilities

The live source of truth will be ordinary in-memory C# objects. JSON is the storage/export representation of a copied snapshot. Gameplay never parses JSON per action and never derives state from sprite positions.

| Component | Target responsibility |
| --- | --- |
| GameState | Map, actors, stable execution order, turn number, run status, and run configuration/seed references. Implemented now (`Map`, `Player`/`Enemies` as ActorState references, `TurnNumber`, `Status`); run configuration/seed references beyond `Map.Seed` are not included, and nothing consumes it as an input yet - that's TurnResolver's job. |
| ActorState | Unique instance ID, definition ID, integer position, health, facing, equipment, attack preparation and enemy behavior state. Implemented: instance id, definition id, GridPosition, health, facing, HasPreparedMove, WeaponId/ToolId (Player-only), and an Attack reference (the actor's live AttackState, not a copy). |
| TurnResolver | Validate commands and execute complete turns; return structured outcomes and changed actor/cell IDs. Implemented: `ResolvePlayerAction` (attack -> dig -> move/bump for one player input, returns `PlayerActionOutcome`) and `ResolveEnemyAction` (wraps `Enemy.TakeTurn` + door-opening, returns the thinner `EnemyActionOutcome`). Command validation/rejection, the enemy-phase loop itself, and GameState-as-input are not there yet. |
| Existing movement behaviors | Retain behavior IDs/factories, migrate decisions to state and explicit outcomes. Remove Player/node/visual dependencies. Do not add a competing EnemyBrain registry. |
| AttackResolver / DigResolver | Apply combat and digging rules against authoritative state. |
| DungeonMap / DungeonGenerator | Keep current map and generation responsibilities. |
| Main, actor views, DungeonRenderer, UI | Assemble the run, translate input, display state/results, manage camera and presentation. |
| Snapshot capture | Copy complete committed state into versioned, serializable data. No live references. |
| RunSaveService (planned) | Serialize/validate/load the current run, safely replace saves and retain a backup. |
| DebugHistory (planned) | Retain the last 10 turn transitions and starting state; export human/AI-readable JSON. |
| Later progression storage | Save persistent unlocks, balances and cosmetic ownership separately from a run. |

These are responsibilities, not a requirement for an interface or separate project per row. Use direct calls and small data objects. Rendering, saving and debug export consume the same state; they do not own alternative gameplay copies.

## Turn contract

Preserve the current sequence during extraction:

1. Reject gameplay input before selection or after game end. (Still Main's own guard in `OnPlayerMoveRequested`, not yet moved into TurnResolver.)
2. Resolve a pending/detected attack; otherwise try adjacent digging; otherwise move or bump. (Implemented in `TurnResolver.ResolvePlayerAction`.)
3. On player movement, update zone membership and open any door at the destination. (Door-opening is inside `ResolvePlayerAction` now, since it's a DungeonMap mutation; zone membership stays in Main as a presentation-adjacent side effect of the returned `Moved` outcome.)
4. Remove defeated enemies and check global victory. Victory can end the turn before enemies act.
5. Execute surviving enemies in stable order, each reading the updated state. Open the door at each enemy's resulting position. Stop on player death.
6. Finalize removals and victory checks.
7. Single completion boundary: increment turn, capture state and structured outcomes, append debug history, enqueue autosave, and update presentation. This must also run on terminal turns that skip the enemy phase. `GameState.CompleteTurn()` (turn increment only) is wired at both exit points of `Main.OnPlayerMoveRequested` now; the state/outcome capture, debug history and autosave pieces are still proposed, not implemented.

Blocked movement and digging consume a turn. Menus and camera controls do not. Wait is not yet implemented. Snapshot work must not add another enemy phase.

An optional environment phase belongs before final capture if later enabled. Main currently never calls DungeonMap.AdvanceTurn; keep that disabled during extraction.

## Actor lifecycle and AI state

Actor health/alive state must determine turn eligibility and occupancy, independently of QueueFree or animations. An actor killed during the player action must not act later that turn.

Use GridPosition throughout simulation. Convert to pixels only in views. Preserve order explicitly; JSON object iteration or node order must not determine enemy execution.

Done: facing and the chaser's prepared-move flag now live on ActorState (`Facing`, `HasPreparedMove`) instead of a private Enemy field and a private ChasePlayerBehavior field; `IEnemyMovementHost` exposes both so ChasePlayerBehavior itself is stateless. Saving GridPosition/health alone would still make a chaser prepare again after load - that gap is closed now. AttackState's preparing flag, remaining turns and locked direction still live only on the separate AttackState object (already non-node, but not yet referenced from ActorState) and matter even when currently equipped attacks have zero preparation.

## Terrain and rendering

DungeonCell already has IsOpen. Doors remain walkable whether open or not; player and enemies both reveal/open them on occupancy. Preserve this rule. Blocking/locked doors are a later gameplay decision, not a required refactor.

DigResolver damages terrain; destruction becomes floor. DungeonRenderer.RefreshCell clears and rebuilds that cell. It does not refresh neighbors automatically, and repeated Render calls append nodes: loading must create a fresh view or explicitly clear it. If a mutation changes neighboring wall art, refresh those neighbors too.

TreeWall/GrowingWall code exists but is disabled in the playable loop. Future snapshots must include pending regrowth kind/countdown, terrain turn counter and resumable terrain RNG state if this mechanic is enabled. Those values are currently private and no persistence API exists. Do not assume a default serializer captures them.

Zone connections describe generation; current cells determine traversal. Neutral shared boundaries need not become new room objects.

## Combat, equipment and mods

Keep definitions separate from per-actor runtime state. Protect shared offset collections when converting combat to grid coordinates. Ordered-line wall blocking remains suitable for swords; define occlusion before adding sweeps or area patterns.

Preserve MonsterDefinition.Id, movement IDs and attack-pattern IDs through the migration. WeaponDefinition and DiggingToolDefinition now have a stable `Id` (`core.basic_sword`, `core.long_sword`, `core.basic_shovel`) via `IEquipment.Id`, mirrored into `ActorState.WeaponId`/`ToolId` so a save can reference them without relying on display names or the Godot node. Only MonsterDefinition.PrimaryAttack (the first attack) is currently used. StatusEffectId and LootTableId are metadata without consumers.

Mod loading uses unsorted directory/file enumeration and has no duplicate-ID check. Reproducible setup and save compatibility require stable ordering, unique definitions and content fingerprints. Missing/changed required definitions must produce a clear load error rather than silently resetting actors. Details: [Modding](MODDING.md).

## Saving and recent-turn diagnostics

See [Save and debug history](SAVE_AND_DEBUG_HISTORY.md) for the shared snapshot contract. Neither feature is implemented yet.

- Resume: one committed current-run snapshot plus backup; reload actual mutated terrain and actors.
- Debug history: last 10 completed turn transitions, represented by up to 11 independent state snapshots plus commands and ordered outcomes.
- Debug export is inspectable evidence, not exact replay, screenshots, undo, or a previous-floor archive.
- Snapshot once at the stable boundary. Background serialization uses an immutable copy, never the live mutable graph.
- Keep state authority in C#; exporting or redrawing must not advance the simulation or consume RNG.

The map seed currently reproduces generation only. Main's random spawn positions are separately randomized. Record actual state now; establish reproducible setup and restorable runtime RNG before claiming deterministic continuation of random mechanics.

## Migration order

Completed: shared map blocking, digging/tool separation, cell refresh, door opening graphics, keyboard weapon menu, monster definitions and reusable behaviors, Player's and Enemy's GridPosition/health extracted into ActorState, `ICombatant`/`AttackResolver`/Main's occupancy and wall queries converted from pixel `Vector2` to `GridPosition`, facing and the chaser's prepared-move flag moved into ActorState, ActorState instance/definition ids, stable weapon/tool ids, GameState introduced and kept in sync by Main, equipment ids and AttackState referenced from ActorState (NEXT_STEPS milestone 1 done), TurnResolver.ResolvePlayerAction and TurnResolver.ResolveEnemyAction extracted (milestone 2).

Next: make movement behaviors return an explicit outcome instead of void/callbacks so ResolveEnemyAction can report what actually happened; move the enemy-phase loop, victory/death checks and command rejection into TurnResolver so it - not Main - reads/writes GameState; capture snapshots and 10-turn history; implement versioned run save/resume. Keep gameplay rules unchanged throughout. See [Next steps](NEXT_STEPS.md).

## Verification and deferred work

There are 56 test methods: generator/terrain 8, weapon 6, digging 4, dynamic terrain 4, mod loader 8, ActorState 14, GameState 6, TurnResolver 6. Source inspection only in this documentation review; no test execution claim.

Highest-value additions: full turns/death/order, chaser intent, grid/visual independence, snapshot copy isolation, history rollover, save/load next-turn equivalence, terrain restoration, mod compatibility and failed-save backup recovery. Use small Godot checks for focus, door art and loading views.

No ECS, global event bus, DI framework, generic ability scripting, room streaming or full replay system. Keep full snapshots until measured size justifies a different representation.
