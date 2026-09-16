# Turn Dungeon Prototype

A Godot C# turn-based dungeon roguelike inspired by the non-rhythm Bard mode of Crypt of the NecroDancer, with planned long-term progression influenced by Hades.

Reviewed against main `147df77146dd0ad7c53355e9c77da119f29f5b00` on 2026-09-15 (UTC). Status describes source inspection, not a runtime test.

## Current gameplay

- Move with arrow keys or WASD. A directional action advances the turn, including a blocked move.
- Player action priority is attack, then dig, then movement or wall bump.
- Enemies act sequentially after the player, across the entire floor.
- Choose Basic Sword or Long Sword before starting: Up/Down then Enter/Space, number keys 1/2, or mouse. The first button receives focus.
- The player also carries a Basic Shovel. Digging damages adjacent destructible terrain without moving; destroyed walls become floor.
- Doors are walkable before opening. Player or enemy occupancy sets `IsOpen`; the door graphic disappears.
- Defeat all enemies to win; player death ends the game. Restart creates a new setup.
- Camera: mouse wheel or +/- to zoom, 0 to reset during gameplay. No explicit wait command yet.

## Dungeon and monsters

Seeded generation builds connected zones from room templates on one shared grid. Start, combat, shop, and exit roles exist; shops and exits do not yet implement their gameplay objectives.

Player and enemies share map-backed terrain queries. Breakable walls have distinct brick art and changed cells can be refreshed.

`MonsterDefinition` describes built-in and modded monsters. JSON mods combine existing movement and attack IDs; see [Modding](docs/MODDING.md). The current spawner places one actor per loaded definition, rather than selecting a budgeted encounter.

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

The source contains 47 NUnit test methods across generation, weapon attacks, digging, disabled dynamic terrain, monster-mod loading, actor state, and game state. Full-turn and Godot input/rendering integration coverage are still missing.

## Architecture and next work

Terrain authority is already unified. Both the player's and enemies' unique instance id, definition id, grid position, health, and facing now live in an authoritative `ActorState` (including the slow chaser's prepared-move flag, previously a private field), and combat/occupancy (attacks, wall checks, spawn placement) run on grid coordinates rather than pixels. Weapons and the digging tool also have stable ids now. A `GameState` aggregate (map, player/enemy state references, turn number, run status) exists and Main keeps it in sync, though Main still makes every decision itself. Attack preparation and equipment references still live only on the Godot nodes/AttackState. Next: fold those into ActorState too, then extract a complete-turn resolver that actually reads and drives GameState, while preserving existing behavior and mod IDs.

Approved direction to implement after that foundation:

- Render from in-memory C# game state; JSON is a serialized representation, not the live simulation.
- Save/resume the current run at completed-turn boundaries, with a backup.
- Keep the last 10 completed turns plus their starting snapshot as independent 2D-grid debug snapshots; export the sequence with commands, actor state, and ordered action outcomes for inspection by a person or AI.

Save/resume and debug history are **not implemented**. Debug history is not a previous-floor archive or a player rewind feature.

## Documentation

- [Game design](docs/GAME_DESIGN.md): current rules and planned experience.
- [Architecture](docs/ARCHITECTURE.md): current dependencies and migration.
- [Save and debug history](docs/SAVE_AND_DEBUG_HISTORY.md): snapshot boundaries, data, and acceptance criteria.
- [Next steps](docs/NEXT_STEPS.md): implemented status and ordered work.
- [Modding](docs/MODDING.md): current monster JSON support and limitations.
- [Gameplay ideas](docs/Turn_Dungeon_Gameplay_Ideas.md): exploratory backlog, not implemented scope.

## Current limitations

- Attack preparation and equipment (weapon/tool) references remain node/AttackState-only, not yet referenced from ActorState. `GameState` exists and is kept in sync, but nothing reads from it yet - Main still owns every decision; no TurnResolver.
- A map seed does not reproduce enemy placement or guarantee the same mod roster/order.
- No run save/load, debug-history export, persistent progression, or difficulty modifiers.
- Closed-door blocking, floor transitions, wait, and obstacle-aware navigation remain open.
