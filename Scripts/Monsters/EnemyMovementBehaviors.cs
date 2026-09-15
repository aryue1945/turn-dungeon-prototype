using Godot;
using System;
using System.Collections.Generic;

// Outcome of a single forward-move attempt, shared between Enemy and every
// movement behavior.
public enum EnemyMoveResult
{
	Moved,
	AttackAction,
	Blocked
}

// The primitives a movement behavior needs from the Enemy it drives, without
// exposing the rest of Enemy's internals. Enemy implements this explicitly.
public interface IEnemyMovementHost
{
	Vector2 Position { get; }
	Vector2 FacingDirection { get; }

	void SetFacingDirection(Vector2 direction);
	void SetFacingIndicatorVisible(bool visible);
	void TurnLeft();
	void TurnRight();

	EnemyMoveResult TryMoveForward(
		HashSet<Vector2> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants);
}

// One reusable, swappable piece of monster AI. Both built-in monsters and
// modded ones are configured with a behavior id (see EnemyMovementBehaviors)
// instead of hard-coded logic, so new monsters can be assembled from
// existing behaviors without new C#.
public interface IEnemyMovementBehavior
{
	Vector2 InitialFacingDirection { get; }
	bool ShowsFacingIndicatorInitially { get; }

	void TakeTurn(
		IEnemyMovementHost host,
		Player player,
		HashSet<Vector2> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants);
}

// Chases the player: spends one turn choosing a facing direction toward the
// player, then the next turn moving (or attacking) along it. Mirrors the
// former "SlowChaser" enemy type.
public sealed class ChasePlayerBehavior : IEnemyMovementBehavior
{
	private bool _hasPreparedMove;

	public Vector2 InitialFacingDirection => Vector2.Down;
	public bool ShowsFacingIndicatorInitially => false;

	public void TakeTurn(
		IEnemyMovementHost host,
		Player player,
		HashSet<Vector2> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		if (!_hasPreparedMove)
		{
			// Preparing is the entire action for this turn.
			PrepareMove(host, player.Position);
			_hasPreparedMove = true;
			return;
		}

		// Moving uses the direction locked during the previous turn.
		host.TryMoveForward(occupiedEnemyPositions, combatants);
		_hasPreparedMove = false;
		host.SetFacingIndicatorVisible(false);
	}

	private static void PrepareMove(IEnemyMovementHost host, Vector2 playerPosition)
	{
		Vector2 difference = playerPosition - host.Position;

		if (difference.IsZeroApprox())
			return;

		Vector2 direction = Mathf.Abs(difference.X) > Mathf.Abs(difference.Y)
			? new Vector2(Mathf.Sign(difference.X), 0)
			: new Vector2(0, Mathf.Sign(difference.Y));

		host.SetFacingDirection(direction);
		host.SetFacingIndicatorVisible(true);
	}
}

// Walks forward; turns right only when blocked. Mirrors the former
// "Patroller" enemy type.
public sealed class PatrolBehavior : IEnemyMovementBehavior
{
	public Vector2 InitialFacingDirection => Vector2.Right;
	public bool ShowsFacingIndicatorInitially => true;

	public void TakeTurn(
		IEnemyMovementHost host,
		Player player,
		HashSet<Vector2> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		EnemyMoveResult result =
			host.TryMoveForward(occupiedEnemyPositions, combatants);

		if (result == EnemyMoveResult.Blocked)
			host.TurnRight();
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

	public void TakeTurn(
		IEnemyMovementHost host,
		Player player,
		HashSet<Vector2> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		EnemyMoveResult result =
			host.TryMoveForward(occupiedEnemyPositions, combatants);

		// An attack consumes the entire beat.
		if (result == EnemyMoveResult.AttackAction)
			return;

		// Otherwise turning is the second action, even if movement was blocked.
		if (_turnRight)
			host.TurnRight();
		else
			host.TurnLeft();
	}
}

// Never moves or turns on its own. Mirrors the former "Stationary" enemy type.
public sealed class StationaryBehavior : IEnemyMovementBehavior
{
	public Vector2 InitialFacingDirection => Vector2.Down;
	public bool ShowsFacingIndicatorInitially => false;

	public void TakeTurn(
		IEnemyMovementHost host,
		Player player,
		HashSet<Vector2> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
	}
}

// The registry of movement behavior ids a MonsterDefinition can reference,
// for both the built-in roster and mod JSON. Each entry is a factory rather
// than a shared instance because behaviors such as ChasePlayerBehavior carry
// per-enemy state.
public static class EnemyMovementBehaviors
{
	private static readonly Dictionary<string, Func<IEnemyMovementBehavior>> Factories = new()
	{
		["chase_player"] = () => new ChasePlayerBehavior(),
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
