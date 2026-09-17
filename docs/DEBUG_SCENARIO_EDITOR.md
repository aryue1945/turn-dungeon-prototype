# Debug Scenario Editor

Planned, not implemented. Written against main `419c146cb469222ea9ff4f603b846eb7fa5d8039` on 2026-09-16 (UTC). This is the implementable spec; [Next steps](NEXT_STEPS.md) is the work order, [Gameplay ideas](Turn_Dungeon_Gameplay_Ideas.md) is the exploratory backlog.

## Why this exists

The design direction is emergence-first: the interesting moments are combinations nobody scripted - an enemy charging onto a spike, a pushed enemy colliding with another, a monster triggering a trap meant for the player. Those situations cannot be validated by playing procedurally. They occur rarely, at random, and cannot be reproduced to confirm a fix.

Constructing a situation currently means writing another `Main.StartFixedEncounter` in C#: one hand-built room, hardcoded, single-purpose. That pattern does not survive contact with a dozen mechanics.

Two payoffs:

1. **Scenarios become data, not code.** The save format (`RunSaveEnvelope` -> `RunSaveSerializer` -> `RunSaveFileService`) already captures a complete, restorable game state. A scenario built in the editor is just a save file, so a library of reproducible test situations costs almost no new code.
2. **It compounds.** Every trap, weapon, enemy and NPC added afterward is testable in it on the turn it is written.

Build this before the next wave of content, not after.

## Sandbox mode

The editor is **its own mode**, not a toggle layered over an active run. Sandbox is entered from the startup screen (the same place the temporary `F` fixed-encounter launcher lives today) and replaces it.

This separation is the design, not an implementation detail. A toggle over the live run can leak: a stray edit mutates a real run, an autosave fires mid-edit, a half-edited state gets committed as the player's save. A distinct mode makes those failures structurally impossible rather than guarded against.

Sandbox has two states, toggled with **F2**:

- **Edit** - cursor, palette, place/delete. Turns never advance. No enemy acts, no environment phase runs.
- **Play** - normal turn resolution against the edited state. No editing.

Entering Play snapshots the edited scenario. **Reset** restores that snapshot, so the same situation can be replayed immediately after watching it go wrong. Toggling back to Edit and changing something takes a fresh snapshot on the next Play.

### Isolation guarantees

Sandbox must never overwrite or advance the normal run. Three rules, in order of how much they are worth enforcing structurally:

1. **Storage is separate.** Sandbox reads and writes only under a scenarios directory (below). It never writes the real save slot in `OS.GetUserDataDir()`.
2. **Autosave never fires.** `Main` already has `_isFixedEncounter`, checked once at the top of `AutosaveCurrentRun`. Rename it to `_suppressAutosave` and set it for the entire Sandbox session - set once on entry, never cleared. A per-state flag that has to be toggled correctly at every Edit/Play transition is exactly the kind of thing that eventually gets it wrong.
3. **Leaving Sandbox reloads the scene.** Returning to the startup screen via `GetTree().ReloadCurrentScene()` (the same thing `OnRestartPressed` does) guarantees nothing from a Sandbox session survives into a normal run. Carry the "go to startup" intent across the reload with a static flag, as `_skipStartupMenuForNewRun` already does.

## Scope (v1)

