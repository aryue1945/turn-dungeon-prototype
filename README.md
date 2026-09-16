# Turn Dungeon Prototype

A Godot C# turn-based dungeon roguelike inspired by the non-rhythm Bard mode of Crypt of the NecroDancer, with planned long-term progression influenced by Hades.

Reviewed against main `c4a5d2e99236852b8374e8c733f0ac521deeff20` on 2026-09-16 (UTC). Status describes source inspection, not a runtime test.

## Current gameplay

- Move with arrow keys or WASD. A directional action advances the turn, including a blocked move.
- Player action priority is attack, then dig, then movement or wall bump.
- Enemies act sequentially after the player, across the entire floor.
- Choose Basic Sword or Long Sword before starting: Up/Down then Enter/Space, number keys 1/2, or mouse. The first button receives focus.
- The player also carries a Basic Shovel. Digging damages adjacent destructible terrain without moving; destroyed walls become floor.
- Doors are walkable before opening. Player or enemy occupancy sets `IsOpen`; the door graphic disappears.
- Defeat all enemies to win; player death ends the game. Restart (button or the "R" key) starts a fresh run immediately, bypassing the Continue/New Run choice for the run just finished.
- On launch, an unfinished saved run offers Continue (resume exactly where you left off, skipping weapon selection) or New Run (confirms first if it would overwrite that unfinished run); a finished save or no save at all goes straight to New Run.
- ESC opens a pause menu during play (Restart - confirms first since a run is in progress; Quit; Resume). No explicit wait command yet.
- Camera: mouse wheel or +/- to zoom, 0 to reset during gameplay.
- An "Export Debug History" HUD button (or the "X" key) opens a menu to export the last 3, 5 (default) or 10 turns plus the snapshot immediately before them to a JSON file under the user data directory, including per-attack detail, derived per-cell actor ids, and compact weapon/monster summaries, for diagnosing unexpected behavior.

## Dungeon and monsters

Seeded generation builds connected zones from room templates on one shared grid. Start, combat, shop, and exit roles exist; shops and exits do not yet implement their gameplay objectives.

Player and enemies share map-backed terrain queries. Breakable walls have distinct brick art and changed cells can be refreshed.

`MonsterDefinition` describes built-in and modded monsters. JSON mods combine existing movement and attack IDs, with duplicate ids across mods (or against a built-in id) rejected at load time; see [Modding](docs/MODDING.md). `EncounterPlanner` selects a budgeted roster (5 enemies by default, independent of how many definitions exist) and places them deterministically from the run's own seed - the same seed/config/content always produces the same roster, order and positions.

Tree-wall regrowth and growing-wall spreading exist in code and tests but are disabled in normal gameplay: templates do not place them and `Main` does not call `DungeonMap.AdvanceTurn`. Fire, ice, attack status-effect tags, and loot-table IDs have no active effects.

## Development setup

The project declares `Godot.NET.Sdk/4.7.2`, targeting .NET 8 for desktop and .NET 9 for Android. Use a compatible Godot .NET editor.

1. Clone the repository.
2. Open `project.godot` in Godot.
3. Build the C# project.
4. Run `main.tscn`.

```sh
dotnet build "New Game Project.csproj"
dotnet test Tests/TurnDungeon.Tests.csproj
```

The source contains 147 NUnit test methods across generation, weapon attacks, digging, disabled dynamic terrain, monster-mod loading (including duplicate-id rejection), actor state, game state, turn resolution, enemy movement behaviors, game snapshots, debug history, its exporter, map restore, actor restore, run save serialization, the save-file service, definition-registry lookups, encounter planning, and content fingerprinting. Godot input/rendering integration coverage is still missing - Main is a Godot node and untested directly.

## Architecture and next work

NEXT_STEPS milestones 1-5 are done: authoritative actor state and grid combat; complete-turn execution through `TurnResolver`; snapshot capture with a Last 3/5/10-turn debug-history export (per-attack detail, derived per-cell actor ids, identity/content metadata); full save/resume (`GameSnapshotRestore`, `RunSaveEnvelope`/`RunSaveSerializer`, `RunSaveFileService`'s backup/recovery, and Main's Continue/New Run/autosave/pause-menu flow); and reproducible encounter generation (duplicate-id rejection, stable content ordering, a seeded `EncounterPlanner` with an explicit spawn budget, and save-content fingerprint validation via a shared `ContentFingerprinter`). See [Architecture](docs/ARCHITECTURE.md) for the full breakdown.

The active direction now is gameplay content, not more architecture - see [Next steps](docs/NEXT_STEPS.md)'s "Active roadmap": a manual verification pass of the save/resume flow, an explicit Wait command, then a first tactical slice (one weapon, one enemy, one hazard, one designed encounter), a floor-clear objective, and the fuller loop beyond that. Deferred architecture work with no current gameplay requirement (RNG-stream continuation after loading, save schema migration, per-definition fingerprint diagnostics, a generalized hazard framework, and more) lives in that document's "Architecture backlog," implemented only once a concrete requirement needs it.

## Documentation

- [Game design](docs/GAME_DESIGN.md): current rules and planned experience.
- [Architecture](docs/ARCHITECTURE.md): current dependencies and migration.
- [Save and debug history](docs/SAVE_AND_DEBUG_HISTORY.md): snapshot boundaries, data, and acceptance criteria.
- [Next steps](docs/NEXT_STEPS.md): implemented status and ordered work.
- [Modding](docs/MODDING.md): current monster JSON support and limitations.
- [Gameplay ideas](docs/Turn_Dungeon_Gameplay_Ideas.md): exploratory backlog, not implemented scope.

## Current limitations

- The enemy-phase loop, top-of-turn input rejection, and the turn's completion boundary are still Main's own code rather than `TurnResolver`'s - a deliberate choice (moving them would relocate complexity, not reduce it), not an oversight.
- RNG-stream continuation after loading is out of scope: Continue restores committed state, not an in-flight random sequence.
- No persistent progression or difficulty modifiers yet (roadmap item 5).
- Closed-door blocking, floor transitions/objectives, an explicit wait command, and obstacle-aware navigation remain open (roadmap items 2 and 4).
