using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// One terrain cell's state at the moment a snapshot was captured. Copies
// values out of DungeonCell rather than referencing it, so later map
// mutations cannot rewrite history - see docs/SAVE_AND_DEBUG_HISTORY.md.
public sealed class CellSnapshot
{
	public GridPosition Position { get; }
	public TerrainKind Terrain { get; }
	public int Durability { get; }
	public bool IsOpen { get; }
	public int ZoneId { get; }
	public int ConnectedZoneA { get; }
	public int ConnectedZoneB { get; }

	// Meaningless unless Terrain is SpikeTrap. Real, active gameplay state
	// (not disabled/experimental terrain), so unlike the regrow timer this
	// is captured/restored exactly (NEXT_STEPS roadmap item 3).
	public SpikeTrapPhase SpikeTrapPhase { get; }
	public int SpikeTrapPhaseTurnsRemaining { get; }

	// Derived inspection data only, computed at capture time from the
	// authoritative Player/Enemies lists below - never a second source of
	// truth for where an actor is (docs/SAVE_AND_DEBUG_HISTORY.md). Kept as
	// the exact Guid[] the constructor takes, matching the CellSnapshot[][]
	// precedent on GridSnapshot.Rows for System.Text.Json's parameterized-
	// constructor binding.
	public Guid[] ActorInstanceIds { get; }

	public CellSnapshot(
		GridPosition position,
		TerrainKind terrain,
		int durability,
		bool isOpen,
		int zoneId,
		int connectedZoneA,
		int connectedZoneB,
		SpikeTrapPhase spikeTrapPhase,
		int spikeTrapPhaseTurnsRemaining,
		Guid[] actorInstanceIds)
	{
		Position = position;
		Terrain = terrain;
		Durability = durability;
		IsOpen = isOpen;
		ZoneId = zoneId;
		ConnectedZoneA = connectedZoneA;
		ConnectedZoneB = connectedZoneB;
		SpikeTrapPhase = spikeTrapPhase;
		SpikeTrapPhaseTurnsRemaining = spikeTrapPhaseTurnsRemaining;
		ActorInstanceIds = actorInstanceIds ?? Array.Empty<Guid>();
	}
}

// The full map at the moment of capture: width/height plus row-major cells,
// cells[y][x], per docs/SAVE_AND_DEBUG_HISTORY.md.
public sealed class GridSnapshot
{
	private readonly CellSnapshot[][] _rows;

	public int Width { get; }
	public int Height { get; }

	// Row-major cells[y][x], exposed for export/serialization (GetCell is
	// the normal access path for code that already knows x/y). Kept as the
	// same CellSnapshot[][] type the constructor takes - System.Text.Json's
	// parameterized-constructor deserialization requires an exact type
	// match between a constructor parameter and its bound property, not
	// just an assignment-compatible one (IReadOnlyList<IReadOnlyList<T>>
	// does not count, even though CellSnapshot[] implements
	// IReadOnlyList<CellSnapshot>).
	public CellSnapshot[][] Rows => _rows;

	public GridSnapshot(int width, int height, CellSnapshot[][] rows)
	{
		Width = width;
		Height = height;
		_rows = rows;
	}

	public CellSnapshot GetCell(int x, int y) => _rows[y][x];
}

// One actor's state at the moment of capture: everything ActorState and its
// referenced AttackState held, copied by value. Facing/PreparedDirection are
// plain direction vectors, not positions, so Vector2 here is not the
// "reads pixels" problem the rest of the migration is about.
public sealed class ActorSnapshot
{
	public Guid InstanceId { get; }
	public string DefinitionId { get; }
	public GridPosition Position { get; }
	public int Health { get; }
	public int MaxHealth { get; }
	public Vector2 Facing { get; }
	public bool HasPreparedMove { get; }
	public string WeaponId { get; }
	public string ToolId { get; }
	public bool IsAttackPreparing { get; }
	public int AttackRemainingPreparationTurns { get; }
	public Vector2 AttackPreparedDirection { get; }