- **Enter Sandbox** from the startup screen, into Edit state, with a blank room.
- **Cursor.** Mouse hover and arrow keys both move a highlighted cell cursor.
- **Palette.** A list of things that already exist as definitions: every `TerrainKind`, every `MonsterDefinitions.All` entry (plus loaded mods, i.e. Main's `_spawnPool`), and "player start".
- **Place / delete.** Place the selected palette entry at the cursor; delete clears it (terrain reverts to `Floor`; an enemy is removed).
- **F2 toggles Edit and Play.** Entering Play snapshots the scenario.
- **Reset** restores the snapshot taken when Play began, leaving Sandbox ready to replay it.
- **Scenario save / load.** Write the current scenario to its own directory and load it back.
- **Exit** to the startup screen.

## Non-goals (v1)

Do not build these until a scenario actually needs them:

- **Importing the current normal run's state into Sandbox.** Deliberately deferred - it is the feature most likely to reintroduce the leak the mode separation exists to prevent, and it needs the id-to-definition resolution and fingerprint handling `Main.TryResolveSaveDefinitions` does. Add it later, as an explicit one-way copy into Sandbox storage.
- Per-entity property editing (health, facing, prepared-move/charge intent)
- Undo/redo, multi-select, copy/paste, fill/rectangle tools
- Resizing the map or editing zones/rooms
- Spike-trap phase or other mid-cycle state editing (place it and tick to the phase you need)
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
| Remove an enemy | `QueueFree()` the node and drop it from `_enemies`; `GameState.RemoveDefeatedEnemies` only drops *dead* actors, so remove the living one's `ActorState` explicitly |
| Move the player | `Player.PlaceAt(cell, CellToPosition(cell))` |
| Cell <-> pixel | `Main.CellToPosition(GridPosition)` (currently `private`); invert it for mouse picking: `floor((mouse - MapOrigin) / TileSize)`, `TileSize = 32` |
| Snapshot for Reset | `GameSnapshot.Capture(gameState)` |
| Restore for Reset | `GameSnapshotRestore.RestoreMap` / `RestoreActor`, then rebuild views the way `Main.RestoreRun` already does |
| Capture a scenario to disk | `RunSaveEnvelope.Capture(gameState, weapon, tool, enemyDefinitions)` |
| Write / read a scenario | `RunSaveFileService.Save(directory, envelope)` / `Load(directory)` |

### Scenario storage

`RunSaveFileService` takes a **directory** and uses fixed filenames within it (`run_save.json` plus a backup). The zero-change approach is therefore one directory per scenario:

```
user://scenarios/<scenario-name>/run_save.json
```

This keeps scenarios completely isolated from the real save slot. Adding an optional filename parameter to the service is the alternative; prefer the directory approach for v1 since it needs no changes to tested code.

### Play snapshot and Reset

Take the snapshot when Edit -> Play happens, not on every turn. Two options, and the in-memory one is simpler:

- **In memory (preferred).** Hold the `GameSnapshot` plus the parallel list of `MonsterDefinition`s the editor placed. Reset rebuilds from those directly - no serialization, no id-to-definition lookup, no content fingerprint comparison, because the definitions are already in hand.
- **Through the save format.** Correct but does strictly more work: `RunSaveEnvelope.Capture` -> restore -> resolve every `DefinitionId` back to a definition, exactly as Continue does.

Use the in-memory path for Reset and the save format only for scenarios written to disk.

### Two things that will bite

- **Hand-built rooms have no zones.** `DungeonMap.Zones` is populated by `DungeonGenerator`, and `GameSnapshot` does not capture it, so `GetZone(id)` returns null for any sandbox or restored map. `Main.OnPlayerEnteredZone` already guards for this; anything new that reads `Zones` must too.
- **A living enemy is not removed by `RemoveDefeatedEnemies`.** Deleting an enemy in Edit has to drop it from both `_enemies` and `GameState.Enemies` explicitly, or it keeps taking turns in Play as an invisible actor.

### Keep it out of Main

`Main.cs` is ~1900 lines. Put Sandbox in its own class (`Scripts/Game/ScenarioSandbox.cs`, or its own Godot node) that Main enters and forwards input to, exposing the few things it needs (map, enemy list, game state, renderer, cell/pixel conversion). Do not grow Main by another 400 lines.

### Input keys already taken

`1`/`2`/`3` (weapon select), `-`/`=`/`0` (zoom), `X` (export menu), `R` (restart), `Escape` (pause), `F` (fixed encounter, to be removed), `Space` (wait), arrows + WASD (movement). `F2` is the Edit/Play toggle; the remaining function keys are free for palette or Reset bindings.

## Suggested PR slices

Each builds, tests, and is independently reviewable, matching the slicing used for the tactical slice.

1. **Sandbox mode with Edit/Play states.** Startup-screen entry, the two-state machine with the F2 toggle, the Play snapshot and Reset, the isolation guarantees (suppressed autosave, reload on exit), and a cell cursor driven by mouse and arrow keys. No editing yet - a blank room proves the mode, the states and the isolation before anything can mutate. Testable pure piece: pixel -> cell conversion.
2. **Terrain palette and placement.** Palette UI, select an entry, place/delete terrain at the cursor, refresh the edited cell.
3. **Actors.** Place/delete enemies from the definition list; move the player start.
4. **Scenario save/load.** Capture to a per-scenario directory, list existing scenarios, load one back into Edit.

## Testing

Follow the existing convention: Godot glue in Main/Sandbox UI stays untested; anything pure gets covered.

- Pixel <-> cell conversion round-trips, including negative and out-of-bounds coordinates
- A scenario captured and reloaded produces an identical `GameSnapshot` (terrain, actors, spike phases) - mostly already proven by `Tests/SpikeTrapTests.cs` and `Tests/RunSaveSerializerTests.cs`, so it should be a thin addition
- Reset restores the snapshot exactly: same terrain, actor positions, health and spike phases as when Play began
- Scenario storage never resolves to the real save directory

## Acceptance

Building this scenario in Sandbox, playing it, resetting it and saving it should take under a minute: a room, one spike trap, one Charging Beetle positioned so its charge crosses the trap, and the player holding a War Hammer. That is exactly what `Main.StartFixedEncounter` hardcodes today - once Sandbox can reproduce it as a saved scenario, that method should be deleted along with its `F` keybinding.

A normal run saved before entering Sandbox must be byte-identical afterward, however much was edited or played in between.
