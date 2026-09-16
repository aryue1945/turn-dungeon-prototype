# Next Steps

Reviewed against main `147df77146dd0ad7c53355e9c77da119f29f5b00` on 2026-09-15 (UTC). Implemented status is based on source inspection.

This roadmap separates completed foundation work from planned changes. Save/resume and 10-turn diagnostics are agreed design direction, not implemented features.

## Completed foundation

- Shared DungeonMap blocking: Main injects the same query into enemies.
- DigResolver and separate Basic Shovel; destructible walls become floor.
- Distinct breakable-wall brick texture and DungeonRenderer.RefreshCell.
- Door IsOpen state and visual removal for player/enemy occupancy; doors remain walkable before opening.
- Keyboard weapon selection with initial focus, arrow neighbors, confirm and 1/2 shortcuts.
- Stable monster definitions, reusable movement behavior IDs, and JSON data-only monster mods.
- 110 test methods covering generation, weapons, digging, disabled dynamic terrain, mod loading, ActorState, GameState, TurnResolver, enemy movement behaviors, GameSnapshot, DebugHistory, its exporter, map restore, actor restore, and run save serialization.
- Milestone 1 (ActorState/GameState/grid combat) is done; milestone 2 (complete-turn execution) is substantially done; milestone 3 (snapshot capture/debug history) is substantially done; milestone 4 (save/resume) is in progress - see below.

Tree/growing walls remain disabled in generation and the turn loop. Do not re-enable them incidentally. Full turns and save/load have no implementation or tests yet; debug export does now.

## 1. Authoritative actor state and grid combat (done)

Add ActorState and GameState with instance IDs, GridPosition, health, facing, equipment and attack/behavior state. Convert combat and occupancy to cells. Preserve existing monster IDs and behavior factories; add stable weapon/tool IDs.

`Scripts/Actors/ActorState.cs` (unique `InstanceId` (Guid), required `DefinitionId`, GridPosition, health, facing, HasPreparedMove, WeaponId/ToolId, Attack reference; engine-independent, unit tested). Player and Enemy both own one as their authoritative source; `PlaceAt`/`Move`/`SetFacingDirection` (Player) and `Configure`/`TryMoveForward`/`IEnemyMovementHost` (Enemy) keep pixel Position and facing indicator rotation in sync from it. `ICombatant.GridPosition` replaced pixel `Position`; `AttackResolver`'s offset math and Main's `IsWallAt`/`OpenDoorAt`/enemy-occupancy sets/spawn-cell selection all work in `GridPosition` now. `ChasePlayerBehavior._hasPreparedMove` and Enemy's `_facingDirection` are gone, both read/write ActorState through `IEnemyMovementHost`. `WeaponDefinition`/`DiggingToolDefinition` (`IEquipment`) carry a stable `Id` (`core.basic_sword`, `core.long_sword`, `core.basic_shovel`), mirrored into `ActorState.WeaponId`/`ToolId` by Player's `EquipWeapon`/`EquipDiggingTool`. `ActorState.Attack` holds the same `AttackState` instance Player/Enemy already mutate (set once in `_Ready`/`Configure`), not a copy. `Scripts/Game/GameState.cs` exists (Map, Player/Enemies as ActorState references, TurnNumber, Status) and Main keeps it in sync via `AddEnemy`/`RemoveDefeatedEnemies`/`CompleteTurn`/`SetStatus`.

GameState is not yet a dependency of any rule - Main still decides everything from its own fields and reads/writes GameState only as a mirror. That's milestone 2's job.

Acceptance: rules no longer read node positions; views derive positions from state; definitions remain separate from runtime data; a slow chaser's prepared move can be captured explicitly.

Risks (addressed): coordinate rotation, duplicate actor occupancy, lost private AI state, shared definition mutation.

## 2. Complete-turn execution (substantially done)

Extract TurnResolver and adapt existing movement behaviors to state-based decisions/outcomes. Keep attack -> dig -> move/bump priority, sequential enemy order, door-on-occupancy behavior, and early victory/death semantics.

Done: `Scripts/Game/TurnResolver.cs` - `ResolvePlayerAction` resolves the player's half of a turn (attack -> dig -> move/bump) and returns a typed `PlayerActionOutcome` (Attacked/Preparing/TerrainDug/TerrainDestroyed/Moved/Blocked). It performs every gameplay mutation itself (damage via AttackResolver, terrain via DigResolver, movement, door-opening); `Main.ApplyPlayerActionOutcome` only narrates and refreshes changed cells. Tested via a small `IPlayerTurnActor` interface (Player already satisfies it - no Godot node needed in tests) and a hand-built `DungeonMap`, not the generator. `ResolveEnemyAction` wraps one enemy's turn (`Enemy.TakeTurn`) plus door-opening, returning `EnemyActionOutcome`; Main's old `OpenDoorAt` helper is gone, replaced by both resolver methods calling `DungeonMap.OpenDoor` directly.

