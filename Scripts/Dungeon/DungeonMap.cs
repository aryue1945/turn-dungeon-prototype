using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public enum TerrainKind
{
	Empty,
	Floor,
	SolidWall,
	BreakableWall,
	TreeWall,
	GrowingWall,
	Door,
	Fire,
	Ice,
	SpikeTrap
}

// A spike trap cell's cycle (NEXT_STEPS roadmap item 3): Safe -> Warning
// (telegraphs the coming Active phase) -> Active (damages whoever is
// standing on it) -> back to Safe. Meaningless on any other terrain kind.
public enum SpikeTrapPhase
{
	Safe,
	Warning,
	Active
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
	public int? RegrowAfterTurns { get; }
	public bool IsDestructible =>
		MaxDurability > 0 && DestroyedInto.HasValue;

	public TerrainDefinition(
		TerrainKind kind,
		string name,
		char debugSymbol,
		bool isWalkable,
		int maxDurability = 0,
		int contactDamage = 0,
		TerrainKind? destroyedInto = null,
		int? regrowAfterTurns = null)
	{
		Kind = kind;
		Name = name;
		DebugSymbol = debugSymbol;
		IsWalkable = isWalkable;
		MaxDurability = maxDurability;
		ContactDamage = contactDamage;
		DestroyedInto = destroyedInto;
		RegrowAfterTurns = regrowAfterTurns;
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
		[TerrainKind.TreeWall] = new(
			TerrainKind.TreeWall,
			"Tree Wall",
			'T',
			isWalkable: false,
			maxDurability: 1,
			destroyedInto: TerrainKind.Floor,
			regrowAfterTurns: 3
		),
		[TerrainKind.GrowingWall] = new(
			TerrainKind.GrowingWall,
			"Growing Wall",
			'G',
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
		),
		[TerrainKind.SpikeTrap] = new(
			TerrainKind.SpikeTrap,
			"Spike Trap",
			'^',
			isWalkable: true,
			contactDamage: 1
		)
	};

	public static TerrainDefinition Get(TerrainKind kind)
	{
		return Definitions[kind];
	}
}

public readonly record struct GridPosition(int X, int Y);

public enum DungeonZoneType
{
	Start,
	Combat,
	Shop,
	Exit
}

public sealed class DungeonCell
{
	public GridPosition Position { get; }
	public TerrainDefinition Terrain { get; private set; }
	public int Durability { get; private set; }
	public int ZoneId { get; internal set; } = -1;
	public int ConnectedZoneA { get; internal set; } = -1;
	public int ConnectedZoneB { get; internal set; } = -1;
	public bool IsWalkable => Terrain.IsWalkable;
	public bool IsOpen { get; private set; }
	public int? PendingRegrowTurns =>
		_regrowingKind.HasValue ? _regrowTurnsRemaining : null;

	// Meaningless on any terrain other than SpikeTrap. Unlike the regrow
	// timer below, this is real per-turn state that must persist and export
	// (NEXT_STEPS roadmap item 3), not disabled/dropped-on-restore
	// experimental terrain.
	public SpikeTrapPhase SpikeTrapPhase { get; private set; }
	public int SpikeTrapPhaseTurnsRemaining { get; private set; }

	private const int SpikeTrapSafeTurns = 2;
	private const int SpikeTrapWarningTurns = 1;
	private const int SpikeTrapActiveTurns = 1;

	private TerrainKind? _regrowingKind;
	private int _regrowTurnsRemaining;

	internal DungeonCell(GridPosition position)
	{
		Position = position;
		SetTerrain(TerrainKind.Empty);
	}

	internal void SetTerrain(TerrainKind terrainKind)
	{
		Terrain = TerrainCatalog.Get(terrainKind);
		Durability = Terrain.MaxDurability;
		IsOpen = false;
		_regrowingKind = null;
		_regrowTurnsRemaining = 0;
		SpikeTrapPhase = SpikeTrapPhase.Safe;
		SpikeTrapPhaseTurnsRemaining = terrainKind == TerrainKind.SpikeTrap ? SpikeTrapSafeTurns : 0;
	}

