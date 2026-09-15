# Run Saves and Recent-Turn Debug History

Status: agreed direction, not implemented. Reviewed against main `147df77146dd0ad7c53355e9c77da119f29f5b00` on 2026-09-15 (UTC).

## Purpose

1. Close the game and continue the current run.
2. Export the last 10 moves/turns as 2D map snapshots so a person or AI can inspect unexpected behavior.

"Recent maps" means successive states of the current run, not ten previous dungeon floors. Debug retention does not grant player rewind. Persistent profile unlocks/currencies are a later, separate save.

## Source of truth

In-memory C# GameState owns terrain and actors. Rendering projects that state into Godot nodes. Snapshot capture copies it; JSON encodes the copy for disk/export. No gameplay reads pixels or reparses saved JSON to execute a turn.

Debug snapshots describe the intended logical map. They cannot prove what was actually drawn on screen; attach a screenshot too when diagnosing a visual mismatch.

## Shared snapshot data

| Data | Required contents |
| --- | --- |
| Identity | Schema version, game build/commit, run ID, floor ID, completed turn number, run status. |
| Configuration | Generation request/seed, run settings, content/definition fingerprints. |
| Grid | Width/height and row-major `cells[y][x]`; each cell includes terrain kind, durability, IsOpen, zone/connection metadata and actor instance IDs at that cell. |
| Actors | Instance ID, definition ID, faction, position, current/max health, facing, weapon/tool IDs, attack preparation and behavior state. |
| Floor metadata | Rooms/zones, current player zone, objective state when introduced. |
| Timing/randomness | Every active simulation timer and enough RNG state to continue; original seed alone is insufficient after random draws. |

Use explicit DTOs and capture/restore mapping. Encode the 2D grid as nested row arrays for JSON, rather than depending on direct serialization of DungeonCell[,]. Coordinates use x right/y down, with (0,0) at the top left. Validate row sizes and references.

The cell actor-ID list is a derived inspection aid; the actor list owns positions. Validate agreement on load/export, never let both representations mutate independently. Terrain must remain visible under actors in the export. Runtime actor IDs must be unique even for two monsters using the same definition.

Do not serialize nodes, textures, callbacks, scene paths as actor identity, or whole definition object graphs. Existing monster IDs are usable; weapon/tool IDs still need introducing. Record definition fingerprints so changed mod data can be diagnosed.

Private state currently needing explicit capture includes ChasePlayerBehavior._hasPreparedMove. Pending terrain regrowth kind, terrain turn counter and terrain RNG need capture before enabling dynamic terrain. Do not accidentally activate disabled mechanics when loading.

## Debug ring: 10 transitions, up to 11 states

- Capture an initial committed state after run setup/weapon choice and before the first command.
- After each consumed gameplay turn, append its command, ordered outcomes, and independent post-turn snapshot.
- Keep the latest 10 transitions plus the snapshot immediately before the oldest retained transition. For example, after turn 25 retain states 15 through 25 and transitions 16 through 25.
- Earlier in a run, retain only the history available; do not fabricate entries.
- Bumps, digs, preparation turns and eventual wait count; menu/camera input and rejected commands do not.
- Capture terminal victory/death turns even if they skip remaining enemies. Keep history available on the end screen until restart.
- Reset history for a new run. After loading a run, use the loaded state as the initial snapshot; retaining old debug history across sessions is not required initially.
- For future floor transitions, retain floor IDs and self-contained dimensions/cells so the sequence stays interpretable across a transition.

Copy all mutable cells, actor collections and nested intent/timer data. A shallow copy would rewrite history as the live game changes.

Each transition records typed outcomes such as Move, Blocked, Dig, TerrainDestroyed, DoorOpened, Prepare, Attack, Damage, Death and Turn. Include actor/target IDs, positions, amounts and blocking reasons where applicable. Derive readable text from these outcomes; do not treat console text as the only record.

Example: an enemy remaining at (4,6) is explained by `Prepare(direction=left)` rather than mistaken for a failed move. A prepared enemy subsequently moves left even if the player changes direction.

## Export

Provide an Export Debug History action available during play and on the end screen. Write one readable JSON file with metadata, ordered snapshots and transitions, using stable field/order conventions. Include definitions relevant to interpreting custom attacks as a compact diagnostic summary or fingerprinted content description, not executable mod code.

Export must not consume a turn, alter state, or draw random numbers. Keep memory bounded; write history on request initially. Exact replay, screenshot automation and a visual history viewer are optional later work.

An export format is separate from the resume-file envelope even if both reuse snapshot DTOs. Export files are for sharing; save files are validated for restoration.

## Resume lifecycle

Capture at the same stable completed-turn boundary, after all applicable rules and immediate deaths. Also save initial setup and future non-turn choices that modify run state. Loading must bypass fresh generation/spawning and restore the snapshot before building views.

Save the actual terrain and actors, not just the original seed. Preserve prepared directions, durability and door IsOpen. Restore run status; a terminal save must not resume as a living pre-death state.

Prefer a synchronous snapshot copy and serialized single-writer file operation initially. If writes become asynchronous, queue or coalesce by monotonically increasing turn/save sequence so an older write cannot overwrite a newer one.

Write a temporary file in the save directory, complete/close it, then replace the current save using a platform-appropriate safe replacement and keep a known-good backup. Validate before replacing active state on load. Test failure recovery on supported platforms. Do not depend only on an exit callback; with asynchronous writes, an abrupt exit may lose the newest unflushed turn.

Reject unsupported versions, invalid bounds/IDs, unresolved definitions or incompatible content with a useful error. Do not silently drop modded enemies or reset AI preparation. First release can reject incompatible versions; no general migration framework is required.

## Acceptance tests

- Advance live state after capture; old terrain, health, positions and AI intent remain unchanged.
- After 25 turns, retain exactly 10 transitions and their 11 boundary states in order.
- Digs, blocked moves, preparation and terminal turns are present; camera/menu input is absent.
- Export/import JSON preserves dimensions, row orientation, actor IDs and door/durability state.
- Save/load followed by the same next command matches uninterrupted play, including a prepared chaser and prepared attack.
- Disabled terrain remains disabled; active timers/RNG round-trip before their mechanics are enabled.
- Loading restores current terrain instead of regenerating destroyed walls.
- Missing mods, duplicate IDs, incompatible versions and corrupt saves fail clearly without replacing a valid run.
- Interrupted writes retain a valid current or backup save; asynchronous completion order cannot roll state backward.
- Fresh rendering from loaded state does not duplicate actors or terrain nodes.
