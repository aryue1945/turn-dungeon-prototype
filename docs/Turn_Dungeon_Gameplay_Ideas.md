# Turn Dungeon – Gameplay Ideas

## Core Direction

The game uses a grid-based, turn-driven dungeon system:

- One player action advances one turn.
- Enemies act after the player.
- Enemy movement and attacks should be predictable enough for the player to plan around them.
- Weapons use different attack patterns and tactical ranges.
- Doors, destructible walls, and changing terrain are part of combat rather than only decoration.
- The long-term goal is an interconnected dungeon where enemies can move between rooms.

The current combat foundation is intentionally close to the non-rhythm Bard mode of *Crypt of the NecroDancer*, but the game should gradually diverge by adding environmental manipulation, spatial puzzles, progression systems, and more complex dungeon behavior.

---

## Puzzle Influence

Add puzzle-like encounters inspired by games such as *Rust Bucket*.

The game should not become a pure puzzle game. Instead, some rooms or situations should require the player to understand:

- Enemy movement patterns.
- Terrain.
- Timing.
- Switches.
- Doors.
- Hazards.
- Positioning.
- Temporary openings.
- Enemy manipulation.

A room can therefore be solved through combat, movement, or manipulating the environment.

Example:

```text
#########
#..E..X.#
#.#.#.#.#
#...P...#
#.#.#.#.#
#K.....D#
#########

P = Player
E = Enemy
X = Reward
K = Switch
D = Locked door
```

The player may need to lure the enemy away, hit the switch, avoid being trapped, and decide whether the reward is worth the extra risk.

---

## Environmental Hazards as Tactical Tools

Environmental hazards should usually be predictable in turns.

The player should be able to use hazards against enemies instead of treating them only as obstacles.

### Meteor Strikes

Example behavior:

```text
Turn 0: Target tile marked
Turn 1: Warning remains
Turn 2: Meteor impacts
Turn 3: Crater or altered terrain remains
```

Possible effects:

- Damage enemies.
- Damage the player.
- Destroy walls.
- Break objects.
- Open shortcuts.
- Reveal secret areas.
- Create craters or blocked terrain.
- Ignite nearby tiles.
- Trigger other environmental objects.

This creates multiple player choices:

- Dodge the meteor.
- Lure an enemy into the impact tile.
- Intentionally use it to destroy a wall.
- Open a new path between rooms.
- Cause enemies from another room to gain access.

---

## Regenerating Walls

Some walls can respawn after being destroyed.

Example:

```text
██████████
P....█...E
.....█....
██████████
```

The player breaks the wall:

```text
██████████
P........E
..........
██████████
```

After several turns, it regenerates:

```text
██████████
P....█...E
.....█....
██████████
```

Possible uses:

- Open a temporary escape path.
- Trap an enemy on the other side.
- Split a group of enemies.
- Reach a temporary treasure area.
- Force the player to complete an objective before the route closes.
- Temporarily connect two rooms.
- Create timed combat puzzles.

Variants:

- Walls regenerate after a fixed number of turns.
- Walls regenerate only when the player leaves an area.
- Walls regenerate unless a switch is active.
- Some enemies rebuild walls.
- Certain weapons can permanently destroy them.
- Higher difficulty modifiers shorten regeneration time.

---

## Other Environmental Mechanics

Potential mechanics that can use the same turn system:

### Terrain

- Collapsing floors.
- Breakable floors.
- Pits.
- Ice or sliding tiles.
- Conveyor tiles.
- Mud or slow tiles.
- Water.
- Rising water.
- Lava.
- Spreading fire.
- Poison clouds.
- Darkness zones.
- Temporary bridges.

### Traps

- Spike traps with predictable cycles.
- Rotating laser beams.
- Arrow launchers.
- Falling rocks.
- Crushing walls.
- Moving blades.
- Bomb tiles.
- Timed explosions.

### Interactive Objects

- Pressure plates.
- Switches.
- Keys.
- Locked gates.
- Movable blocks.
- Explosive barrels.
- Teleporters.
- One-way gates.
- Mirrors or reflectors.
- Objects that redirect projectiles.
- Objects that enemies can activate.

### Dynamic Dungeon Changes

- Walls that move.
- Rooms that change layout.
- Doors that open or close on a timer.
- Sections that collapse.
- Terrain that is rebuilt.
- Secret passages.
- Temporary connections between rooms.
- Hazards that spread from room to room.

