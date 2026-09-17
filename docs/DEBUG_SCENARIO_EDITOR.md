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

Sandbox has two states:

- **Edit** - cursor, palette, place/delete. Turns never advance. No enemy acts, no environment phase runs.
- **Play** - normal turn resolution against the edited state. No editing.

**Superseded by an explicit follow-up request during implementation:** rather than F-key toggles, Sandbox is reached via a **Debug** button on the startup screen (alongside Continue/New Run), and inside it a **Run** / **Reset** / **Exit** button toolbar drives the state machine - Edit shows Run, Play shows Reset, Exit is always visible. The only keyboard shortcuts left are **Esc** (in Play, drops back to Edit; in Edit, closes the placement menu if one is open; it never exits Sandbox on its own) and the **arrow keys** (cursor movement in Edit). Placement itself is a left-click on a cell, which opens a menu instead of using a palette panel plus separate place/delete keys - see Scope below.

Entering Play (Run) snapshots the edited scenario. **Reset** restores that snapshot, so the same situation can be replayed immediately after watching it go wrong. Going back to Edit and changing something takes a fresh snapshot on the next Run.

### Isolation guarantees

Sandbox must never overwrite or advance the normal run. Three rules, in order of how much they are worth enforcing structurally:

1. **Storage is separate.** Sandbox reads and writes only under a scenarios directory (below). It never writes the real save slot in `OS.GetUserDataDir()`.
2. **Autosave never fires.** `Main` already has `_isFixedEncounter`, checked once at the top of `AutosaveCurrentRun`. Rename it to `_suppressAutosave` and set it for the entire Sandbox session - set once on entry, never cleared. A per-state flag that has to be toggled correctly at every Edit/Play transition is exactly the kind of thing that eventually gets it wrong.
3. **Leaving Sandbox reloads the scene.** Returning to the startup screen via `GetTree().ReloadCurrentScene()` (the same thing `OnRestartPressed` does) guarantees nothing from a Sandbox session survives into a normal run. Carry the "go to startup" intent across the reload with a static flag, as `_skipStartupMenuForNewRun` already does.

## Scope (v1)

