# Debug Scenario Editor

Planned, not implemented. Written against main `420aadcb515884bc3c5d4ff1622a7abc40ecde8d` on 2026-09-16 (UTC). This is the implementable spec; [Next steps](NEXT_STEPS.md) is the work order, [Gameplay ideas](Turn_Dungeon_Gameplay_Ideas.md) is the exploratory backlog.

## Why this exists

The design direction is emergence-first: the interesting moments are combinations nobody scripted - an enemy charging onto a spike, a pushed enemy colliding with another, a monster triggering a trap meant for the player. Those situations cannot be validated by playing procedurally. They occur rarely, at random, and cannot be reproduced to confirm a fix.

Constructing a situation currently means writing another `Main.StartFixedEncounter` in C#: one hand-built room, hardcoded, single-purpose. That pattern does not survive contact with a dozen mechanics.

Two payoffs:

1. **Scenarios become data, not code.** The save format (`RunSaveEnvelope` -> `RunSaveSerializer` -> `RunSaveFileService`) already captures a complete, restorable game state. A scenario built in the editor is just a save file, so a library of reproducible test situations costs almost no new code.
2. **It compounds.** Every trap, weapon, enemy and NPC added afterward is testable in it on the turn it is written.

Build this before the next wave of content, not after.

## Scope (v1)

- **Mode toggle.** A key toggles editor mode on top of the *live* game state, mid-run or from a fresh room. Turns do not advance while it is open.
- **Cursor.** Mouse hover and arrow keys both move a highlighted cell cursor.
- **Palette.** A list of things that already exist as definitions: every `TerrainKind`, every `MonsterDefinitions.All` entry (plus loaded mods, i.e. Main's `_spawnPool`), and "player start".
- **Place / delete.** Place the selected palette entry at the cursor; delete clears it (terrain reverts to `Floor`; an enemy is removed).
- **Play from here.** Leave editor mode and resume normal turn resolution against the edited state.
- **Scenario save / load.** Write the current state as a save file under its own directory and load it back.

## Non-goals (v1)

Do not build these until a scenario actually needs them:

- Per-entity property editing (health, facing, prepared-move/charge intent)
- Undo/redo, multi-select, copy/paste, fill/rectangle tools
- Resizing the map or editing zones/rooms
- Spike-trap phase or any other mid-cycle state editing (place it and tick to the phase you need)
- Any in-editor scripting or trigger authoring

## Implementation notes

Everything below already exists; the editor is mostly UI over existing primitives.

| Need | Use |
| --- | --- |
| Set a cell's terrain | `DungeonMap.SetTerrain(x, y, TerrainKind)` (`internal`, same assembly - callable from `Scripts/Game`) |
| Repaint one edited cell | `DungeonRenderer.RefreshCell(map, x, y)` |
| Terrain palette entries | `TerrainKind` enum + `TerrainCatalog.Get(kind)` for name/debug symbol |
| Enemy palette entries | `MonsterDefinitions.All`, or Main's `_spawnPool` to include loaded mods |
| Spawn an enemy | `_enemyScene.Instantiate<Enemy>()` -> `Configure(definition, cell, CellToPosition(cell), IsWallAt)` -> `AddChild` -> `_enemies.Add` -> `_gameState.AddEnemy(enemy.State)` (see `Main.SpawnEnemies`) |
| Remove an enemy | `QueueFree()` the node, drop it from `_enemies`, then `_gameState.RemoveDefeatedEnemies()` will not catch a *living* one - remove its `ActorState` explicitly or rebuild `GameState` |
| Move the player | `Player.PlaceAt(cell, CellToPosition(cell))` |
| Cell <-> pixel | `Main.CellToPosition(GridPosition)` (currently `private`); invert it for mouse picking: `floor((mouse - MapOrigin) / TileSize)`, `TileSize = 32` |
| Capture a scenario | `RunSaveEnvelope.Capture(gameState, weapon, tool, enemyDefinitions)` |
| Write / read a scenario | `RunSaveFileService.Save(directory, envelope)` / `Load(directory)` |
| Rebuild from a scenario | `GameSnapshotRestore.RestoreMap` / `RestoreActor`, then the same view-rebuilding `Main.RestoreRun` already does |

### Scenario storage

`RunSaveFileService` takes a **directory** and uses fixed filenames within it (`run_save.json` plus a backup). The zero-change approach is therefore one directory per scenario:

```
user://scenarios/<scenario-name>/run_save.json
```

This keeps scenarios completely isolated from the real save slot in `OS.GetUserDataDir()`. Adding an optional filename parameter to the service is the alternative; prefer the directory approach for v1 since it needs no changes to tested code.

### Autosave must stay suppressed

`Main` already has `_isFixedEncounter`, checked once at the top of `AutosaveCurrentRun` so the debug encounter can never overwrite a real save. Rename it to something honest like `_suppressAutosave` and set it for editor/scenario sessions too. This is a real hazard: without it, poking at a scenario silently destroys the player's run.

### Keep it out of Main

`Main.cs` is ~1900 lines. Put the editor in its own class (`Scripts/Game/ScenarioEditor.cs`, or its own Godot node) that Main forwards input to and exposes the few things it needs (map, enemy list, game state, renderer, cell/pixel conversion). Do not grow Main by another 400 lines.

### Input keys already taken

`1`/`2`/`3` (weapon select), `-`/`=`/`0` (zoom), `X` (export menu), `R` (restart), `Escape` (pause), `F` (fixed encounter), `Space` (wait), arrows + WASD (movement). `F2` is free and conventional for debug tooling; confirm before wiring.

## Suggested PR slices

Each builds, tests, and is independently reviewable, matching the slicing used for the tactical slice.

1. **Cursor and mode toggle.** Editor mode flag, turn suspension, cell cursor driven by mouse and arrow keys, highlight rendering. No editing yet. Testable pure piece: the pixel -> cell conversion.
2. **Terrain palette and placement.** Palette UI, select an entry, place/delete terrain at the cursor, refresh the edited cell.
3. **Actors.** Place/delete enemies from the definition list; move the player start.
4. **Scenario save/load.** Capture to a per-scenario directory, list existing scenarios, load one back and resume play from it.

## Testing

Follow the existing convention: Godot glue in Main/editor UI stays untested; anything pure gets covered.

- Pixel <-> cell conversion round-trips, including negative and out-of-bounds coordinates
- A scenario captured and reloaded produces an identical `GameSnapshot` (terrain, actors, spike phases) - this is mostly already proven by `Tests/SpikeTrapTests.cs` and `Tests/RunSaveSerializerTests.cs`, so it should be a thin addition
- Scenario storage never touches the real save directory

## Acceptance

Building this scenario by hand in the editor, saving it, reloading it and playing it should take under a minute: a room, one spike trap, one Charging Beetle positioned so its charge crosses the trap, and the player holding a War Hammer. That is exactly what `Main.StartFixedEncounter` hardcodes today - once the editor can reproduce it as a saved scenario, that method should be deleted along with its `F` keybinding.
