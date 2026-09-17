# Game Design

Reviewed against main `420aadcb515884bc3c5d4ff1622a7abc40ecde8d` on 2026-09-16 (UTC). Implemented status is based on source inspection. Planned features are labeled below.

## Core experience

A grid-based dungeon roguelike without rhythm timing.

The player can consider a move before acting. Each committed gameplay
action advances the simulation. Enemy patterns and weapon geometry
create the tactical decisions.

## Design direction

The mechanical reference point is the non-rhythm Bard mode of *Crypt of the NecroDancer*. These are the pillars the roadmap builds toward; [Next steps](NEXT_STEPS.md) sequences them, [Gameplay ideas](Turn_Dungeon_Gameplay_Ideas.md) explores them further.

**Positioning is the game.** Movement, attacking and terrain are one system. Weapons differ by *which cells they hit*, not by damage numbers - a spear, dagger, whip and sword are distinct because their attack geometry is. Enemies are individually simple and readable (moves every other turn; charges when aligned; attacks along a line; vulnerable only from behind) so the player learns rules rather than reacting fast.

**Wasted turns should cost something.** A momentum/multiplier counter rises with continuous effective play and breaks on a wasted action, then feeds the economy. This discourages bumping walls or waiting purely to manipulate enemy timing, without prohibiting cautious play. Momentum may later influence drops, abilities or weapon effects, not just money.

**The same rules apply to everyone.** Traps, terrain and forced movement affect enemies exactly as they affect the player: push an enemy onto spikes and it takes the spikes. This is already true for the Spike Trap and War Hammer knockback and must stay true as hazards are added.

**Systemic, not scripted.** The target shape is generic - an attack or effect names affected cells, entities on those cells receive it, and each entity reacts by its own rules. That single pipeline is what allows a monster to attack a merchant, a merchant to fight back, an enemy to trigger a trap, or a player to push one enemy into another, without any of those outcomes being written individually. Shopkeepers and other NPCs participate in the simulation (damageable, reactive, able to flee and leave merchandise behind) rather than existing as invulnerable UI.

**Extract that pipeline late, not early.** Mechanics are implemented directly first; the shared lifecycle is extracted once several real cases demonstrate it. See the extraction checkpoint in [Next steps](NEXT_STEPS.md) and "Generalized effect or hazard frameworks" in its architecture backlog.

**Structure stays conventional.** Procedural stage -> encounters -> money and items -> shop -> stronger build -> boss -> next stage -> death or restart, eventually around three stages with substages.

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
- An explicit wait command (Space) consumes one turn without moving, attacking or digging.
- Spike traps tick and damage in an environment phase after enemies act, before the turn's snapshot/autosave.

## Weapons

Weapons define attack geometry and tuning separately from resolution.

Current weapons:

- Basic Sword: one cell forward.
- Long Sword: up to two cells forward, hitting the nearest valid target.
- War Hammer: one cell forward, and pushes a surviving target one cell back when that destination is walkable and unoccupied. A blocked push still deals damage.
- All three currently deal one damage and stop at walls.

The player has a separate Basic Shovel with terrain damage 1. Weapon selection uses Up/Down and Enter/Space, number keys 1/2/3, or mouse; the first button receives focus. Each shows a pixel-art icon sized to its reach (`Art/Weapons/`). The weapons and shovel are independent equipment definitions.

Add one weapon at a time with a distinct tactical use.
Define blocking and target priority when introducing non-linear patterns.

## Enemies

Monsters are data definitions with stable IDs. Built-in and JSON-modded monsters share the same actor scene and registered movement behaviors; see [Modding](MODDING.md). Only the first configured attack currently executes.

Preserve the current six behaviors during architecture changes:

- Slow chaser: prepare a direction, then act in that direction next turn.
- Patroller: move forward; turn right when blocked.
- Left turner: move then turn left; an attack replaces that sequence.
- Right turner: move then turn right; an attack replaces that sequence.
- Stationary: no action.
- Charging beetle: telegraph a direction for one turn, then charge up to two cells along it, stopping early on a wall, an occupied cell, or an attack.

Enemies currently act across the entire floor.
Any later activation or awareness rules must be explicit.

## Terrain

Current:

- Solid walls block movement.
- Breakable walls have durability, render with a distinct brick
  texture, and can be dug to destruction (becoming floor).
- Doors have an IsOpen flag and disappear once the player or an enemy occupies them. They are walkable and do not block attacks even before opening; IsOpen currently controls the graphic.
- Spike traps are walkable and cycle Safe (2 turns) -> Warning (1) -> Active (1), damaging whoever stands on them during the Active phase - player and enemies alike. Placed only in hand-built encounters so far, not by procedural generation.
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

## Resume and debugging

Both implemented. Players can close the game and continue the current run: a complete state is saved at stable turn boundaries, including enemy intent and changed terrain, with a backup for recovery and a content fingerprint that refuses a resume whose definitions have changed.

Separately, the last 10 completed gameplay turns plus their starting state are retained as independent 2D-grid snapshots. Export Debug History writes one JSON file with the chosen slice, commands, actor health/facing/intent, per-attack detail and ordered outcomes for human or AI inspection.

This is diagnostic history, not ten prior floors and not a player rewind mechanic. It remains available after death or victory until restart. See [Save and debug history](SAVE_AND_DEBUG_HISTORY.md).

A separate authoring tool - place terrain, enemies and the player on any cell, then play and save that state as a reloadable scenario - is specified in [Debug scenario editor](DEBUG_SCENARIO_EDITOR.md) and is the next thing to build. It exists because emergent interactions have to be constructed and replayed to be validated at all.

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
