using System;
using System.Collections.Generic;

public enum DungeonTerrain
{
	Empty,
	Floor,
	Wall,
	Door
}

public readonly record struct GridPosition(int X, int Y);

public sealed class DungeonCell
{
	public GridPosition Position { get; }
	public DungeonTerrain Terrain { get; internal set; }
	public int ZoneId { get; internal set; } = -1;
	public int ConnectedZoneA { get; internal set; } = -1;
	public int ConnectedZoneB { get; internal set; } = -1;
	public bool IsWalkable =>
		Terrain == DungeonTerrain.Floor ||
		Terrain == DungeonTerrain.Door;

	internal DungeonCell(GridPosition position)
	{
		Position = position;
	}
}

public sealed class DungeonRoom
{
	public int ZoneId { get; internal set; } = -1;
	public int X { get; }
	public int Y { get; }
	public int Width { get; }
	public int Height { get; }
	public int Right => X + Width;
	public int Bottom => Y + Height;
	public int Area => Width * Height;
	public GridPosition Center => new(X + Width / 2, Y + Height / 2);

	public DungeonRoom(int x, int y, int width, int height)
	{
		X = x;
		Y = y;
		Width = width;
		Height = height;
	}
}

public sealed class DungeonGenerationRequest
{
	public int Width { get; }
	public int Height { get; }
	public int TargetRoomCount { get; }
	public int MinimumRoomWidth { get; }
	public int MinimumRoomHeight { get; }
	public int Seed { get; }

	public DungeonGenerationRequest(
		int width,
		int height,
		int targetRoomCount,
		int minimumRoomWidth,
		int minimumRoomHeight,
		int seed)
	{
		Width = width;
		Height = height;
		TargetRoomCount = targetRoomCount;
		MinimumRoomWidth = minimumRoomWidth;
		MinimumRoomHeight = minimumRoomHeight;
		Seed = seed;
	}
}

public sealed class DungeonMap
{
	private readonly DungeonCell[,] _cells;
	private IReadOnlyList<DungeonRoom> _rooms = Array.Empty<DungeonRoom>();

	public int Width { get; }
	public int Height { get; }
	public int Seed { get; }
	public IReadOnlyList<DungeonRoom> Rooms => _rooms;

	public DungeonMap(int width, int height, int seed)
	{
		if (width <= 0)
			throw new ArgumentOutOfRangeException(nameof(width));

		if (height <= 0)
			throw new ArgumentOutOfRangeException(nameof(height));

		Width = width;
		Height = height;
		Seed = seed;
		_cells = new DungeonCell[width, height];

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
				_cells[x, y] = new DungeonCell(new GridPosition(x, y));
		}
	}

	public DungeonCell GetCell(int x, int y)
	{
		return IsInside(x, y) ? _cells[x, y] : null;
	}

	public bool IsInside(int x, int y)
	{
		return x >= 0 && x < Width && y >= 0 && y < Height;
	}

	public bool IsWalkable(int x, int y)
	{
		DungeonCell cell = GetCell(x, y);
		return cell != null && cell.IsWalkable;
	}

	public int GetZoneId(int x, int y)
	{
		DungeonCell cell = GetCell(x, y);
		return cell?.ZoneId ?? -1;
	}

	internal void SetTerrain(int x, int y, DungeonTerrain terrain)
	{
		DungeonCell cell = GetCell(x, y) ??
			throw new ArgumentOutOfRangeException();

		cell.Terrain = terrain;
	}

	internal void SetZone(int x, int y, int zoneId)
	{
		DungeonCell cell = GetCell(x, y) ??
			throw new ArgumentOutOfRangeException();

		cell.ZoneId = zoneId;
	}

	internal void SetDoor(
		int x,
		int y,
		int connectedZoneA,
		int connectedZoneB)
	{
		DungeonCell cell = GetCell(x, y) ??
			throw new ArgumentOutOfRangeException();

		cell.Terrain = DungeonTerrain.Door;
		cell.ZoneId = -1;
		cell.ConnectedZoneA = connectedZoneA;
		cell.ConnectedZoneB = connectedZoneB;
	}

	internal void SetRooms(IReadOnlyList<DungeonRoom> rooms)
	{
		_rooms = rooms;
	}
}
