# Game Design

Reviewed against main `147df77146dd0ad7c53355e9c77da119f29f5b00` on 2026-09-15 (UTC). Implemented status is based on source inspection. Planned features are labeled below.

## Core experience

A grid-based dungeon roguelike without rhythm timing.

The player can consider a move before acting. Each committed gameplay
action advances the simulation. Enemy patterns and weapon geometry
create the tactical decisions.

## Dungeon structure

Each floor is one shared grid containing connected rooms and passages.

Rooms control initial content placement and encounter identity.
They do not inherently restrict actor movement.

Current generation uses connected, uniform room templates.
Variable room sizes and more layout variety are later improvements.

## Turn rules

Current behavior to preserve during refactoring:

- Directional input tries attack, then adjacent digging, then movement or a wall bump.
- A blocked movement attempt consumes a turn.
- Enemies act sequentially after the player.
- Defeated enemies do not act.
- Enemy execution stops when the player dies.
- Camera and menu actions consume no turns.

Proposed addition:

- An explicit wait command consumes one turn without moving.

## Weapons

Weapons define attack geometry and tuning separately from resolution.

Current weapons:

- Basic Sword: one cell forward.
- Long Sword: up to two cells forward, hitting the nearest valid target.
- Both currently deal one damage and stop at walls.

The player has a separate Basic Shovel with terrain damage 1. Weapon selection uses Up/Down and Enter/Space, number keys 1/2, or mouse; the first button receives focus. The two swords and shovel are independent equipment definitions.

Add one weapon at a time with a distinct tactical use.
Define blocking and target priority when introducing non-linear patterns.

## Enemies

Monsters are data definitions with stable IDs. Built-in and JSON-modded monsters share the same actor scene and registered movement behaviors; see [Modding](MODDING.md). Only the first configured attack currently executes.

Preserve the current five behaviors during architecture changes:

- Slow chaser: prepare a direction, then act in that direction next turn.
- Patroller: move forward; turn right when blocked.
- Left turner: move then turn left; an attack replaces that sequence.
- Right turner: move then turn right; an attack replaces that sequence.
- Stationary: no action.

Enemies currently act across the entire floor.
Any later activation or awareness rules must be explicit.

## Terrain

Current:

- Solid walls block movement.
- Breakable walls have durability, render with a distinct brick
  texture, and can be dug to destruction (becoming floor).
- Doors have an IsOpen flag and disappear once the player or an enemy occupies them. They are walkable and do not block attacks even before opening; IsOpen currently controls the graphic.
- Fire and ice are reserved definitions without implemented interactions.

Disabled for now (implemented but not spawned or ticked, see
ARCHITECTURE.md): tree walls that regrow a few turns after being
destroyed, and growing walls that slowly spread into neighboring
floor cells. The concept is worth revisiting; the current shape just
isn't right yet.

Behavior to preserve:

- Digging damages adjacent destructible terrain, consumes a turn, and does not move the player.
- Destroyed ordinary breakable walls become floor.
- Enemies can traverse doors and destroyed walls; they do not dig.
- Both player and enemy occupancy opens a door visually in the same turn.

Blocking/locked doors and enemy permissions are deferred gameplay decisions. Do not add blocked-door behavior as part of state extraction.

## Run completion

Current prototype: defeating all enemies wins.

Planned: separate room clearing, floor exit, and run completion.
The exit should become an explicit gameplay objective.
Its unlock condition will be decided when floor transitions are built.

## Resume and debugging (planned)

Players should be able to close the game and continue the current run. Save a complete state at stable turn boundaries, including enemy intent and changed terrain, with a backup for recovery.

Separately, retain the last 10 completed gameplay turns plus their starting state as independent 2D-grid snapshots. An Export Debug History action should produce one JSON file with the sequence, commands, actor health/facing/intent, and ordered outcomes for human or AI inspection.

This is diagnostic history, not ten prior floors and not a player rewind mechanic. It must remain available after death or victory until restart. See [Save and debug history](SAVE_AND_DEBUG_HISTORY.md). Neither feature is implemented yet.

## Progression

Add progression after the floor combat loop is playable.

Planned categories:

- Run difficulty modifiers.
- Weapon and content unlocks.
- Temporary run rewards.
- Persistent currency.
- Cosmetic purchases.

Keep temporary run state separate from persistent ownership.
Do not define a large economy before one reward and purchase loop works.

## Presentation

Show enemy intent, attack reach, damage, and terrain differences clearly.
Animations display resolved gameplay and must not determine legality.

The current prison art establishes the setting.
Narrative details remain open.

## Deferred decisions

- Final weapon roster and additional equipment slots beyond weapon plus digging tool.
- Enemy activation or awareness outside the player's room.
- Exit requirements.
- Permanent stat upgrades versus content unlocks.
- Detailed currency and difficulty balance.