`IEnemyMovementBehavior.TakeTurn` now returns `EnemyActionResult` (Idle/Prepared/Moved/Attacked/Preparing/Blocked, with an attack name where relevant) instead of `void` - `Enemy.TryMoveForward` returns the same type and no longer prints directly (Main narrates via `ApplyEnemyActionOutcome`, matching the player side). The `player` parameter narrowed from the concrete `Player` class to `ICombatant` (only `GridPosition` was ever used), so all four behaviors are now unit tested (`Tests/EnemyMovementBehaviorTests.cs`) against a fake host and fake player - no Godot node needed, mirroring `IPlayerTurnActor`.

`GameState` gained `IsPlayerDefeated`/`AreAllEnemiesDefeated`, and `Main.CheckForVictory`/`TakeEnemyTurns` read those instead of `_enemies.Count`/`_player.Health` directly - both were already kept live in sync, so this is the first rule that actually depends on GameState rather than mirroring into it.

Deliberately deferred, judged not worth closing before moving to milestone 3: the enemy-phase loop (iterate enemies, stop on player death) stays in `Main.TakeEnemyTurns` - moving it into TurnResolver would mean passing it a pile of delegates for Main's enemy-liveness/occupancy/combatant-list logic, relocating complexity rather than reducing it. `ResolveEnemyAction` still takes concrete `Enemy`/`Player` and needs a real node to test itself (only the behavior decision inside it is decoupled). The top-of-method gameplay-input guard and the completion-boundary concept (see Turn contract step 7 in ARCHITECTURE.md) are still Main's own code, not a formal TurnResolver step.

Acceptance: plain NUnit tests cover complete turns, dead enemies never act, later enemies stop after player death, and each consumed command has exactly one completion boundary even on terminal turns.

Risks: extra enemy phases, chaser timing changes, turning after attacks, node-deletion timing leaking into rules. Keep dynamic terrain disabled.

## 3. Snapshot capture and 10-turn debug history (substantially done)

Implement independent full snapshots and a bounded history using [the snapshot contract](SAVE_AND_DEBUG_HISTORY.md). Store 10 transitions plus their initial state, with 2D cells, actor details, commands and structured outcomes. Add Export Debug History during play and on the end screen.

`Scripts/Game/GameSnapshot.cs` - `GameSnapshot.Capture(GameState)` deep-copies the map (`GridSnapshot`/`CellSnapshot`: terrain, durability, IsOpen, zone/connection ids) and every actor (`ActorSnapshot`: instance/definition id, position, health, facing, prepared-move flag, weapon/tool ids, attack-preparation state) into independent, serializable objects. Unit tested that later mutating the live `DungeonMap`/`ActorState` cannot change an already-captured snapshot (`Tests/GameSnapshotTests.cs`). `Scripts/Game/DebugHistory.cs` - `TurnTransition` bundles one turn's direction, `PlayerActionOutcome`, `EnemyActionOutcome` list and resulting `GameSnapshot`; `DebugHistory` keeps the latest 10 transitions plus `BoundarySnapshot` (the evicted transition's own resulting snapshot doubles as "the state right before the new oldest retained transition"). Unit tested against the spec's own turn-25-retains-states-15..25 example (`Tests/DebugHistoryTests.cs`). Wired into `Main.cs`: `SelectWeapon` constructs the initial `DebugHistory` (matching "after setup/weapon choice, before the first command"); `OnPlayerMoveRequested` appends a transition at both completion boundaries (normal and early-victory/death), after `CompleteTurn()` so the transition's turn number matches its own resulting snapshot; `TakeEnemyTurns` now returns the `List<EnemyActionOutcome>` a transition carries. `Scripts/Game/DebugHistoryExporter.cs` - `ToJson` serializes a `DebugHistory` with `System.Text.Json` (indented, camelCase, string enums), unit tested against the parsed JSON structure (`Tests/DebugHistoryExporterTests.cs`). A HUD button (visible during play and on the end screen, since the HUD panel is never hidden) calls `Main.OnExportDebugHistoryPressed`, which writes the JSON under `OS.GetUserDataDir()`.

