using System;
using System.Collections.Generic;
using System.Text;

public enum TerrainKind
{
	Empty,
	Floor,
	SolidWall,
	BreakableWall,
	Door,
	Fire,
	Ice
}

public sealed class TerrainDefinition
{
	public TerrainKind Kind { get; }
	public string Name { get; }
	public char DebugSymbol { get; }
	public bool IsWalkable { get; }
	public int MaxDurability { get; }
	public int ContactDamage { get; }
	public TerrainKind? DestroyedInto { get; }
	public bool IsDestructible =>
		MaxDurability > 0 && DestroyedInto.HasValue;

	public TerrainDefinition(
		TerrainKind kind,
		string name,
		char debugSymbol,
		bool isWalkable,
		int maxDurability = 0,
		int contactDamage = 0,
		TerrainKind? destroyedInto = null)
	{
		Kind = kind;
		Name = name;
		DebugSymbol = debugSymbol;
		IsWalkable = isWalkable;
		MaxDurability = maxDurability;
		ContactDamage = contactDamage;
		DestroyedInto = destroyedInto;
	}
}

public static class TerrainCatalog
{
	private static readonly IReadOnlyDictionary<
		TerrainKind,
		TerrainDefinition
	> Definitions = new Dictionary<TerrainKind, TerrainDefinition>
	{
		[TerrainKind.Empty] = new(
			TerrainKind.Empty, "Empty", ' ', isWalkable: false
		),
		[TerrainKind.Floor] = new(
			TerrainKind.Floor, "Floor", '.', isWalkable: true
		),
		[TerrainKind.SolidWall] = new(
			TerrainKind.SolidWall,
			"Solid Wall",
			'#',
			isWalkable: false
		),
		[TerrainKind.BreakableWall] = new(
			TerrainKind.BreakableWall,
			"Breakable Wall",
			'B',
			isWalkable: false,
			maxDurability: 1,
			destroyedInto: TerrainKind.Floor
		),
		[TerrainKind.Door] = new(
			TerrainKind.Door, "Door", 'D', isWalkable: true
		),
		[TerrainKind.Fire] = new(
			TerrainKind.Fire,
			"Fire",
			'F',
			isWalkable: true,
			contactDamage: 1
		),
		[TerrainKind.Ice] = new(
			TerrainKind.Ice, "Ice", 'I', isWalkable: true
		)
	};

	public static TerrainDefinition Get(TerrainKind kind)
	{
		return Definitions[kind];
	}
}

public readonly record struct GridPosition(int X, int Y);

public sealed class DungeonCell
{
	public GridPosition Position { get; }
	public TerrainDefinition Terrain { get; private set; }
	public int Durability { get; private set; }
	public int ZoneId { get; internal set; } = -1;
	public int ConnectedZoneA { get; internal set; } = -1;
	public int ConnectedZoneB { get; internal set; } = -1;
	public bool IsWalkable => Terrain.IsWalkable;

	internal DungeonCell(GridPosition position)
	{
		Position = position;
		SetTerrain(TerrainKind.Empty);
	}

	internal void SetTerrain(TerrainKind terrainKind)
	{
		Terrain = TerrainCatalog.Get(terrainKind);
		Durability = Terrain.MaxDurability;
	}

	internal bool DamageTerrain(int damage)
	{
		if (damage <= 0 || !Terrain.IsDestructible)
			return false;

		Durability = Math.Max(0, Durability - damage);

		if (Durability > 0)
			return false;

		SetTerrain(Terrain.DestroyedInto.Value);
		return true;
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

	public bool DamageTerrain(int x, int y, int damage)
	{
		DungeonCell cell = GetCell(x, y);
		return cell != null && cell.DamageTerrain(damage);
	}

	public string ToDebugString()
	{
		StringBuilder output = new();
		output.AppendLine(
			$"Dungeon map {Width}x{Height}, seed {Seed}:"
		);

		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				char symbol = _cells[x, y].Terrain.DebugSymbol;
				output.Append(symbol);
				output.Append(symbol);
			}

			output.AppendLine();
		}

		output.Append(
			"Legend: ## solid, BB breakable, DD door, .. floor, " +
			"FF fire, II ice"
		);
		return output.ToString();
	}

	internal void SetTerrain(int x, int y, TerrainKind terrainKind)
	{
		DungeonCell cell = GetCell(x, y) ??
			throw new ArgumentOutOfRangeException();

		cell.SetTerrain(terrainKind);
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

		cell.SetTerrain(TerrainKind.Door);
		cell.ZoneId = -1;
		cell.ConnectedZoneA = connectedZoneA;
		cell.ConnectedZoneB = connectedZoneB;
	}

	internal void SetRooms(IReadOnlyList<DungeonRoom> rooms)
	{
		_rooms = rooms;
	}
}