---

## Environment Turn

Environmental systems should use the same central turn-resolution model instead of each feature implementing its own independent timing logic.

Suggested order:

```text
Player Action
    ↓
Player Combat / Interaction
    ↓
Enemy Actions
    ↓
Environment Turn
    ├─ Meteor countdown
    ├─ Wall regeneration
    ├─ Fire spreading
    ├─ Trap cycles
    ├─ Moving terrain
    └─ Door / switch state
    ↓
Resolve deaths / terrain changes
    ↓
Next Player Turn
```

This will make future mechanics easier to combine.

Example:

1. Player pushes an enemy onto a marked meteor tile.
2. Enemy takes its turn.
3. Meteor countdown reaches zero.
4. Meteor kills the enemy.
5. The explosion destroys a nearby regenerating wall.
6. Another enemy now has a path into the room.
7. Three turns later, the wall closes again.

The interesting gameplay comes from systems interacting rather than from creating a unique script for every room.

---

## Interconnected Dungeon Design

The dungeon should eventually behave as one larger space instead of a chain of isolated combat rooms.

Important possibilities:

- Enemies can follow the player between rooms.
- Opening a door can expose the player to enemies elsewhere.
- Destroying walls can create alternate routes.
- Environmental hazards can affect neighboring rooms.
- The player can retreat, reposition, or lure enemies into another room.
- Temporary walls can divide the dungeon dynamically.
- Some puzzle solutions can have consequences outside the current room.

This makes navigation part of combat.

---

## Procedural Puzzle Design

Avoid trying to procedurally generate complex hand-authored puzzles from scratch.

Instead, combine reusable components:

```text
Room Geometry
+ Enemy Pattern
+ Environmental Mechanic
+ Objective
+ Reward
+ Optional Modifier
```

Example:

```text
Small room
+ charging enemy
+ regenerating wall
+ pressure plate
+ treasure chest
+ meteor strikes every 6 turns
```

Another run could reuse the same geometry but replace the systems:

```text
Small room
+ ranged enemy
+ moving wall
+ locked exit
+ health reward
+ spreading fire
```

This provides variation while keeping generation manageable.

---

## Combat Beyond Direct Damage

A longer-term goal can be builds that specialize in manipulating the environment rather than only increasing weapon damage.

Possible upgrades:

### Shovel / Terrain Weapon

- Break walls faster.
- Break multiple tiles.
- Push enemies.
- Create pits.
- Permanently destroy regenerating walls.
- Dig temporary shortcuts.

### Meteor Build

- Shorter meteor countdown.
- Larger impact area.
- Player immune to own meteor damage.
- Meteor leaves fire.
- Meteor creates loot.
- Meteor targets enemies instead of random tiles.

### Trap Build

- Increase trap damage.
- Reverse trap direction.
- Delay traps.
- Trigger traps manually.
- Make enemies activate pressure plates.

### Control Build

- Push enemies farther.
- Pull enemies.
- Freeze enemy turns.
- Swap positions.
- Create temporary walls.
- Lock doors.

This can produce runs where the player's main strength is controlling space rather than simply having a stronger weapon.

---

## Relationship to Existing Inspirations

Current design influences can be separated roughly as:

- **Crypt of the NecroDancer – Bard mode:** grid-based turn combat, predictable enemy patterns, weapon attack shapes.
- **Rust Bucket:** spatial puzzles and enemy manipulation.
- **Hades:** progression, difficulty modifiers, unlocks, currencies, and long-term replayability.
- **Original direction:** interconnected dungeon, destructible/regenerating terrain, environmental combat, and enemies moving between rooms.

The goal is not to remove the familiar turn-based foundation. The goal is to build enough interacting systems on top of it that the resulting game has its own tactical identity.

---

## Near-Term Priority

Do not implement all of these mechanics immediately.

Recommended order:

1. Finish stable room connectivity.
2. Add doors.
3. Add destructible terrain.
4. Allow enemies to navigate between connected rooms.
5. Create a generic environment-turn system.
6. Implement one timed hazard, such as meteor strikes.
7. Implement one changing-terrain mechanic, such as regenerating walls.
8. Build a few hand-authored test rooms combining those systems.
9. Evaluate whether the interactions are fun before expanding the mechanic list.
10. Only then integrate these mechanics into procedural generation.

A small number of interacting systems is more useful than many isolated gimmicks.
