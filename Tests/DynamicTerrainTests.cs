using NUnit.Framework;
using System.Collections.Generic;

// TreeWall/GrowingWall are implemented in DungeonMap (regrowth and slow
// spread) but are currently disabled: no zone template places them, and
// Main no longer calls DungeonMap.AdvanceTurn. These tests build small maps
// directly so the dormant mechanic stays covered and ready to re-enable
// (wire a template symbol to it and call AdvanceTurn once per turn again).
[TestFixture]
public sealed class DynamicTerrainTests
{
	private const int Size = 5;
	private static readonly HashSet<GridPosition> NoOccupants = new();

	[Test]
	public void DestroyedTreeWallRegrowsAfterThreeUnoccupiedTurns()
	{
		DungeonMap map = CreateMapWithTerrain(TerrainKind.TreeWall, out GridPosition tree);

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
		DungeonMap map = CreateMapWithTerrain(TerrainKind.TreeWall, out GridPosition tree);
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
		DungeonMap map = CreateMapWithTerrain(TerrainKind.GrowingWall, out GridPosition growingWall);
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
		DungeonMap map = CreateMapWithTerrain(TerrainKind.GrowingWall, out _);
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

	// A small enclosed room: a solid border, floor interior, and one cell of
	// the requested kind at the center.
	private static DungeonMap CreateMapWithTerrain(
		TerrainKind kind,
		out GridPosition placedAt)
	{
		DungeonMap map = new(Size, Size, seed: 1);

		for (int y = 0; y < Size; y++)
		{
			for (int x = 0; x < Size; x++)
			{
				bool isBoundary = x == 0 || y == 0 ||
					x == Size - 1 || y == Size - 1;
				map.SetTerrain(
					x,
					y,
					isBoundary ? TerrainKind.SolidWall : TerrainKind.Floor
				);
			}
		}

		placedAt = new GridPosition(Size / 2, Size / 2);
		map.SetTerrain(placedAt.X, placedAt.Y, kind);
		return map;
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
