using Godot;
using System;
using System.Collections.Generic;

// What one enemy's turn actually did. Idle/Prepared never touch the map;
// Attacked/Preparing carry the attack's name for narration; Moved/Blocked
// describe a forward-step attempt. This is the enemy-side counterpart to
// PlayerActionOutcome (Scripts/Game/TurnResolver.cs) - same idea, scoped to
// what movement behaviors can currently produce.
public enum EnemyActionKind
{
	Idle,
	Prepared,
	Moved,
	Attacked,
	Preparing,
	Blocked
}

public sealed class EnemyActionResult
{
	public EnemyActionKind Kind { get; }
	public string AttackName { get; }
	public AttackExecutionDetail AttackDetail { get; }

	private EnemyActionResult(
		EnemyActionKind kind,
		string attackName,
		AttackExecutionDetail attackDetail)
	{
		Kind = kind;
		AttackName = attackName;
		AttackDetail = attackDetail;
	}

	public static readonly EnemyActionResult Idle =
		new(EnemyActionKind.Idle, null, null);
	public static readonly EnemyActionResult Prepared =
		new(EnemyActionKind.Prepared, null, null);
	public static readonly EnemyActionResult Moved =
		new(EnemyActionKind.Moved, null, null);
	public static readonly EnemyActionResult Blocked =
		new(EnemyActionKind.Blocked, null, null);

	public static EnemyActionResult Attacked(
		string attackName,
		AttackExecutionDetail attackDetail) =>
		new(EnemyActionKind.Attacked, attackName, attackDetail);

	public static EnemyActionResult Preparing(string attackName) =>
		new(EnemyActionKind.Preparing, attackName, null);

	public bool IsAttackAction =>
		Kind == EnemyActionKind.Attacked || Kind == EnemyActionKind.Preparing;
}

// The primitives a movement behavior needs from the Enemy it drives, without
// exposing the rest of Enemy's internals. Enemy implements this explicitly.
public interface IEnemyMovementHost
{
	GridPosition GridPosition { get; }
	Vector2 FacingDirection { get; }
	bool HasPreparedMove { get; }

	void SetFacingDirection(Vector2 direction);
	void SetFacingIndicatorVisible(bool visible);
	void SetHasPreparedMove(bool hasPreparedMove);
	void TurnLeft();
	void TurnRight();

	bool IsWallAt(GridPosition position);

	EnemyActionResult TryMoveForward(
		HashSet<GridPosition> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants);
}

// Shared by the chase-type behaviors below: locks facing toward the first
// step of a wall-avoiding route to the player (see EnemyPathfinding), or
// reports failure when the player is unreachable so the caller can skip the
// turn instead of telegraphing a move into a wall.
internal static class ChaseTowardPlayer
{
	public static bool TryPrepare(IEnemyMovementHost host, GridPosition playerGridPosition)
	{
		Vector2? direction = EnemyPathfinding.FindNextStepDirection(
			host.GridPosition, playerGridPosition, host.IsWallAt);

		if (direction == null)
			return false;

		host.SetFacingDirection(direction.Value);
		host.SetFacingIndicatorVisible(true);
		return true;
	}
}

// One reusable, swappable piece of monster AI. Both built-in monsters and
// modded ones are configured with a behavior id (see EnemyMovementBehaviors)
// instead of hard-coded logic, so new monsters can be assembled from
// existing behaviors without new C#.
public interface IEnemyMovementBehavior
{
	Vector2 InitialFacingDirection { get; }
	bool ShowsFacingIndicatorInitially { get; }

	EnemyActionResult TakeTurn(
		IEnemyMovementHost host,
		ICombatant player,
		HashSet<GridPosition> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants);
}

// Chases the player: spends one turn choosing a facing direction toward the
// player, then the next turn moving (or attacking) along it. Mirrors the
// former "SlowChaser" enemy type.
//
// The behavior instance itself holds no mutable state - "is a move
// prepared" lives on the host's ActorState (via HasPreparedMove) so it
// survives independently of this object and can eventually be captured for
// save/debug history.
public sealed class ChasePlayerBehavior : IEnemyMovementBehavior
{
	public Vector2 InitialFacingDirection => Vector2.Down;
	public bool ShowsFacingIndicatorInitially => false;

	public EnemyActionResult TakeTurn(
		IEnemyMovementHost host,
		ICombatant player,
		HashSet<GridPosition> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		if (!host.HasPreparedMove)
		{
			// Preparing is the entire action for this turn. No route to the
			// player means no move to prepare - skip the turn instead of
			// telegraphing a direction that only leads into a wall.
			if (!ChaseTowardPlayer.TryPrepare(host, player.GridPosition))
				return EnemyActionResult.Idle;

			host.SetHasPreparedMove(true);
			return EnemyActionResult.Prepared;
		}

		// Moving uses the direction locked during the previous turn.
		EnemyActionResult result =
			host.TryMoveForward(occupiedEnemyPositions, combatants);
		host.SetHasPreparedMove(false);
		host.SetFacingIndicatorVisible(false);
		return result;
	}
}

