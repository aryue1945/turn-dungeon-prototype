using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public sealed class DynamicTerrainTests
{
	private const int Width = 24;
	private const int Height = 16;
	private const int ZoneCount = 5;
	private static readonly HashSet<GridPosition> NoOccupants = new();

	[Test]
	public void DestroyedTreeWallRegrowsAfterThreeUnoccupiedTurns()
	{
		DungeonMap map = Generate(seed: 1);
		GridPosition tree = FindCell(map, TerrainKind.TreeWall);

		map.DamageTerrain(tree.X, tree.Y, damage: 1);
		Assert.That(
			map.GetCell(tree.X, tree.Y).Terrain.Kind,
			Is.EqualTo(TerrainKind.Floor)
		);

		map.AdvanceTurn(NoOccupants);
		map.AdvanceTurn(NoOccupants);
		Assert.That(
			map.GetCell(tree.X, tree.Y).Terrain.Kind,
			Is.EqualTo(TerrainKind.Floor),
			"Should still be floor before the third turn."
		);

		IReadOnlyList<GridPosition> changed = map.AdvanceTurn(NoOccupants);
		Assert.That(
			map.GetCell(tree.X, tree.Y).Terrain.Kind,
			Is.EqualTo(TerrainKind.TreeWall)
		);
		Assert.That(changed, Does.Contain(tree));
	}

	[Test]
	public void RegrowthWaitsWhileAnActorOccupiesTheCell()
	{
		DungeonMap map = Generate(seed: 1);
		GridPosition tree = FindCell(map, TerrainKind.TreeWall);
		HashSet<GridPosition> occupied = new() { tree };

		map.DamageTerrain(tree.X, tree.Y, damage: 1);

		map.AdvanceTurn(occupied);
		map.AdvanceTurn(occupied);
		map.AdvanceTurn(occupied);
		Assert.That(
			map.GetCell(tree.X, tree.Y).Terrain.Kind,
			Is.EqualTo(TerrainKind.Floor),
			"An occupied cell must never regrow a wall on top of an actor."
		);

		map.AdvanceTurn(NoOccupants);
		Assert.That(
			map.GetCell(tree.X, tree.Y).Terrain.Kind,
			Is.EqualTo(TerrainKind.TreeWall),
			"Once the cell clears, the deferred regrowth should complete."
		);
	}

	[Test]
	public void GrowingWallSpreadsIntoAnAdjacentFloorCellOverTime()
	{
		DungeonMap map = Generate(seed: 0);
		GridPosition growingWall = FindCell(map, TerrainKind.GrowingWall);
		int growingWallCountBefore = CountCells(map, TerrainKind.GrowingWall);

		bool spread = false;

		for (int turn = 0; turn < 20 && !spread; turn++)
		{
			IReadOnlyList<GridPosition> changed = map.AdvanceTurn(NoOccupants);
			spread = changed.Count > 0;
		}

		Assert.That(spread, Is.True, "Expected the growing wall to spread within 20 turns.");
		Assert.That(
			CountCells(map, TerrainKind.GrowingWall),
			Is.EqualTo(growingWallCountBefore + 1)
		);
		Assert.That(
			map.GetCell(growingWall.X, growingWall.Y).Terrain.Kind,
			Is.EqualTo(TerrainKind.GrowingWall),
			"The original growing wall cell should remain a wall."
		);
	}

	[Test]
	public void GrowingWallNeverSpreadsOntoAnOccupiedCell()
	{
		DungeonMap map = Generate(seed: 0);
		GridPosition growingWall = FindCell(map, TerrainKind.GrowingWall);
		HashSet<GridPosition> occupied = new();

		for (int y = 0; y < map.Height; y++)
		for (int x = 0; x < map.Width; x++)
		{
			if (map.IsWalkable(x, y))
				occupied.Add(new GridPosition(x, y));
		}

		int growingWallCountBefore = CountCells(map, TerrainKind.GrowingWall);

		for (int turn = 0; turn < 20; turn++)
			map.AdvanceTurn(occupied);

		Assert.That(
			CountCells(map, TerrainKind.GrowingWall),
			Is.EqualTo(growingWallCountBefore),
			"Every floor cell is occupied, so growth must never find a free target."
		);
	}

	private static DungeonMap Generate(int seed)
	{
		return new DungeonGenerator().Generate(
			new DungeonGenerationRequest(Width, Height, ZoneCount, seed)
		);
	}

	private static GridPosition FindCell(DungeonMap map, TerrainKind terrainKind)
	{
		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				if (map.GetCell(x, y).Terrain.Kind == terrainKind)
					return new GridPosition(x, y);
			}
		}

		throw new AssertionException($"No {terrainKind} cell exists.");
	}

	private static int CountCells(DungeonMap map, TerrainKind terrainKind)
	{
		int count = 0;

		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				if (map.GetCell(x, y).Terrain.Kind == terrainKind)
					count++;
			}
		}

		return count;
	}
}