Export overhauled since the paragraph above was first written: the Export button and a new "X" keybinding both open a modal menu (`Main.CreateExportMenu`) offering Last 3/5/10 turns (5 focused as the default) or Cancel; opening/using it only toggles UI visibility and reads the already-captured `DebugHistory`, so it cannot consume a turn, mutate `GameState`, or draw RNG. `DebugHistory.GetSnapshotBefore`/`GetLastTransitions` select a menu-sized slice - N transitions plus the one snapshot immediately before the first of them (e.g. last 5 of 25 turns yields state 20 then transitions 21..25) - unit tested against that exact case (`Tests/DebugHistoryTests.cs`). `DebugHistoryExporter.ToJson(DebugHistory, int, DebugHistoryExportContext)` serializes that slice as a `DebugHistoryExport`: schema version (2), a `RunId` generated once per session, the dungeon seed, a `FloorId` left `null` (still no floor concept), a best-effort `BuildVersion` (assembly version), a `ContentFingerprint` (`DebugHistoryExportContext.ComputeContentFingerprint`, a SHA-256 hash over the current weapon/monster rosters' gameplay fields), and compact `WeaponSummary`/`MonsterSummary` descriptions of every weapon and the actual spawn pool for the run (`Scripts/Game/DebugHistoryExportContext.cs`). `CellSnapshot.ActorInstanceIds` is now populated - derived at `GameSnapshot.Capture` time from the authoritative Player/Enemies lists, never a second source of truth, unit tested for both an empty cell and two actors sharing one cell (`Tests/GameSnapshotTests.cs`). Attacks are recorded properly instead of a default `(0,0)` target cell: `PlayerActionOutcome.TargetCell` is now `GridPosition?` (null for Attacked/Preparing), and both `PlayerActionOutcome`/`EnemyActionOutcome` carry an `AttackExecutionDetail` (attacker id, every cell the attack pattern swept, and a per-target `AttackHitDetail` with damage/remaining health/defeated) built by `AttackResolver.TryAttack`'s new `out` parameter - unit tested for a normal hit, a defeat, and the no-detection-target case (`Tests/WeaponAttackTests.cs`). `EnemyActionOutcome.ActorInstanceId` now identifies which enemy an outcome belongs to, previously only implied by list position. Remaining, deliberately deferred: no run/floor concept beyond the generated `RunId`/`null` `FloorId`, and the Main wiring (DebugHistory construction/append, menu open/close, export handlers) has no automated test - Main is a Godot node - only the pieces it calls are unit tested.

Acceptance: advancing the live game does not alter past snapshots; at turn 25 the retained states are 15..25; terminal turns are retained; exported JSON explains a blocked/prepared actor; export consumes no turn/RNG.

Risks: shallow copies, transposed rows, wrong retention count, omitting the last death/win turn, treating snapshots as screenshots or replay.

## 4. Current-run save/resume (in progress)

Reuse snapshot data with a versioned save envelope and explicit restore mapping. Restore actual terrain, actors, equipment and intent before building fresh views. Save at setup and completed turns; keep a backup and safe file replacement. Retain persistent profile data separately when introduced later.

Done: `Scripts/Game/GameSnapshotRestore.cs` - `RestoreMap(GridSnapshot, seed)` rebuilds a `DungeonMap` from saved terrain/durability/IsOpen/zone-connection data, via a new internal `DungeonMap.RestoreCell` primitive (`SetTerrain` resets durability/IsOpen to defaults, which restore must not do - a saved cell may be mid-damage or an already-open door). `RestoreActor(ActorSnapshot, AttackState)` rebuilds an `ActorState` using only its existing constructor/setters (health restored via `TakeDamage(MaxHealth - snapshot.Health)`, since there is no direct setter) plus a new `AttackState.RestorePreparation` (unlike `BeginPreparation`, does not reset remaining turns to `Definition.PreparationTurns` - a restored attack may be mid-preparation). It takes an already-resolved `AttackState` rather than building one itself, since turning a WeaponId/DefinitionId into a definition is actor-type-specific. `DungeonMap`/`ActorState`/`AttackState` all stay unaware of the snapshot types; the restore orchestration lives in the Game layer, which already depends on Actors/Dungeon. Unit tested (`Tests/GameSnapshotRestoreTests.cs`, `Tests/ActorRestoreTests.cs`).

`Scripts/Game/RunSaveEnvelope.cs`/`RunSaveSerializer.cs` - `RunSaveEnvelope` wraps a `GameSnapshot` with `SchemaVersion` and `Seed` (generation config kept separate from committed state, per the spec's "Configuration" bucket). `RunSaveSerializer.ToJson`/`FromJson` round-trip it via `System.Text.Json`, returning a typed `RunSaveLoadOutcome` (Loaded/UnsupportedVersion/Invalid) instead of throwing - the "reject unsupported versions... with a useful error" requirement. Getting the round-trip actually correct needed two fixes caught by its own tests before merge: `GridSnapshot.Rows`'s exposed type had to exactly match its constructor parameter type for `System.Text.Json`'s parameterized-constructor binding to find it at all, and `Godot.Vector2` needed a hand-written `Vector2JsonConverter` (`Scripts/Game/GameJsonOptions.cs`, now shared with `DebugHistoryExporter`) since it silently deserialized to `(0,0)` otherwise. Unit tested for full round-trip fidelity and both rejection paths (`Tests/RunSaveSerializerTests.cs`).

Remaining: safe file writes (temp file, atomic replace, backup); and an actual load flow in Main that resolves WeaponId (`WeaponDefinitions`) and DefinitionId (`MonsterDefinitions`/mods) back into real definitions, then bypasses `CreateDungeon`/`SpawnEnemies` and builds Player/Enemy nodes from restored data instead. RNG continuation is out of scope per the acceptance note below.

Acceptance: save/load plus the next command matches uninterrupted execution; an opened door and destroyed wall stay changed; terminal status persists; corrupt/incompatible saves do not overwrite a valid run.

Risks: fresh generation overwriting loaded state, missing mod definitions, stale asynchronous writes, reset preparation or RNG, duplicate view nodes. Do not promise exact random continuation until active RNG state is restorable.

## 5. Reproducible encounter setup and mod compatibility

Seed spawning, sort content enumeration, detect duplicate IDs, record definition fingerprints, and introduce an explicit spawn budget so adding definitions does not require one enemy per definition. Resolve the exported-build mod path and validate missing/changed content for resume.

Acceptance: same seed/config/content yields the same terrain, roster/order and positions; overfull rooms are handled; saves report incompatible required content clearly. Add fixed-seed generation and spawn coverage.

Risks: changed RNG consumption, filesystem order, spawn exhaustion, silently altered saved enemy behavior. Implement minimum ID/fingerprint validation in milestone 4 before relying on modded saves.

## 6. Input and presentation integration

Keep the implemented arrow-key menu. Add explicit wait and a clear input policy around animations. Rebuild views safely on load and refresh neighboring terrain art when required.

Acceptance: one input means at most one command; selection does not also move; wait consumes a turn; visual motion cannot affect combat; load/restart have no duplicate nodes.

Risks: input leakage, double turns, stale cell graphics and animations controlling state.

## 7. Floor objectives and transitions

Separate room clear, floor exit and run completion. Define the exit requirement. Include floor identity in saves and history. Previous-floor persistence is only needed if backtracking becomes a feature.

Acceptance: last-enemy death cannot bypass the chosen exit rule; each transition happens once; debug records across floors identify their maps.

Risks: premature victory, losing relevant floor state, incompatible save schema changes.

## 8. Tactical content and one environment experiment

Add one non-linear attack or obstacle-aware enemy with explicit blocking rules. After state/turn capture is stable, experiment with one timed hazard or revisit disabled changing walls in a small test map; keep this separate from enabling it in procedural runs.

Acceptance: intent is readable, navigation uses current terrain, environment changes occur at most once per turn before capture, and timers/RNG survive save/load if activated.

Risks: pattern occlusion, actor trapping, inaccessible routes, snapshotting before hazards finish. See [Gameplay ideas](Turn_Dungeon_Gameplay_Ideas.md) for optional content.

## 9. Difficulty and run rewards

Add a small RunConfig and one useful reward/spending loop. Include configuration and rewards in run snapshots.

Acceptance: modifiers apply once, definitions remain unchanged, and rewards cannot be duplicated by loading.

Risks: double modifiers/rewards and unbalanced content combinations.

## 10. Persistent progression

Save one unlock and one cosmetic purchase first, independently of run save/history.

Acceptance: save/load preserves balances and ownership; defaults are defined; temporary effects do not persist; loading a run does not roll back or duplicate profile rewards.

Risks: currency loss, purchase duplication and run/profile disagreement.

## Change discipline

- Keep documentation aligned with code; mark proposals and disabled experiments explicitly.
- Preserve current rules during architectural refactors; gameplay changes get separate review.
- Run relevant behavior tests for code changes. Documentation changes need link/status checks, not new runtime tests.
- No ECS, global event bus, DI framework, generic effect language or full replay engine.
- Keep full copied snapshots and the existing generator until a concrete requirement justifies more complexity.
