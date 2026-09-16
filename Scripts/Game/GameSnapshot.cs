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

	public CellSnapshot(
		GridPosition position,
		TerrainKind terrain,
		int durability,
		bool isOpen,
		int zoneId,
		int connectedZoneA,
		int connectedZoneB)
	{
		Position = position;
		Terrain = terrain;
		Durability = durability;
		IsOpen = isOpen;
		ZoneId = zoneId;
		ConnectedZoneA = connectedZoneA;
		ConnectedZoneB = connectedZoneB;
	}
}

// The full map at the moment of capture: width/height plus row-major cells,
// cells[y][x], per docs/SAVE_AND_DEBUG_HISTORY.md.
public sealed class GridSnapshot
{
	private readonly CellSnapshot[][] _rows;

	public int Width { get; }
	public int Height { get; }

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
// game currently has. Deferred, not yet meaningful: schema/build/run/floor
// identity beyond TurnNumber/Status (no run-id or floor concept exists
// yet), generation request/seed beyond Map.Seed, content fingerprints, and
// per-cell actor-id lists (a derived view the spec calls optional - the
// actor list here already owns positions).
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

		return new GameSnapshot(
			state.TurnNumber,
			state.Status,
			CaptureGrid(state.Map),
			CaptureActor(state.Player),
			state.Enemies.Select(CaptureActor).ToList()
		);
	}

	private static GridSnapshot CaptureGrid(DungeonMap map)
	{
		CellSnapshot[][] rows = new CellSnapshot[map.Height][];

		for (int y = 0; y < map.Height; y++)
		{
			rows[y] = new CellSnapshot[map.Width];

			for (int x = 0; x < map.Width; x++)
			{
				DungeonCell cell = map.GetCell(x, y);
				rows[y][x] = new CellSnapshot(
					cell.Position,
					cell.Terrain.Kind,
					cell.Durability,
					cell.IsOpen,
					cell.ZoneId,
					cell.ConnectedZoneA,
					cell.ConnectedZoneB
				);
			}
		}

		return new GridSnapshot(map.Width, map.Height, rows);
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
