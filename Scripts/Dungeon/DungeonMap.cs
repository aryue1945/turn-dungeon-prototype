using System;
using System.Collections.Generic;

public enum DungeonCellType
{
	Empty,
	Floor,
	Wall,
	Door
}

public readonly record struct GridPosition(int X, int Y);

public sealed class DungeonRoom
{
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

public sealed class DungeonMap
{
	private readonly DungeonCellType[,] _cells;
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
		_cells = new DungeonCellType[width, height];
	}

	public DungeonCellType GetCell(int x, int y)
	{
		return IsInside(x, y)
			? _cells[x, y]
			: DungeonCellType.Empty;
	}

	public bool IsInside(int x, int y)
	{
		return x >= 0 && x < Width && y >= 0 && y < Height;
	}

	public bool IsWalkable(int x, int y)
	{
		DungeonCellType cell = GetCell(x, y);
		return cell == DungeonCellType.Floor ||
			cell == DungeonCellType.Door;
	}

	internal void SetCell(int x, int y, DungeonCellType cell)
	{
		if (!IsInside(x, y))
			throw new ArgumentOutOfRangeException();

		_cells[x, y] = cell;
	}

	internal void SetRooms(IReadOnlyList<DungeonRoom> rooms)
	{
		_rooms = rooms;
	}
}
