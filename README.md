# Turn Dungeon Prototype

A Godot C# turn-based dungeon roguelike prototype.

The main inspiration is the non-rhythm Bard mode of Crypt of the
NecroDancer. Planned long-term progression includes difficulty
modifiers, unlocks, currencies, and cosmetic spending.

## Current gameplay

- Move with arrow keys or WASD.
- A directional action advances the turn, including a blocked move.
- Enemies act sequentially after the player.
- Choose Basic Sword or Long Sword before starting.
- Attacks trigger when a valid target is detected in the chosen direction.
- Defeat all enemies to win the current prototype.
- Player death ends the game.
- Restart creates a new setup.

Camera controls:
- Mouse wheel or +/-: zoom.
- 0: reset zoom.

Weapon selection currently uses buttons. Explicit keyboard focus
and navigation remain to be completed.

## Dungeon implementation

The game already generates a seeded, connected dungeon using room
templates on a shared grid.

Zones have start, combat, shop, and exit roles. Shop and exit roles
currently provide metadata rather than complete gameplay.

Doors are currently walkable and retain their graphics.
Breakable walls have durability in map data, but gameplay does not
yet invoke destruction.

## Development setup

The current project declares Godot.NET.Sdk 4.7.2 and targets .NET 8
for desktop. The Android target is .NET 9.

Use the Godot .NET editor compatible with the project configuration.

1. Clone the repository.
2. Open project.godot in Godot.
3. Build the C# project.
4. Run main.tscn.

Build from a terminal:

    dotnet build "New Game Project.csproj"

Run the NUnit tests:

    dotnet test Tests/TurnDungeon.Tests.csproj

## Architecture direction

DungeonMap is the intended authority for terrain.
Actor game state will own integer grid positions.
Godot nodes will display that state.
TurnResolver will own player and enemy turn execution.

The migration is proposed and is not yet complete.

## Documentation

- [Game design](docs/GAME_DESIGN.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Next steps](docs/NEXT_STEPS.md)

## Current limitations

- Enemy terrain checks still read scene wall nodes.
- Actor scene positions are also gameplay positions.
- Complete turns and enemy behaviors lack automated coverage.
- A map seed does not reproduce enemy placement.
- No persistent progression or difficulty modifiers.