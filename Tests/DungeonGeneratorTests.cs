using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public sealed class DungeonGeneratorTests
{
	private const int Width = 48;
	private const int Height = 32;
	private const int RoomCount = 10;

	[Test]
	public void Generate_CreatesRequestedNumberOfRoomsAndEnoughDoors()
	{
		DungeonMap map = Generate(seed: 12345);

		Assert.That(map.Rooms.Count, Is.EqualTo(RoomCount));
		Assert.That(
			CountCells(map, DungeonTerrain.Door),
			Is.GreaterThanOrEqualTo(RoomCount - 1)
		);
	}

	[Test]
	public void Generate_AddsDoorToEverySharedRoomWall()
	{
		DungeonMap map = Generate(seed: 54321);
		int adjacentPairCount = 0;

		for (int first = 0; first < map.Rooms.Count; first++)
		{
			for (int second = first + 1; second < map.Rooms.Count; second++)
			{
				DungeonRoom firstRoom = map.Rooms[first];
				DungeonRoom secondRoom = map.Rooms[second];
				List<GridPosition> sharedWall = GetSharedWall(
					firstRoom,
					secondRoom
				);

				if (sharedWall.Count == 0)
					continue;

				adjacentPairCount++;
				bool hasDoor = false;

				foreach (GridPosition position in sharedWall)
				{
					DungeonCell cell = map.GetCell(position.X, position.Y);

					if (cell.Terrain != DungeonTerrain.Door)
						continue;

					hasDoor = DoorConnects(cell, firstRoom, secondRoom);

					if (hasDoor)
						break;
				}

				Assert.That(
					hasDoor,
					Is.True,
					$"Zones {firstRoom.ZoneId} and " +
					$"{secondRoom.ZoneId} share a wall without a door."
				);
			}
		}

		Assert.That(adjacentPairCount, Is.GreaterThan(0));
	}

	[Test]
	public void Generate_AssignsEveryRoomItsOwnZone()
	{
		DungeonMap map = Generate(seed: 97531);

		foreach (DungeonRoom room in map.Rooms)
		{
			Assert.That(
				map.GetZoneId(room.Center.X, room.Center.Y),
				Is.EqualTo(room.ZoneId)
			);
		}
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
			Assert.That(
				map.GetCell(x, 0).Terrain,
				Is.EqualTo(DungeonTerrain.Wall)
			);
			Assert.That(
				map.GetCell(x, map.Height - 1).Terrain,
				Is.EqualTo(DungeonTerrain.Wall)
			);
		}

		for (int y = 0; y < map.Height; y++)
		{
			Assert.That(
				map.GetCell(0, y).Terrain,
				Is.EqualTo(DungeonTerrain.Wall)
			);
			Assert.That(
				map.GetCell(map.Width - 1, y).Terrain,
				Is.EqualTo(DungeonTerrain.Wall)
			);
		}
	}

	[Test]
	public void Generate_WithSameRequestProducesSameMap()
	{
		DungeonMap first = Generate(seed: 777);
		DungeonMap second = Generate(seed: 777);

		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				DungeonCell firstCell = first.GetCell(x, y);
				DungeonCell secondCell = second.GetCell(x, y);

				Assert.That(
					secondCell.Terrain,
					Is.EqualTo(firstCell.Terrain)
				);
				Assert.That(secondCell.ZoneId, Is.EqualTo(firstCell.ZoneId));
				Assert.That(
					secondCell.ConnectedZoneA,
					Is.EqualTo(firstCell.ConnectedZoneA)
				);
				Assert.That(
					secondCell.ConnectedZoneB,
					Is.EqualTo(firstCell.ConnectedZoneB)
				);
			}
		}
	}

	private static DungeonMap Generate(int seed)
	{
		DungeonGenerationRequest request = new(
			Width,
			Height,
			RoomCount,
			minimumRoomWidth: 5,
			minimumRoomHeight: 5,
			seed: seed
		);

		return new DungeonGenerator().Generate(request);
	}

	private static bool DoorConnects(
		DungeonCell door,
		DungeonRoom first,
		DungeonRoom second)
	{
		return door.ConnectedZoneA == first.ZoneId &&
				door.ConnectedZoneB == second.ZoneId ||
			door.ConnectedZoneA == second.ZoneId &&
				door.ConnectedZoneB == first.ZoneId;
	}

	private static List<GridPosition> GetSharedWall(
		DungeonRoom first,
		DungeonRoom second)
	{
		List<GridPosition> positions = new();
		DungeonRoom left = first.X < second.X ? first : second;
		DungeonRoom right = left == first ? second : first;

		if (left.Right + 1 == right.X)
		{
			int start = System.Math.Max(left.Y, right.Y);
			int end = System.Math.Min(left.Bottom, right.Bottom);

			for (int y = start; y < end; y++)
				positions.Add(new GridPosition(left.Right, y));

			if (positions.Count > 0)
				return positions;
		}

		DungeonRoom top = first.Y < second.Y ? first : second;
		DungeonRoom bottom = top == first ? second : first;

		if (top.Bottom + 1 != bottom.Y)
			return positions;

		int horizontalStart = System.Math.Max(top.X, bottom.X);
		int horizontalEnd = System.Math.Min(top.Right, bottom.Right);

		for (int x = horizontalStart; x < horizontalEnd; x++)
			positions.Add(new GridPosition(x, top.Bottom));

		return positions;
	}

	private static int CountCells(DungeonMap map, DungeonTerrain terrain)
	{
		int count = 0;

		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				if (map.GetCell(x, y).Terrain == terrain)
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