	// Sets every piece of a cell's saved state directly, for milestone-4
	// save/resume: unlike SetTerrain, durability/IsOpen are not reset to
	// terrain defaults, since a restored cell may be mid-damage or an
	// already-open door. Pending regrowth is intentionally not restored -
	// dynamic terrain is still disabled (see docs/NEXT_STEPS.md) - but a
	// spike trap's cycle phase/timer is restored exactly, since it is real,
	// active gameplay state that must persist.
	internal void RestoreState(
		TerrainKind terrainKind,
		int durability,
		bool isOpen,
		int zoneId,
		int connectedZoneA,
		int connectedZoneB,
		SpikeTrapPhase spikeTrapPhase,
		int spikeTrapPhaseTurnsRemaining)
	{
		Terrain = TerrainCatalog.Get(terrainKind);
		Durability = durability;
		IsOpen = isOpen;
		ZoneId = zoneId;
		ConnectedZoneA = connectedZoneA;
		ConnectedZoneB = connectedZoneB;
		_regrowingKind = null;
		_regrowTurnsRemaining = 0;
		SpikeTrapPhase = spikeTrapPhase;
		SpikeTrapPhaseTurnsRemaining = spikeTrapPhaseTurnsRemaining;
	}

	// Called once per game turn for every SpikeTrap cell (DungeonMap.
	// TickSpikeTraps). Cycles Safe -> Warning -> Active -> Safe on a fixed
	// schedule, independent of occupancy - unlike terrain regrowth, a spike
	// trap's cycle is not paused by someone standing on it.
	internal void TickSpikeTrap()
	{
		if (Terrain.Kind != TerrainKind.SpikeTrap)
			return;

		SpikeTrapPhaseTurnsRemaining--;

		if (SpikeTrapPhaseTurnsRemaining > 0)
			return;

		SpikeTrapPhase = SpikeTrapPhase switch
		{
			SpikeTrapPhase.Safe => SpikeTrapPhase.Warning,
			SpikeTrapPhase.Warning => SpikeTrapPhase.Active,
			_ => SpikeTrapPhase.Safe
		};

		SpikeTrapPhaseTurnsRemaining = SpikeTrapPhase switch
		{
			SpikeTrapPhase.Safe => SpikeTrapSafeTurns,
			SpikeTrapPhase.Warning => SpikeTrapWarningTurns,
			_ => SpikeTrapActiveTurns
		};
	}

	internal bool Open()
	{
		if (Terrain.Kind != TerrainKind.Door || IsOpen)
			return false;

		IsOpen = true;
		return true;
	}

	internal bool DamageTerrain(int damage)
	{
		if (damage <= 0 || !Terrain.IsDestructible)
			return false;

		Durability = Math.Max(0, Durability - damage);

		if (Durability > 0)
			return false;

		TerrainDefinition destroyedTerrain = Terrain;
		SetTerrain(destroyedTerrain.DestroyedInto.Value);

		if (destroyedTerrain.RegrowAfterTurns.HasValue)
		{
			_regrowingKind = destroyedTerrain.Kind;
			_regrowTurnsRemaining = destroyedTerrain.RegrowAfterTurns.Value;
		}

		return true;
	}