- **Enter Sandbox** via the "Debug" button on the startup screen, into Edit state, with a blank room.
- **Cursor.** Mouse hover, WASD, and arrow keys move a cell-aligned highlighted cursor. Enter or Space opens the placement menu for the highlighted cell.
- **Keyboard menus.** Placement and facing menus focus their first option when opened; arrow keys move through options, Enter/Space selects, and Escape closes the menu.
- **Palette and placement.** Left-clicking a cell opens a menu listing every `TerrainKind`, every `MonsterDefinitions.All` entry (plus loaded mods, i.e. Main's `_spawnPool`), "player start", and Delete. Choosing an object (other than Delete) then asks for a facing to place it with; Delete applies immediately. Delete clears the cell (terrain reverts to `Floor`; a living enemy there is removed from both `_enemies` and `GameState.Enemies`).
- **Run** (button) enters Play, snapshotting the scenario.
- **Restart** (button, shown in Play) restores the latest setup snapshot taken when Run was pressed, leaving Sandbox ready to replay it.
- **Reset** (button) discards all edits and play state, restores the blank room from Sandbox entry, and returns to Edit.
- **Scenario save / load.** Write the current scenario to its own directory and load it back.
- **Exit** (button) returns to the startup screen.

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
| Snapshot for Restart/Reset | `GameSnapshot.Capture(gameState)` |
| Restore for Restart/Reset | `GameSnapshotRestore.RestoreMap` / `RestoreActor`, then rebuild views the way `Main.RestoreRun` already does |
| Capture a scenario to disk | `RunSaveEnvelope.Capture(gameState, weapon, tool, enemyDefinitions)` |
| Write / read a scenario | `RunSaveFileService.Save(directory, envelope)` / `Load(directory)` |

### Scenario storage

`RunSaveFileService` takes a **directory** and uses fixed filenames within it (`run_save.json` plus a backup). The zero-change approach is therefore one directory per scenario:

```
user://scenarios/<scenario-name>/run_save.json
```

This keeps scenarios completely isolated from the real save slot. Adding an optional filename parameter to the service is the alternative; prefer the directory approach for v1 since it needs no changes to tested code.

### Play snapshot, Restart and Reset

Take the Restart snapshot when Edit -> Play happens, not on every turn. Capture a separate initial snapshot once when Sandbox opens for Reset. Two options, and the in-memory one is simpler:

- **In memory (preferred).** Hold the `GameSnapshot` plus the parallel list of `MonsterDefinition`s the editor placed. Restart rebuilds from those directly; Reset rebuilds from the initial blank snapshot - no serialization, no id-to-definition lookup, no content fingerprint comparison, because the definitions are already in hand.
- **Through the save format.** Correct but does strictly more work: `RunSaveEnvelope.Capture` -> restore -> resolve every `DefinitionId` back to a definition, exactly as Continue does.

Use the in-memory path for Reset and the save format only for scenarios written to disk.

### Two things that will bite

- **Hand-built rooms have no zones.** `DungeonMap.Zones` is populated by `DungeonGenerator`, and `GameSnapshot` does not capture it, so `GetZone(id)` returns null for any sandbox or restored map. `Main.OnPlayerEnteredZone` already guards for this; anything new that reads `Zones` must too.
- **A living enemy is not removed by `RemoveDefeatedEnemies`.** Deleting an enemy in Edit has to drop it from both `_enemies` and `GameState.Enemies` explicitly, or it keeps taking turns in Play as an invisible actor.

### Keep it out of Main

`Main.cs` is ~1900 lines. Put Sandbox in its own class (`Scripts/Game/ScenarioSandbox.cs`, or its own Godot node) that Main enters and forwards input to, exposing the few things it needs (map, enemy list, game state, renderer, cell/pixel conversion). Do not grow Main by another 400 lines.

### Input keys already taken

`1`/`2`/`3` (weapon select), `-`/`=`/`0` (zoom), `X` (export menu), `R` (restart), `Escape` (pause, or Sandbox's Play->Edit/close-menu), `F` (fixed encounter, to be removed), `Space` (wait), arrows + WASD (movement). Sandbox introduces no new keybindings beyond reusing the arrow keys as its Edit-state cursor and Escape as above - entry (Debug button), Run/Restart/Reset/Exit and placement are all mouse-driven UI, per an explicit follow-up request to keep Sandbox's controls out of hidden function keys.

## Suggested PR slices

Each builds, tests, and is independently reviewable, matching the slicing used for the tactical slice.

1. **Sandbox mode with Edit/Play states.** Startup-screen entry (a "Debug" button), the two-state machine driven by a Run/Restart/Reset/Exit button toolbar, the Play snapshot and Restart, the initial snapshot and Reset, the isolation guarantees (suppressed autosave, reload on exit), and a cell cursor driven by mouse and arrow keys. No editing yet - a blank room proves the mode, the states and the isolation before anything can mutate. Testable pure piece: pixel -> cell conversion.
2. **Terrain palette and placement, plus actors.** Delivered together as one click-to-place menu rather than a separate palette panel: left-click a cell to choose an object (every `TerrainKind`, "Player Start", every `MonsterDefinitions.All` entry, or Delete) and then a facing, applied on confirm; refresh the edited cell. Deleting or overwriting an occupied cell drops any living enemy there from both `_enemies` and `GameState.Enemies` explicitly.
3. **Scenario save/load.** Capture to a per-scenario directory, list existing scenarios, load one back into Edit.

## Testing

Follow the existing convention: Godot glue in Main/Sandbox UI stays untested; anything pure gets covered.

- Pixel <-> cell conversion round-trips, including negative and out-of-bounds coordinates
- A scenario captured and reloaded produces an identical `GameSnapshot` (terrain, actors, spike phases) - mostly already proven by `Tests/SpikeTrapTests.cs` and `Tests/RunSaveSerializerTests.cs`, so it should be a thin addition
- Restart restores the snapshot exactly: same terrain, actor positions, health and spike phases as when Play began
- Reset restores the blank Sandbox-entry state and returns to Edit
- Scenario storage never resolves to the real save directory

## Acceptance

Building this scenario in Sandbox, playing it, restarting it and saving it should take under a minute: a room, one spike trap, one Charging Beetle positioned so its charge crosses the trap, and the player holding a War Hammer. That is exactly what `Main.StartFixedEncounter` hardcodes today - once Sandbox can reproduce it as a saved scenario, that method should be deleted along with its `F` keybinding.

A normal run saved before entering Sandbox must be byte-identical afterward, however much was edited or played in between.
