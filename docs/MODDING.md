# Modding: monsters

Monsters are data (`MonsterDefinition`), not hard-coded C# classes. The five
built-in enemies (`Scripts/Monsters/MonsterDefinitions.cs`) and any monster a
mod provides are the same shape and go through the same `Enemy` scene, so a
mod can add a new monster without writing or compiling any code.

There are two tiers, matching how much a mod needs to touch:

- **Data-only monsters** (documented here): combine an id, stats, a sprite,
  and existing movement/attack building blocks. Safe by construction - a bad
  file gets disabled with a readable error instead of crashing the game.
- **Scripted monsters** (custom behavior via a mod API) are not implemented.
  They would need a real security/compatibility story first, so they're
  intentionally out of scope until the data-only path has proven itself.

## Directory layout

Drop a folder per mod under `mods/` at the project root:

```
mods/
└── fire-rat/
    ├── mod.json
    ├── monsters/
    │   └── fire_rat.json
    └── sprites/
        └── fire_rat.png
```

At startup, `Main` calls `MonsterModLoader.LoadFromDirectory` against
`res://mods` (see `Scripts/Modding/MonsterModLoader.cs`), which:

1. Looks at every subfolder of `mods/`.
2. Reads every `*.json` file under `<mod>/monsters/`.
3. Validates each one (see below) and either produces a `MonsterDefinition`
   or disables it with a readable error printed via `GD.PushError`.
4. Adds every successfully loaded monster to the same spawn pool as the
   built-in roster, so it actually appears in combat zones.

`mod.json` (`id`, `name`, `version`) is read for a mod's own identity but is
currently informational only - nothing keys off it yet.

## Monster JSON

```json
{
  "id": "fire-rat.fire_rat",
  "name": "Fire Rat",
  "health": 4,
  "sprite": "fire_rat.png",
  "movement": "chase_player",
  "attacks": [
    { "pattern": "adjacent", "damage": 2, "effect": "burn" }
  ],
  "lootTable": "basic_monster"
}
```

| Field       | Required | Notes                                                                 |
|-------------|----------|------------------------------------------------------------------------|
| `id`        | yes      | Stable id. Prefix it with your mod's id (`fire-rat.fire_rat`) so it can never collide with another mod or the built-in `core.*` roster. Saves/generation should reference monsters by this id, never a C# type. |
| `name`      | yes      | Display name (shown over the enemy in-game).                          |
| `health`    | yes      | Must be greater than zero.                                            |
| `sprite`    | yes      | File name resolved under `<mod>/sprites/`. Must exist.                |
| `movement`  | yes      | One of the ids below. Anything else disables the monster.             |
| `attacks`   | yes      | At least one. Each needs a known `pattern` and a positive `damage`.   |
| `attacks[].effect` | no | Free-form status effect id. Stored on the attack (`AttackDefinition.StatusEffectId`) but **not yet applied by combat** - there's no status-effect system yet, so `"burn"` is currently just a tag for future use. |
| `lootTable` | no       | Free-form id; nothing consumes it yet.                                |

### Known movement behaviors

Defined in `Scripts/Monsters/EnemyMovementBehaviors.cs`:

- `chase_player` - faces toward the player one turn, moves along that facing the next.
- `patrol` - walks forward, turns right only when blocked.
- `turn_left` / `turn_right` - walks forward, then always turns that direction (an attack uses up the turn instead).
- `stationary` - never moves.

### Known attack patterns

Defined in `Scripts/Monsters/AttackPatterns.cs`:

- `adjacent` - one cell directly ahead.
- `line2` - one and two cells directly ahead (stops at the first thing it hits).

Both lists are intentionally small right now. Adding a new behavior or
pattern means adding one class/entry in C# - once it exists, every mod can
reference it by id.

## Validation

A bad monster file never crashes the game or blocks other mods from
loading. Each `monsters/*.json` is validated independently; a failure
disables just that file with a message like:

```
Fire Rat disabled: unknown movement behavior "chase_players". Expected one of: chase_player, patrol, turn_left, turn_right, stationary.
```

Checks, in order: JSON parses -> `id`/`name`/`health`/`sprite` present and
valid -> sprite file exists -> `movement` is known -> at least one attack,
each with a known `pattern` and positive `damage`. See
`Tests/MonsterModLoaderTests.cs` for the exact cases covered.

## Known limitations

- The mods path is resolved via `ProjectSettings.GlobalizePath("res://mods")`,
  which only maps to a real folder next to the project/editor - it will not
  resolve correctly inside an exported PCK. Before shipping a build, this
  needs to move to something like an executable-relative or `user://` path.
- No hot-reloading; mods load once at `Main._Ready`.
- No mod-vs-mod id collision detection yet.
- Scripted (code) mods are not implemented - see the two-tier note above.