	// Called once per game turn. Counts down while the cell sits empty and
	// converts back once the timer expires, unless an actor is currently
	// standing on the cell (never regrow a wall on top of someone).
	internal bool TickRegrowth(bool isOccupied)
	{
		if (_regrowingKind == null)
			return false;

		if (_regrowTurnsRemaining > 0)
			_regrowTurnsRemaining--;

		if (_regrowTurnsRemaining > 0)
			return false;

		if (isOccupied)
			return false;

		TerrainKind regrownKind = _regrowingKind.Value;
		_regrowingKind = null;
		SetTerrain(regrownKind);
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

public sealed class DungeonZone
{
	private readonly List<int> _connectedZoneIds = new();

	public int Id { get; }
	public DungeonRoom Room { get; }
	public GridPosition LayoutPosition { get; }
	public GridPosition TemplateOrigin { get; }
	public DungeonZoneType Type { get; internal set; }
	public string TemplateName { get; internal set; } = "Open";
	public int TemplateRotation { get; internal set; }
	public bool TemplateMirrored { get; internal set; }
	public IReadOnlyList<int> ConnectedZoneIds => _connectedZoneIds;

	public DungeonZone(
		int id,
		DungeonRoom room,
		GridPosition layoutPosition,
		GridPosition templateOrigin)
	{
		Id = id;
		Room = room;
		LayoutPosition = layoutPosition;
		TemplateOrigin = templateOrigin;
	}

	internal void ConnectTo(int zoneId)
	{
		if (zoneId != Id && !_connectedZoneIds.Contains(zoneId))
			_connectedZoneIds.Add(zoneId);
	}
}

public sealed class DungeonGenerationRequest
{
	public int Width { get; }
	public int Height { get; }
	public int TargetZoneCount { get; }
	public int Seed { get; }

	public DungeonGenerationRequest(
		int width,
		int height,
		int targetZoneCount,
		int seed)
	{
		Width = width;
		Height = height;
		TargetZoneCount = targetZoneCount;
		Seed = seed;
	}
}

public sealed class DungeonMap
{
	// How often (in game turns) a GrowingWall cell attempts to spread into
	// one adjacent floor cell. One spread per interval keeps it "slow".
	private const int GrowthIntervalTurns = 4;

	private static readonly GridPosition[] GrowthDirections =
	{
		new(1, 0),
		new(-1, 0),
		new(0, 1),
		new(0, -1)
	};

	private readonly DungeonCell[,] _cells;
	private readonly Random _terrainRandom;
	private IReadOnlyList<DungeonRoom> _rooms = Array.Empty<DungeonRoom>();
	private IReadOnlyList<DungeonZone> _zones = Array.Empty<DungeonZone>();
	private int _turnCounter;

	public int Width { get; }
	public int Height { get; }
	public int Seed { get; }
	public IReadOnlyList<DungeonRoom> Rooms => _rooms;
	public IReadOnlyList<DungeonZone> Zones => _zones;

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
		_terrainRandom = new Random(seed);

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

	public DungeonZone GetZone(int zoneId)
	{
		return zoneId >= 0 && zoneId < _zones.Count
			? _zones[zoneId]
			: null;
	}

	public bool DamageTerrain(int x, int y, int damage)
	{
		DungeonCell cell = GetCell(x, y);
		return cell != null && cell.DamageTerrain(damage);
	}

	public bool OpenDoor(int x, int y)
	{
		DungeonCell cell = GetCell(x, y);
		return cell != null && cell.Open();
	}

	// Advances destroyed-terrain regrowth timers and, occasionally, spreads
	// GrowingWall cells. Call once per completed game turn. occupiedCells
	// prevents regrowth or growth from ever landing on an actor. Returns the
	// cells whose terrain changed, so the caller can refresh their visuals.
	public IReadOnlyList<GridPosition> AdvanceTurn(
		IReadOnlyCollection<GridPosition> occupiedCells)
	{
		List<GridPosition> changedCells = new();
		_turnCounter++;

		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				DungeonCell cell = _cells[x, y];

				if (cell.TickRegrowth(occupiedCells.Contains(cell.Position)))
					changedCells.Add(cell.Position);
			}
		}

		if (_turnCounter % GrowthIntervalTurns == 0)
		{
			GridPosition? grownCell = TryGrowOneWall(occupiedCells);

			if (grownCell.HasValue)
				changedCells.Add(grownCell.Value);
		}

		return changedCells;
	}