	public ActorSnapshot(
		Guid instanceId,
		string definitionId,
		GridPosition position,
		int health,
		int maxHealth,
		Vector2 facing,
		bool hasPreparedMove,
		string weaponId,
		string toolId,
		bool isAttackPreparing,
		int attackRemainingPreparationTurns,
		Vector2 attackPreparedDirection)
	{
		InstanceId = instanceId;
		DefinitionId = definitionId;
		Position = position;
		Health = health;
		MaxHealth = maxHealth;
		Facing = facing;
		HasPreparedMove = hasPreparedMove;
		WeaponId = weaponId;
		ToolId = toolId;
		IsAttackPreparing = isAttackPreparing;
		AttackRemainingPreparationTurns = attackRemainingPreparationTurns;
		AttackPreparedDirection = attackPreparedDirection;
	}
}

// An independent, fully-copied point-in-time view of a GameState: the shared
// snapshot contract from docs/SAVE_AND_DEBUG_HISTORY.md, scoped to what the
// game currently has. Per-cell actor-id lists are derived here at capture
// time (CellSnapshot.ActorInstanceIds); Player/Enemies below remain the only
// authoritative source of actor position. Deferred, not yet meaningful here:
// schema/build/run/floor identity beyond TurnNumber/Status (that lives on
// the debug export wrapper instead - see DebugHistoryExportContext) and
// generation request/seed beyond Map.Seed.
public sealed class GameSnapshot
{
	public int TurnNumber { get; }
	public RunStatus Status { get; }
	public GridSnapshot Grid { get; }
	public ActorSnapshot Player { get; }
	public IReadOnlyList<ActorSnapshot> Enemies { get; }

	public GameSnapshot(
		int turnNumber,
		RunStatus status,
		GridSnapshot grid,
		ActorSnapshot player,
		IReadOnlyList<ActorSnapshot> enemies)
	{
		TurnNumber = turnNumber;
		Status = status;
		Grid = grid;
		Player = player;
		Enemies = enemies;
	}

	public static GameSnapshot Capture(GameState state)
	{
		if (state == null)
			throw new ArgumentNullException(nameof(state));

		ActorSnapshot player = CaptureActor(state.Player);
		List<ActorSnapshot> enemies = state.Enemies.Select(CaptureActor).ToList();

		return new GameSnapshot(
			state.TurnNumber,
			state.Status,
			CaptureGrid(state.Map, player, enemies),
			player,
			enemies
		);
	}

	private static GridSnapshot CaptureGrid(
		DungeonMap map,
		ActorSnapshot player,
		IReadOnlyList<ActorSnapshot> enemies)
	{
		Dictionary<GridPosition, List<Guid>> actorIdsByPosition = new();
		AddActorId(actorIdsByPosition, player);

		foreach (ActorSnapshot enemy in enemies)
			AddActorId(actorIdsByPosition, enemy);

		CellSnapshot[][] rows = new CellSnapshot[map.Height][];

		for (int y = 0; y < map.Height; y++)
		{
			rows[y] = new CellSnapshot[map.Width];

			for (int x = 0; x < map.Width; x++)
			{
				DungeonCell cell = map.GetCell(x, y);
				actorIdsByPosition.TryGetValue(cell.Position, out List<Guid> actorIds);

				rows[y][x] = new CellSnapshot(
					cell.Position,
					cell.Terrain.Kind,
					cell.Durability,
					cell.IsOpen,
					cell.ZoneId,
					cell.ConnectedZoneA,
					cell.ConnectedZoneB,
					cell.SpikeTrapPhase,
					cell.SpikeTrapPhaseTurnsRemaining,
					actorIds?.ToArray() ?? Array.Empty<Guid>()
				);
			}
		}

		return new GridSnapshot(map.Width, map.Height, rows);
	}

	private static void AddActorId(
		Dictionary<GridPosition, List<Guid>> actorIdsByPosition,
		ActorSnapshot actor)
	{
		if (!actorIdsByPosition.TryGetValue(actor.Position, out List<Guid> ids))
		{
			ids = new List<Guid>();
			actorIdsByPosition[actor.Position] = ids;
		}

		ids.Add(actor.InstanceId);
	}

	private static ActorSnapshot CaptureActor(ActorState actor)
	{
		return new ActorSnapshot(
			actor.InstanceId,
			actor.DefinitionId,
			actor.GridPosition,
			actor.Health,
			actor.MaxHealth,
			actor.Facing,
			actor.HasPreparedMove,
			actor.WeaponId,
			actor.ToolId,
			actor.Attack?.IsPreparing ?? false,
			actor.Attack?.RemainingPreparationTurns ?? 0,
			actor.Attack?.PreparedDirection ?? Vector2.Zero
		);
	}
}
