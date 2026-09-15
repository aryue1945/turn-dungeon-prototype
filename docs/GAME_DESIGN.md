# Game Design

Status: proposed direction. Unimplemented rules are identified below.

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

- Directional input attempts an attack before movement.
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

Add one weapon at a time with a distinct tactical use.
Define blocking and target priority when introducing non-linear patterns.

## Enemies

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
- Tree walls are destructible like breakable walls, but the cell
  regrows the tree three turns after destruction, unless an actor is
  standing on it when the timer expires (regrowth then waits until
  the cell is clear).
- Growing walls are destructible, and roughly every four turns one
  existing growing-wall cell spreads into one adjacent floor cell,
  never onto an occupied one. Cutting them back only slows the
  spread; it does not stop it while any growing-wall cell survives.
- Doors are walkable and disappear once an actor steps onto them, but
  have no closed state that blocks movement beforehand.
- Fire and ice are reserved definitions without implemented interactions.

Proposed first implementation:

- Entering a closed door opens it and moves into its cell in one action.
- An open door displays as a passage.
- Digging damages an adjacent breakable wall and consumes a turn.
- Digging does not move the player.
- A destroyed wall becomes floor.
- Enemies can traverse open doors and destroyed walls.
- Enemy door-opening and digging abilities are not included initially.

## Run completion

Current prototype: defeating all enemies wins.

Planned: separate room clearing, floor exit, and run completion.
The exit should become an explicit gameplay objective.
Its unlock condition will be decided when floor transitions are built.

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

- Final weapon roster and equipment slots.
- Enemy activation or awareness outside the player's room.
- Exit requirements.
- Permanent stat upgrades versus content unlocks.
- Detailed currency and difficulty balance.