	private GridPosition? TryGrowOneWall(
		IReadOnlyCollection<GridPosition> occupiedCells)
	{
		List<DungeonCell> growingCells = new();

		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				if (_cells[x, y].Terrain.Kind == TerrainKind.GrowingWall)
					growingCells.Add(_cells[x, y]);
			}
		}

		if (growingCells.Count == 0)
			return null;

		DungeonCell source = growingCells[_terrainRandom.Next(growingCells.Count)];
		List<GridPosition> shuffledDirections = GrowthDirections
			.OrderBy(_ => _terrainRandom.Next())
			.ToList();

		foreach (GridPosition direction in shuffledDirections)
		{
			DungeonCell target = GetCell(
				source.Position.X + direction.X,
				source.Position.Y + direction.Y
			);

			if (target == null ||
				target.Terrain.Kind != TerrainKind.Floor ||
				occupiedCells.Contains(target.Position))
			{
				continue;
			}

			target.SetTerrain(TerrainKind.GrowingWall);
			return target.Position;
		}

		return null;
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
				output.Append(_cells[x, y].Terrain.DebugSymbol);
				output.Append(' ');
			}

			output.AppendLine();
		}

		output.Append(
			"Legend: # solid, B breakable, T tree, G growing, D door, " +
			". floor, F fire, I ice, ^ spike trap (each cell uses two columns)"
		);

		foreach (DungeonZone zone in Zones)
		{
			output.AppendLine();
			output.Append(
				$"Zone {zone.Id}: {zone.Type}, " +
				$"template {zone.TemplateName}, " +
				$"connections [{string.Join(", ", zone.ConnectedZoneIds)}]"
			);
		}

		return output.ToString();
	}

	internal void SetTerrain(int x, int y, TerrainKind terrainKind)
	{
		DungeonCell cell = GetCell(x, y) ??
			throw new ArgumentOutOfRangeException();

		cell.SetTerrain(terrainKind);
	}

	// Milestone-4 save/resume: restores one cell's full saved state. Callers
	// outside this file reach it only through a Game-layer restore helper
	// (e.g. rebuilding a map from a GridSnapshot) that knows the save
	// format - DungeonMap itself stays unaware of GameSnapshot/CellSnapshot.
	internal void RestoreCell(
		int x,
		int y,
		TerrainKind terrainKind,
		int durability,
		bool isOpen,
		int zoneId,
		int connectedZoneA,
		int connectedZoneB,
		SpikeTrapPhase spikeTrapPhase,
		int spikeTrapPhaseTurnsRemaining)
	{
		DungeonCell cell = GetCell(x, y) ??
			throw new ArgumentOutOfRangeException();

		cell.RestoreState(
			terrainKind,
			durability,
			isOpen,
			zoneId,
			connectedZoneA,
			connectedZoneB,
			spikeTrapPhase,
			spikeTrapPhaseTurnsRemaining
		);
	}

	// Advances every spike trap cell's cycle by one turn (NEXT_STEPS
	// roadmap item 3). Call once per completed game turn, after enemy
	// actions and before the turn's snapshot/autosave. This method only
	// advances the cycle, it does not know about combatants - the caller
	// damages whoever stands on an ActiveCells position and refreshes the
	// visuals of every ChangedCells position (not just the ones that became
	// Active - a cell leaving Active also needs its tint refreshed).
	public (IReadOnlyList<GridPosition> ChangedCells, IReadOnlyList<GridPosition> ActiveCells) TickSpikeTraps()
	{
		List<GridPosition> changedCells = new();
		List<GridPosition> activeCells = new();

		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				DungeonCell cell = _cells[x, y];

				if (cell.Terrain.Kind != TerrainKind.SpikeTrap)
					continue;

				SpikeTrapPhase previousPhase = cell.SpikeTrapPhase;
				cell.TickSpikeTrap();

				if (cell.SpikeTrapPhase != previousPhase)
					changedCells.Add(cell.Position);

				if (cell.SpikeTrapPhase == SpikeTrapPhase.Active)
					activeCells.Add(cell.Position);
			}
		}

		return (changedCells, activeCells);
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

	internal void SetZones(IReadOnlyList<DungeonZone> zones)
	{
		_zones = zones;
	}
}
