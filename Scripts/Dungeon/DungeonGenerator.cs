using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DungeonGenerator
{
	public DungeonMap Generate(DungeonGenerationRequest request)
	{
		Validate(request);

		Random random = new(request.Seed);
		DungeonMap map = new(request.Width, request.Height, request.Seed);
		InitializeBuilding(map);

		List<DungeonRoom> rooms = new()
		{
			new DungeonRoom(
				1,
				1,
				request.Width - 2,
				request.Height - 2
			)
		};

		while (rooms.Count < request.TargetRoomCount)
		{
			List<DungeonRoom> candidates = rooms
				.Where(room => CanSplit(room, request))
				.OrderByDescending(room => room.Area)
				.ToList();

			if (candidates.Count == 0)
				break;

			int candidatePoolSize = Math.Min(3, candidates.Count);
			DungeonRoom room = candidates[random.Next(candidatePoolSize)];
			int roomIndex = rooms.IndexOf(room);

			(DungeonRoom first, DungeonRoom second) =
				SplitRoom(map, room, request, random);

			rooms[roomIndex] = first;
			rooms.Add(second);
		}

		AssignZones(map, rooms);
		ConnectEveryAdjacentRoomPair(map, rooms, random);
		map.SetRooms(rooms.AsReadOnly());
		return map;
	}

	private static void Validate(DungeonGenerationRequest request)
	{
		if (request == null)
			throw new ArgumentNullException(nameof(request));

		if (request.MinimumRoomWidth < 2)
			throw new ArgumentOutOfRangeException(
				nameof(request.MinimumRoomWidth)
			);

		if (request.MinimumRoomHeight < 2)
			throw new ArgumentOutOfRangeException(
				nameof(request.MinimumRoomHeight)
			);

		if (request.Width < request.MinimumRoomWidth * 2 + 3)
			throw new ArgumentOutOfRangeException(nameof(request.Width));

		if (request.Height < request.MinimumRoomHeight * 2 + 3)
			throw new ArgumentOutOfRangeException(nameof(request.Height));

		if (request.TargetRoomCount < 1)
			throw new ArgumentOutOfRangeException(
				nameof(request.TargetRoomCount)
			);
	}

	private static void InitializeBuilding(DungeonMap map)
	{
		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				bool isBoundary = x == 0 || y == 0 ||
					x == map.Width - 1 || y == map.Height - 1;

				map.SetTerrain(
					x,
					y,
					isBoundary ? TerrainKind.SolidWall : TerrainKind.Floor
				);
			}
		}
	}

	private static bool CanSplit(
		DungeonRoom room,
		DungeonGenerationRequest request)
	{
		return CanSplitVertically(room, request) ||
			CanSplitHorizontally(room, request);
	}

	private static bool CanSplitVertically(
		DungeonRoom room,
		DungeonGenerationRequest request)
	{
		return room.Width >= request.MinimumRoomWidth * 2 + 1;
	}

	private static bool CanSplitHorizontally(
		DungeonRoom room,
		DungeonGenerationRequest request)
	{
		return room.Height >= request.MinimumRoomHeight * 2 + 1;
	}

	private static (DungeonRoom First, DungeonRoom Second) SplitRoom(
		DungeonMap map,
		DungeonRoom room,
		DungeonGenerationRequest request,
		Random random)
	{
		bool canSplitVertically = CanSplitVertically(room, request);
		bool canSplitHorizontally = CanSplitHorizontally(room, request);
		bool splitVertically;

		if (!canSplitHorizontally)
			splitVertically = true;
		else if (!canSplitVertically)
			splitVertically = false;
		else if (room.Width > room.Height * 1.25f)
			splitVertically = true;
		else if (room.Height > room.Width * 1.25f)
			splitVertically = false;
		else
			splitVertically = random.Next(2) == 0;

		return splitVertically
			? SplitVertically(map, room, request, random)
			: SplitHorizontally(map, room, request, random);
	}

	private static (DungeonRoom, DungeonRoom) SplitVertically(
		DungeonMap map,
		DungeonRoom room,
		DungeonGenerationRequest request,
		Random random)
	{
		int wallOffset = random.Next(
			request.MinimumRoomWidth,
			room.Width - request.MinimumRoomWidth
		);
		int wallX = room.X + wallOffset;

		for (int y = room.Y; y < room.Bottom; y++)
			map.SetTerrain(wallX, y, TerrainKind.BreakableWall);

		return (
			new DungeonRoom(room.X, room.Y, wallOffset, room.Height),
			new DungeonRoom(
				wallX + 1,
				room.Y,
				room.Width - wallOffset - 1,
				room.Height
			)
		);
	}

	private static (DungeonRoom, DungeonRoom) SplitHorizontally(
		DungeonMap map,
		DungeonRoom room,
		DungeonGenerationRequest request,
		Random random)
	{
		int wallOffset = random.Next(
			request.MinimumRoomHeight,
			room.Height - request.MinimumRoomHeight
		);
		int wallY = room.Y + wallOffset;

		for (int x = room.X; x < room.Right; x++)
			map.SetTerrain(x, wallY, TerrainKind.BreakableWall);

		return (
			new DungeonRoom(room.X, room.Y, room.Width, wallOffset),
			new DungeonRoom(
				room.X,
				wallY + 1,
				room.Width,
				room.Height - wallOffset - 1
			)
		);
	}

	private static void AssignZones(
		DungeonMap map,
		IReadOnlyList<DungeonRoom> rooms)
	{
		for (int zoneId = 0; zoneId < rooms.Count; zoneId++)
		{
			DungeonRoom room = rooms[zoneId];
			room.ZoneId = zoneId;

			for (int y = room.Y; y < room.Bottom; y++)
			{
				for (int x = room.X; x < room.Right; x++)
					map.SetZone(x, y, zoneId);
			}
		}
	}

	private static void ConnectEveryAdjacentRoomPair(
		DungeonMap map,
		IReadOnlyList<DungeonRoom> rooms,
		Random random)
	{
		for (int firstIndex = 0; firstIndex < rooms.Count; firstIndex++)
		{
			for (
				int secondIndex = firstIndex + 1;
				secondIndex < rooms.Count;
				secondIndex++
			)
			{
				TryCreateSharedWallDoor(
					map,
					rooms[firstIndex],
					rooms[secondIndex],
					random
				);
			}
		}
	}

	private static void TryCreateSharedWallDoor(
		DungeonMap map,
		DungeonRoom first,
		DungeonRoom second,
		Random random)
	{
		DungeonRoom left = first.X < second.X ? first : second;
		DungeonRoom right = left == first ? second : first;

		if (left.Right + 1 == right.X)
		{
			int overlapStart = Math.Max(left.Y, right.Y);
			int overlapEnd = Math.Min(left.Bottom, right.Bottom);

			if (overlapEnd > overlapStart)
			{
				int doorY = ChooseDoorCoordinate(
					overlapStart,
					overlapEnd,
					random
				);
				map.SetDoor(
					left.Right,
					doorY,
					left.ZoneId,
					right.ZoneId
				);
				return;
			}
		}

		DungeonRoom top = first.Y < second.Y ? first : second;
		DungeonRoom bottom = top == first ? second : first;

		if (top.Bottom + 1 != bottom.Y)
			return;

		int horizontalOverlapStart = Math.Max(top.X, bottom.X);
		int horizontalOverlapEnd = Math.Min(top.Right, bottom.Right);

		if (horizontalOverlapEnd <= horizontalOverlapStart)
			return;

		int doorX = ChooseDoorCoordinate(
			horizontalOverlapStart,
			horizontalOverlapEnd,
			random
		);
		map.SetDoor(
			doorX,
			top.Bottom,
			top.ZoneId,
			bottom.ZoneId
		);
	}

	private static int ChooseDoorCoordinate(
		int overlapStart,
		int overlapEnd,
		Random random)
	{
		int length = overlapEnd - overlapStart;

		return length > 2
			? random.Next(overlapStart + 1, overlapEnd - 1)
			: random.Next(overlapStart, overlapEnd);
	}
}
