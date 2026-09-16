using System;
using System.Collections.Generic;
using System.Linq;

// One planned enemy spawn: which definition and where. Plain data, built by
// PlanSpawns and consumed by Main to actually instantiate Enemy nodes -
// this class never touches Godot.
public sealed class PlannedSpawn
{
	public MonsterDefinition Definition { get; }
	public GridPosition Position { get; }

	public PlannedSpawn(MonsterDefinition definition, GridPosition position)
	{
		Definition = definition;
		Position = position;
	}
}

// Deterministic roster/placement selection for NEXT_STEPS milestone 5: the
// same seed, generation configuration and content must produce the same
// enemy roster, order and positions, independent of dictionary or
// filesystem enumeration order. Everything here is driven by a
// System.Random seeded from the run's own dungeon seed - never
// Godot.RandomNumberGenerator or any other unseeded source - and the spawn
// pool is always sorted by id first, so neither mod load order nor the
// order BuildSpawnPool happened to concatenate built-ins/mods in can change
// the result.
public static class EncounterPlanner
{
	// How many enemies a run spawns, independent of how many monster
	// definitions currently exist - adding a new definition must not
	// silently add another enemy to every run. Matches the previous
	// one-enemy-per-built-in-definition roster size (5) so introducing an
	// explicit budget does not itself rebalance the prototype.
	public const int DefaultSpawnBudget = 5;

	private const int MaxRandomAttemptsPerCell = 100;

	public static IReadOnlyList<PlannedSpawn> PlanSpawns(
		IReadOnlyList<MonsterDefinition> spawnPool,
		IReadOnlyList<DungeonRoom> combatRooms,
		Func<GridPosition, bool> isValidSpawnCell,
		int seed,
		int spawnBudget = DefaultSpawnBudget)
	{
		if (spawnPool == null || spawnPool.Count == 0 ||
			combatRooms == null || combatRooms.Count == 0 ||
			spawnBudget <= 0)
		{
			return Array.Empty<PlannedSpawn>();
		}

		List<MonsterDefinition> sortedPool = spawnPool
			.OrderBy(definition => definition.Id, StringComparer.Ordinal)
			.ToList();

		Random random = new(seed);
		List<PlannedSpawn> planned = new();
		HashSet<GridPosition> claimed = new();

		for (int i = 0; i < spawnBudget; i++)
		{
			MonsterDefinition definition = sortedPool[random.Next(sortedPool.Count)];
			DungeonRoom room = combatRooms[i % combatRooms.Count];

			// A full room (or one with no valid cells left after earlier
			// spawns claimed them) is not a failure - it just caps this run
			// at fewer than the requested budget, per milestone 5's "cap
			// the result safely when there are fewer valid spawn cells than
			// the requested budget" rather than throwing.
			if (!TryPickSpawnCell(room, claimed, isValidSpawnCell, random, out GridPosition cell))
				continue;

			claimed.Add(cell);
			planned.Add(new PlannedSpawn(definition, cell));
		}

		return planned;
	}

	private static bool TryPickSpawnCell(
		DungeonRoom room,
		HashSet<GridPosition> claimed,
		Func<GridPosition, bool> isValidSpawnCell,
		Random random,
		out GridPosition cell)
	{
		for (int attempt = 0; attempt < MaxRandomAttemptsPerCell; attempt++)
		{
			GridPosition candidate = new(
				room.X + random.Next(room.Width),
				room.Y + random.Next(room.Height)
			);

			if (!claimed.Contains(candidate) && isValidSpawnCell(candidate))
			{
				cell = candidate;
				return true;
			}
		}

		for (int y = room.Y; y < room.Bottom; y++)
		{
			for (int x = room.X; x < room.Right; x++)
			{
				GridPosition candidate = new(x, y);

				if (!claimed.Contains(candidate) && isValidSpawnCell(candidate))
				{
					cell = candidate;
					return true;
				}
			}
		}

		cell = default;
		return false;
	}
}
