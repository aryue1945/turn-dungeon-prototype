using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public sealed class DungeonGeneratorTests
{
	private const int Width = 48;
	private const int Height = 32;
	private const int RoomCount = 10;

	[Test]
	public void Generate_CreatesRequestedNumberOfRoomsAndDoors()
	{
		DungeonMap map = Generate(seed: 12345);

		Assert.That(map.Rooms.Count, Is.EqualTo(RoomCount));
		Assert.That(
			CountCells(map, DungeonCellType.Door),
			Is.EqualTo(RoomCount - 1)
		);
	}

	[Test]
	public void Generate_MakesEveryWalkableCellReachable()
	{
		DungeonMap map = Generate(seed: 24680);
		GridPosition start = map.Rooms[0].Center;

		HashSet<GridPosition> visited = FloodFill(map, start);

		Assert.That(
			visited.Count,
			Is.EqualTo(CountWalkableCells(map))
		);
	}

	[Test]
	public void Generate_UsesSolidOuterWalls()
	{
		DungeonMap map = Generate(seed: 13579);

		for (int x = 0; x < map.Width; x++)
		{
			Assert.That(map.GetCell(x, 0), Is.EqualTo(DungeonCellType.Wall));
			Assert.That(
				map.GetCell(x, map.Height - 1),
				Is.EqualTo(DungeonCellType.Wall)
			);
		}

		for (int y = 0; y < map.Height; y++)
		{
			Assert.That(map.GetCell(0, y), Is.EqualTo(DungeonCellType.Wall));
			Assert.That(
				map.GetCell(map.Width - 1, y),
				Is.EqualTo(DungeonCellType.Wall)
			);
		}
	}

	[Test]
	public void Generate_WithSameSeedProducesSameMap()
	{
		DungeonMap first = Generate(seed: 777);
		DungeonMap second = Generate(seed: 777);

		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				Assert.That(
					second.GetCell(x, y),
					Is.EqualTo(first.GetCell(x, y))
				);
			}
		}
	}

	private static DungeonMap Generate(int seed)
	{
		return new DungeonGenerator().Generate(
			Width,
			Height,
			seed,
			RoomCount
		);
	}

	private static int CountCells(DungeonMap map, DungeonCellType type)
	{
		int count = 0;

		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				if (map.GetCell(x, y) == type)
					count++;
			}
		}

		return count;
	}

	private static int CountWalkableCells(DungeonMap map)
	{
		int count = 0;

		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				if (map.IsWalkable(x, y))
					count++;
			}
		}

		return count;
	}

	private static HashSet<GridPosition> FloodFill(
		DungeonMap map,
		GridPosition start)
	{
		GridPosition[] directions =
		{
			new(1, 0),
			new(-1, 0),
			new(0, 1),
			new(0, -1)
		};

		HashSet<GridPosition> visited = new() { start };
		Queue<GridPosition> remaining = new();
		remaining.Enqueue(start);

		while (remaining.Count > 0)
		{
			GridPosition current = remaining.Dequeue();

			foreach (GridPosition direction in directions)
			{
				GridPosition next = new(
					current.X + direction.X,
					current.Y + direction.Y
				);

				if (!map.IsWalkable(next.X, next.Y) ||
					!visited.Add(next))
				{
					continue;
				}

				remaining.Enqueue(next);
			}
		}

		return visited;
	}
}
