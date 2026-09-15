# Architecture

Reviewed against main `147df77146dd0ad7c53355e9c77da119f29f5b00` on 2026-09-15 (UTC). Status describes source inspection, not a runtime test.

## Current implementation

| Source | Responsibility and remaining coupling |
| --- | --- |
| `Scripts/Game/Main.cs` | Builds map, actors, mod spawn pool, UI and camera; executes player/enemy turns; handles digging, door refresh, victory and restart. |
| `Scripts/Actors/Player.cs` | Godot input, weapon/tool state, facing display; owns an `ActorState` (GridPosition, health) and keeps its pixel Position synced from it via `PlaceAt`/`Move`. |
| `Scripts/Actors/ActorState.cs` | Engine-independent GridPosition and health for one actor. Player treats an instance as authoritative; Enemy does not use it yet. No instance ID, facing, equipment or attack/behavior state - see target shape below. |
| `Scripts/Actors/Enemy.cs` | Holds monster definition and behavior instance; health, attack execution, pixel movement, labels, facing and QueueFree lifecycle. Still fully node-based. |
| `Scripts/Dungeon/DungeonMap.cs` | Engine-independent cells, integer GridPosition, zones, durability, IsOpen, and disabled dynamic terrain timers/randomness. |
| `Scripts/Dungeon/DungeonGenerator.cs`, `ZoneTemplate.cs` | Seeded connected layout and transformed room templates. |
| `Scripts/Dungeon/DigResolver.cs` | Engine-independent tool damage against destructible terrain. |
| `Scripts/Dungeon/DungeonRenderer.cs` | Cell-to-node lookup, initial render and RefreshCell; loads visuals from map state. |
| `Scripts/Combat/AttackDefinition.cs`, `AttackResolver.cs` | Shared attack configuration, per-actor preparation and damage resolution through ICombatant; still use Godot Vector2 pixel coordinates. |
| `Scripts/Weapons/WeaponDefinition.cs`, `Scripts/Equipment/EquipmentDefinition.cs` | Two weapon definitions and independent digging tool; no stable weapon/tool IDs yet. |
| `Scripts/Monsters/*` | Stable monster IDs, attack-pattern IDs, behavior factories and sprite loading. Behavior APIs still reference Player, Godot vectors and visual methods. |
| `Scripts/Modding/MonsterModLoader.cs` | File/JSON parsing and definition validation; loaded definitions join the built-in spawn list. |

Terrain queries are already unified: Main injects its DungeonMap-backed IsWallAt delegate into Enemy.Configure. Do not reintroduce scene-wall scanning.

