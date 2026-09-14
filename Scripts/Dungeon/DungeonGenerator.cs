using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DungeonGenerator
{
	private const int MinimumRoomWidth = 5;
	private const int MinimumRoomHeight = 5;

	public DungeonMap Generate(
		int width,
		int height,
		int seed,
		int targetRoomCount = 10)
	{
		if (width < MinimumRoomWidth * 2 + 3)
			throw new ArgumentOutOfRangeException(nameof(width));

		if (height < MinimumRoomHeight * 2 + 3)
			throw new ArgumentOutOfRangeException(nameof(height));

		if (targetRoomCount < 1)
			throw new ArgumentOutOfRangeException(nameof(targetRoomCount));

		Random random = new(seed);
		DungeonMap map = new(width, height, seed);
		InitializeBuilding(map);

		List<DungeonRoom> rooms = new()
		{
			new DungeonRoom(1, 1, width - 2, height - 2)
		};

		while (rooms.Count < targetRoomCount)
		{
			List<DungeonRoom> candidates = rooms
				.Where(CanSplit)
				.OrderByDescending(room => room.Area)
				.ToList();

			if (candidates.Count == 0)
				break;

			int candidatePoolSize = Math.Min(3, candidates.Count);
			DungeonRoom room = candidates[random.Next(candidatePoolSize)];
			int roomIndex = rooms.IndexOf(room);

			(DungeonRoom first, DungeonRoom second) =
				SplitRoom(map, room, random);

			rooms[roomIndex] = first;
			rooms.Add(second);
		}

		map.SetRooms(rooms.AsReadOnly());
		return map;
	}

	private static void InitializeBuilding(DungeonMap map)
	{
		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				bool isBoundary = x == 0 || y == 0 ||
					x == map.Width - 1 || y == map.Height - 1;

				map.SetCell(
					x,
					y,
					isBoundary
						? DungeonCellType.Wall
						: DungeonCellType.Floor
				);
			}
		}
	}

	private static bool CanSplit(DungeonRoom room)
	{
		return CanSplitVertically(room) || CanSplitHorizontally(room);
	}

	private static bool CanSplitVertically(DungeonRoom room)
	{
		return room.Width >= MinimumRoomWidth * 2 + 1;
	}

	private static bool CanSplitHorizontally(DungeonRoom room)
	{
		return room.Height >= MinimumRoomHeight * 2 + 1;
	}

	private static (DungeonRoom First, DungeonRoom Second) SplitRoom(
		DungeonMap map,
		DungeonRoom room,
		Random random)
	{
		bool canSplitVertically = CanSplitVertically(room);
		bool canSplitHorizontally = CanSplitHorizontally(room);
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
			? SplitVertically(map, room, random)
			: SplitHorizontally(map, room, random);
	}

	private static (DungeonRoom, DungeonRoom) SplitVertically(
		DungeonMap map,
		DungeonRoom room,
		Random random)
	{
		int wallOffset = random.Next(
			MinimumRoomWidth,
			room.Width - MinimumRoomWidth
		);
		int wallX = room.X + wallOffset;

		for (int y = room.Y; y < room.Bottom; y++)
			map.SetCell(wallX, y, DungeonCellType.Wall);

		int doorY = random.Next(room.Y + 1, room.Bottom - 1);
		map.SetCell(wallX, doorY, DungeonCellType.Door);

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
		Random random)
	{
		int wallOffset = random.Next(
			MinimumRoomHeight,
			room.Height - MinimumRoomHeight
		);
		int wallY = room.Y + wallOffset;

		for (int x = room.X; x < room.Right; x++)
			map.SetCell(x, wallY, DungeonCellType.Wall);

		int doorX = random.Next(room.X + 1, room.Right - 1);
		map.SetCell(doorX, wallY, DungeonCellType.Door);

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
}
