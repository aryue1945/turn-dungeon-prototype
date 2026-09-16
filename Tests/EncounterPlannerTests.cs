using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

[TestFixture]
public sealed class EncounterPlannerTests
{
	private static readonly MonsterDefinition Chaser = MonsterDefinitions.SlowChaser;
	private static readonly MonsterDefinition Patroller = MonsterDefinitions.Patroller;

	[Test]
	public void PlanSpawns_SameSeedProducesSameRosterOrderAndPositions()
	{
		List<MonsterDefinition> pool = new() { Chaser, Patroller };
		List<DungeonRoom> rooms = new() { new DungeonRoom(0, 0, 5, 5) };

		IReadOnlyList<PlannedSpawn> first = EncounterPlanner.PlanSpawns(
			pool, rooms, AlwaysValid, seed: 1234, spawnBudget: 3);
		IReadOnlyList<PlannedSpawn> second = EncounterPlanner.PlanSpawns(
			pool, rooms, AlwaysValid, seed: 1234, spawnBudget: 3);

		Assert.That(first.Count, Is.EqualTo(second.Count));

		for (int i = 0; i < first.Count; i++)
		{
			Assert.That(second[i].Definition.Id, Is.EqualTo(first[i].Definition.Id));
			Assert.That(second[i].Position, Is.EqualTo(first[i].Position));
		}
	}

	[Test]
	public void PlanSpawns_DifferentSeedCanProduceADifferentEncounter()
	{
		List<MonsterDefinition> pool = new() { Chaser, Patroller };
		List<DungeonRoom> rooms = new() { new DungeonRoom(0, 0, 8, 8) };

		IReadOnlyList<PlannedSpawn> a = EncounterPlanner.PlanSpawns(
			pool, rooms, AlwaysValid, seed: 1, spawnBudget: 4);
		IReadOnlyList<PlannedSpawn> b = EncounterPlanner.PlanSpawns(
			pool, rooms, AlwaysValid, seed: 2, spawnBudget: 4);

		bool anyDifference =
			a.Count != b.Count ||
			a.Where((spawn, i) => spawn.Position != b[i].Position || spawn.Definition.Id != b[i].Definition.Id)
				.Any();

		Assert.That(anyDifference, Is.True, "Two different seeds produced an identical encounter.");
	}

	[Test]
	public void PlanSpawns_IsIndependentOfSpawnPoolEnumerationOrder()
	{
		List<MonsterDefinition> poolA = new() { Chaser, Patroller };
		List<MonsterDefinition> poolB = new() { Patroller, Chaser };
		List<DungeonRoom> rooms = new() { new DungeonRoom(0, 0, 5, 5) };

		IReadOnlyList<PlannedSpawn> a = EncounterPlanner.PlanSpawns(
			poolA, rooms, AlwaysValid, seed: 42, spawnBudget: 3);
		IReadOnlyList<PlannedSpawn> b = EncounterPlanner.PlanSpawns(
			poolB, rooms, AlwaysValid, seed: 42, spawnBudget: 3);

		for (int i = 0; i < a.Count; i++)
			Assert.That(b[i].Definition.Id, Is.EqualTo(a[i].Definition.Id));
	}

	[Test]
	public void PlanSpawns_NeverPlansTheSameCellTwice()
	{
		List<MonsterDefinition> pool = new() { Chaser };
		List<DungeonRoom> rooms = new() { new DungeonRoom(0, 0, 3, 3) };

		IReadOnlyList<PlannedSpawn> spawns = EncounterPlanner.PlanSpawns(
			pool, rooms, AlwaysValid, seed: 7, spawnBudget: 5);

		Assert.That(
			spawns.Select(spawn => spawn.Position).Distinct().Count(),
			Is.EqualTo(spawns.Count)
		);
	}

	[Test]
	public void PlanSpawns_CapsSafelyWhenFewerValidCellsThanBudget()
	{
		List<MonsterDefinition> pool = new() { Chaser };
		List<DungeonRoom> rooms = new() { new DungeonRoom(0, 0, 2, 1) };

		IReadOnlyList<PlannedSpawn> spawns = EncounterPlanner.PlanSpawns(
			pool, rooms, AlwaysValid, seed: 99, spawnBudget: 10);

		Assert.That(spawns.Count, Is.EqualTo(2), "Only two cells exist in a 2x1 room.");
	}

	[Test]
	public void PlanSpawns_ReturnsEmptyWhenNoValidCellsExist()
	{
		List<MonsterDefinition> pool = new() { Chaser };
		List<DungeonRoom> rooms = new() { new DungeonRoom(0, 0, 5, 5) };

		IReadOnlyList<PlannedSpawn> spawns = EncounterPlanner.PlanSpawns(
			pool, rooms, _ => false, seed: 1, spawnBudget: 5);

		Assert.That(spawns, Is.Empty);
	}

	[Test]
	public void PlanSpawns_ReturnsEmptyWithNoSpawnPoolOrNoCombatRooms()
	{
		List<MonsterDefinition> pool = new() { Chaser };
		List<DungeonRoom> rooms = new() { new DungeonRoom(0, 0, 5, 5) };

		Assert.That(
			EncounterPlanner.PlanSpawns(new List<MonsterDefinition>(), rooms, AlwaysValid, seed: 1),
			Is.Empty
		);
		Assert.That(
			EncounterPlanner.PlanSpawns(pool, new List<DungeonRoom>(), AlwaysValid, seed: 1),
			Is.Empty
		);
	}

	[Test]
	public void PlanSpawns_DefaultBudgetMatchesThePreviousFiveEnemyRosterSize()
	{
		Assert.That(EncounterPlanner.DefaultSpawnBudget, Is.EqualTo(5));
	}

	private static bool AlwaysValid(GridPosition position) => true;
}