Digging, door visual removal, changed-cell rendering, and keyboard weapon selection are implemented. Player's GridPosition and health now live in `ActorState`, not just node fields; Enemy's do not yet, and combat/occupancy (`ICombatant.Position`, `AttackResolver`, `Main`'s enemy occupancy sets) still work entirely in pixel `Vector2`. The remaining core problem is actor and turn state living in nodes, not missing map infrastructure.

## State authority and target responsibilities

The live source of truth will be ordinary in-memory C# objects. JSON is the storage/export representation of a copied snapshot. Gameplay never parses JSON per action and never derives state from sprite positions.

| Component | Target responsibility |
| --- | --- |
| GameState | Map, actors, stable execution order, turn number, run status, and run configuration/seed references. |
| ActorState | Unique instance ID, definition ID, integer position, health, facing, equipment, attack preparation and enemy behavior state. Player currently has a first slice (GridPosition, health only); Enemy, instance IDs, facing, equipment and attack/behavior state are not migrated yet. |
| TurnResolver | Validate commands and execute complete turns; return structured outcomes and changed actor/cell IDs. |
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

1. Reject gameplay input before selection or after game end.
2. Resolve a pending/detected attack; otherwise try adjacent digging; otherwise move or bump.
3. On player movement, update zone membership and open any door at the destination.
4. Remove defeated enemies and check global victory. Victory can end the turn before enemies act.
5. Execute surviving enemies in stable order, each reading the updated state. Open the door at each enemy's resulting position. Stop on player death.
6. Finalize removals and victory checks.
7. Proposed single completion boundary: increment turn, capture state and structured outcomes, append debug history, enqueue autosave, and update presentation. This must also run on terminal turns that skip the enemy phase.

Blocked movement and digging consume a turn. Menus and camera controls do not. Wait is not yet implemented. Snapshot work must not add another enemy phase.

An optional environment phase belongs before final capture if later enabled. Main currently never calls DungeonMap.AdvanceTurn; keep that disabled during extraction.

## Actor lifecycle and AI state

Actor health/alive state must determine turn eligibility and occupancy, independently of QueueFree or animations. An actor killed during the player action must not act later that turn.

Use GridPosition throughout simulation. Convert to pixels only in views. Preserve order explicitly; JSON object iteration or node order must not determine enemy execution.

ChasePlayerBehavior currently stores `_hasPreparedMove` privately, while facing lives on Enemy. Move both into capturable actor behavior state. Saving only coordinates would make a chaser prepare again after load. AttackState's preparing flag, remaining turns and locked direction also matter even when currently equipped attacks have zero preparation.

## Terrain and rendering

DungeonCell already has IsOpen. Doors remain walkable whether open or not; player and enemies both reveal/open them on occupancy. Preserve this rule. Blocking/locked doors are a later gameplay decision, not a required refactor.

DigResolver damages terrain; destruction becomes floor. DungeonRenderer.RefreshCell clears and rebuilds that cell. It does not refresh neighbors automatically, and repeated Render calls append nodes: loading must create a fresh view or explicitly clear it. If a mutation changes neighboring wall art, refresh those neighbors too.

TreeWall/GrowingWall code exists but is disabled in the playable loop. Future snapshots must include pending regrowth kind/countdown, terrain turn counter and resumable terrain RNG state if this mechanic is enabled. Those values are currently private and no persistence API exists. Do not assume a default serializer captures them.

Zone connections describe generation; current cells determine traversal. Neutral shared boundaries need not become new room objects.

## Combat, equipment and mods

Keep definitions separate from per-actor runtime state. Protect shared offset collections when converting combat to grid coordinates. Ordered-line wall blocking remains suitable for swords; define occlusion before adding sweeps or area patterns.

Preserve MonsterDefinition.Id, movement IDs and attack-pattern IDs through the migration. Add stable weapon/tool IDs before saving them; display names are not durable keys. Only MonsterDefinition.PrimaryAttack (the first attack) is currently used. StatusEffectId and LootTableId are metadata without consumers.

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

Completed: shared map blocking, digging/tool separation, cell refresh, door opening graphics, keyboard weapon menu, monster definitions and reusable behaviors, Player's GridPosition/health extracted into ActorState.

Next: give Enemy the same ActorState treatment, then convert `ICombatant`/`AttackResolver` and Main's occupancy/wall queries from pixel `Vector2` to `GridPosition`; extract complete turns using the existing behavior registry; capture snapshots and 10-turn history; implement versioned run save/resume. Keep gameplay rules unchanged throughout. See [Next steps](NEXT_STEPS.md).

## Verification and deferred work

There are 35 test methods: generator/terrain 8, weapon 6, digging 3, dynamic terrain 4, mod loader 8, ActorState 6. Source inspection only in this documentation review; no test execution claim.

Highest-value additions: full turns/death/order, chaser intent, grid/visual independence, snapshot copy isolation, history rollover, save/load next-turn equivalence, terrain restoration, mod compatibility and failed-save backup recovery. Use small Godot checks for focus, door art and loading views.

No ECS, global event bus, DI framework, generic ability scripting, room streaming or full replay system. Keep full snapshots until measured size justifies a different representation.
