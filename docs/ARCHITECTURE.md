# Architecture

Status: records the current design and proposed migration.

## Current design

Main creates the dungeon, actors, camera, and UI.
It receives Player.MoveRequested and executes player and enemy actions.

Player and Enemy are CharacterBody2D nodes containing both gameplay
state and visual behavior.

DungeonMap, DungeonGenerator, and ZoneTemplate are engine-independent.

AttackResolver is separated from actor classes through ICombatant,
but combat positions use Godot Vector2 pixel coordinates.

DungeonRenderer creates visuals from map data once.
Enemies still use rendered wall nodes for terrain blocking.

## Main problem

Gameplay has multiple spatial authorities:

- DungeonMap for player walkability.
- Scene wall nodes for enemy blocking.
- Actor node positions for occupancy and attacks.

Terrain changes and animation require one authoritative game state.

## Target responsibilities

- Main: setup, view wiring, restart, and floor transitions.
- GameState: map, actors, turn number, and run status.
- ActorState: identity, grid position, health, facing, and action state.
- TurnResolver: command validation and complete turn execution.
- EnemyBrain: behavior decisions and intent.
- AttackResolver: attack rules and damage.
- DungeonMap: terrain queries and transitions.
- DungeonGenerator: seeded map creation.
- DungeonRenderer: create and refresh cell visuals.
- Actor views: display state and animate results.
- Input/UI controller: translate controls into commands.

Later:
- RunConfig: difficulty and run options.
- ProgressionState: persistent currencies and unlocks.

## Dependency rules

Gameplay rules must not read scene positions, wall nodes, labels,
textures, or Godot deletion status.

Actor positions use GridPosition.
Tile size and pixel conversion belong to presentation.

Gameplay may mutate state during resolution.
Views receive the resulting state and a small TurnResult.

Use direct method calls and returned values.
No global event bus or dependency injection framework is required.

## Turn contract

1. Validate input and run status.
2. Resolve the player action.
3. Apply immediate deaths and completion checks.
4. Execute surviving enemies in stable order.
5. Each enemy reads the state after previous actions.
6. Stop if the player dies.
7. Finalize turn state and return presentation results.

Preserve current behavior during extraction.
Changes to turn costs or victory conditions belong in separate changes.

## Entity lifecycle

Health determines whether an actor is alive.
Dead actors immediately stop blocking and acting as required by rules.

Visual deletion or death animations do not control gameplay lifetime.

## Terrain

DungeonMap owns terrain state and durability.
Both factions query it for movement and attack blocking.

Door opening and destruction return changed cell coordinates.
DungeonRenderer refreshes those cells and affected visual neighbors.

Zone connections describe generated layout.
Actual traversal depends on current cells.

## Combat definitions

Keep weapon definitions separate from attack execution.
Keep preparation state per actor.
Protect shared definition collections from mutation.

Current sword patterns are ordered lines.
Define new occlusion rules before introducing area patterns.

## Determinism

A reproducible run setup needs map generation and spawn randomness
derived from a recorded seed and configuration.

Stable actor order is part of turn behavior.
Long-term compatibility across generator versions is not guaranteed.

## Migration

1. Remove enemy scene-based terrain queries.
2. Introduce authoritative actor grid state.
3. Convert combat to grid coordinates.
4. Extract turn execution and enemy decisions.
5. Add changed-cell rendering.
6. Implement terrain interactions.

Do not combine migration with combat rebalancing.

## Testing

Use NUnit for simulation and generator behavior.
Add a small set of Godot integration checks for input, views, and restart.

Current tests cover generator properties and basic sword attacks.
Complete-turn, enemy behavior, and presentation integration coverage
still need to be added.

## Deferred architecture

No ECS, generic ability framework, room streaming, rollback system,
or universal inventory framework.

Keep classes concrete until an actual second implementation is needed.