// Prepares for one turn (visibly locking a charge direction toward the
// player, exactly like ChasePlayerBehavior's telegraph), then charges up to
// two cells in that direction on the next turn. Each cell of the charge is
// one TryMoveForward call, so the existing attack-before-move check inside
// it already makes the beetle attack instead of moving onto an occupied
// cell, and a Blocked/Attacked result already ends the charge early - no
// new combat or forced-movement code needed here, only the up-to-two-steps
// loop. First tactical-slice enemy (NEXT_STEPS roadmap item 3).
public sealed class ChargingBeetleBehavior : IEnemyMovementBehavior
{
	private const int ChargeDistanceCells = 2;

	public Vector2 InitialFacingDirection => Vector2.Down;
	public bool ShowsFacingIndicatorInitially => false;

	public EnemyActionResult TakeTurn(
		IEnemyMovementHost host,
		ICombatant player,
		HashSet<GridPosition> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		if (!host.HasPreparedMove)
		{
			if (!ChaseTowardPlayer.TryPrepare(host, player.GridPosition))
				return EnemyActionResult.Idle;

			host.SetHasPreparedMove(true);
			return EnemyActionResult.Prepared;
		}

		host.SetHasPreparedMove(false);
		host.SetFacingIndicatorVisible(false);

		EnemyActionResult result = EnemyActionResult.Blocked;

		for (int step = 0; step < ChargeDistanceCells; step++)
		{
			result = host.TryMoveForward(occupiedEnemyPositions, combatants);

			if (result.Kind != EnemyActionKind.Moved)
				break;
		}

		return result;
	}
}

// Walks forward; turns right only when blocked. Mirrors the former
// "Patroller" enemy type.
public sealed class PatrolBehavior : IEnemyMovementBehavior
{
	public Vector2 InitialFacingDirection => Vector2.Right;
	public bool ShowsFacingIndicatorInitially => true;

	public EnemyActionResult TakeTurn(
		IEnemyMovementHost host,
		ICombatant player,
		HashSet<GridPosition> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		EnemyActionResult result =
			host.TryMoveForward(occupiedEnemyPositions, combatants);

		if (result.Kind == EnemyActionKind.Blocked)
			host.TurnRight();

		return result;
	}
}

// Walks forward, then always turns (left or right) on the same beat unless
// that beat was spent attacking. Mirrors the former "LeftTurner"/
// "RightTurner" enemy types.
public sealed class TurningWalkerBehavior : IEnemyMovementBehavior
{
	private readonly bool _turnRight;

	public TurningWalkerBehavior(bool turnRight)
	{
		_turnRight = turnRight;
	}

	public Vector2 InitialFacingDirection => Vector2.Down;
	public bool ShowsFacingIndicatorInitially => true;

	public EnemyActionResult TakeTurn(
		IEnemyMovementHost host,
		ICombatant player,
		HashSet<GridPosition> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		EnemyActionResult result =
			host.TryMoveForward(occupiedEnemyPositions, combatants);

		// An attack consumes the entire beat.
		if (result.IsAttackAction)
			return result;

		// Otherwise turning is the second action, even if movement was blocked.
		if (_turnRight)
			host.TurnRight();
		else
			host.TurnLeft();

		return result;
	}
}

// Never moves or turns on its own. Mirrors the former "Stationary" enemy type.
public sealed class StationaryBehavior : IEnemyMovementBehavior
{
	public Vector2 InitialFacingDirection => Vector2.Down;
	public bool ShowsFacingIndicatorInitially => false;

	public EnemyActionResult TakeTurn(
		IEnemyMovementHost host,
		ICombatant player,
		HashSet<GridPosition> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		return EnemyActionResult.Idle;
	}
}

// The registry of movement behavior ids a MonsterDefinition can reference,
// for both the built-in roster and mod JSON. Behaviors are stateless now
// (their per-enemy state lives on the host's ActorState); each entry stays a
// factory rather than a shared instance for consistency with new behaviors
// that might reintroduce their own state later.
public static class EnemyMovementBehaviors
{
	private static readonly Dictionary<string, Func<IEnemyMovementBehavior>> Factories = new()
	{
		["chase_player"] = () => new ChasePlayerBehavior(),
		["charge_beetle"] = () => new ChargingBeetleBehavior(),
		["patrol"] = () => new PatrolBehavior(),
		["turn_left"] = () => new TurningWalkerBehavior(turnRight: false),
		["turn_right"] = () => new TurningWalkerBehavior(turnRight: true),
		["stationary"] = () => new StationaryBehavior()
	};

	public static IReadOnlyCollection<string> KnownIds => Factories.Keys;

	public static bool IsKnown(string id) =>
		id != null && Factories.ContainsKey(id);

	public static IEnemyMovementBehavior Create(string id)
	{
		if (!Factories.TryGetValue(id ?? string.Empty, out Func<IEnemyMovementBehavior> factory))
		{
			throw new ArgumentException(
				$"Unknown movement behavior \"{id}\". " +
				$"Expected one of: {string.Join(", ", Factories.Keys)}."
			);
		}

		return factory();
	}
}
