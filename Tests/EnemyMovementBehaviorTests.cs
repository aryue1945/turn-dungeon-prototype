using Godot;
using NUnit.Framework;
using System;
using System.Collections.Generic;

// Movement behaviors depend only on IEnemyMovementHost and ICombatant (for
// the player's position), so they need no Godot node to test - this is the
// payoff of EnemyActionResult replacing void/callback-only decisions.
[TestFixture]
public sealed class EnemyMovementBehaviorTests
{
	[Test]
	public void ChasePlayerBehavior_FirstTurnPreparesTowardThePlayer()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		FakeCombatant player = new(new GridPosition(3, 0));

		EnemyActionResult result = new ChasePlayerBehavior().TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Prepared));
		Assert.That(host.HasPreparedMove, Is.True);
		Assert.That(host.FacingDirection, Is.EqualTo(Vector2.Right));
		Assert.That(host.MoveForwardCallCount, Is.EqualTo(0));
	}

	[Test]
	public void ChasePlayerBehavior_SecondTurnMovesAlongThePreparedDirection()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		FakeCombatant player = new(new GridPosition(3, 0));
		ChasePlayerBehavior behavior = new();

		behavior.TakeTurn(host, player, new HashSet<GridPosition>(), new List<ICombatant>());
		EnemyActionResult result = behavior.TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Moved));
		Assert.That(host.HasPreparedMove, Is.False);
		Assert.That(host.MoveForwardCallCount, Is.EqualTo(1));
	}

	[Test]
	public void ChasePlayerBehavior_RoutesAroundAWallBlockingTheDirectPath()
	{
		FakeMovementHost host = new(new GridPosition(0, 0))
		{
			// Walls block the direct path and the detour above it, leaving
			// only a route through (0, 1) below.
			IsWallAtFunc = position =>
				position == new GridPosition(1, 0) ||
				position == new GridPosition(1, -1)
		};
		FakeCombatant player = new(new GridPosition(2, 0));

		EnemyActionResult result = new ChasePlayerBehavior().TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Prepared));
		Assert.That(host.FacingDirection, Is.EqualTo(Vector2.Down));
	}

	[Test]
	public void ChasePlayerBehavior_SkipsTheTurnWhenNoRouteExists()
	{
		FakeMovementHost host = new(new GridPosition(0, 0))
		{
			// Walls on all four sides box the enemy in completely.
			IsWallAtFunc = position =>
				position == new GridPosition(1, 0) ||
				position == new GridPosition(-1, 0) ||
				position == new GridPosition(0, 1) ||
				position == new GridPosition(0, -1)
		};
		FakeCombatant player = new(new GridPosition(5, 5));

		EnemyActionResult result = new ChasePlayerBehavior().TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Idle));
		Assert.That(host.HasPreparedMove, Is.False);
		Assert.That(host.MoveForwardCallCount, Is.EqualTo(0));
	}

	[Test]
	public void ChargingBeetleBehavior_FirstTurnPreparesTowardThePlayer()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		FakeCombatant player = new(new GridPosition(0, 4));

		EnemyActionResult result = new ChargingBeetleBehavior().TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Prepared));
		Assert.That(host.HasPreparedMove, Is.True);
		Assert.That(host.FacingDirection, Is.EqualTo(Vector2.Down));
		Assert.That(host.MoveForwardCallCount, Is.EqualTo(0));
	}

	[Test]
	public void ChargingBeetleBehavior_SecondTurnChargesTwoCellsWhenClear()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		FakeCombatant player = new(new GridPosition(0, 4));
		ChargingBeetleBehavior behavior = new();
		host.NextMoveResult = EnemyActionResult.Moved;

		behavior.TakeTurn(host, player, new HashSet<GridPosition>(), new List<ICombatant>());
		EnemyActionResult result = behavior.TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Moved));
		Assert.That(host.HasPreparedMove, Is.False);
		Assert.That(host.MoveForwardCallCount, Is.EqualTo(2), "A clear charge takes exactly two steps.");
	}

	[Test]
	public void ChargingBeetleBehavior_StopsAfterOneCellWhenTheSecondIsBlocked()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		FakeCombatant player = new(new GridPosition(0, 4));
		ChargingBeetleBehavior behavior = new();
		host.NextMoveResult = EnemyActionResult.Blocked;

		behavior.TakeTurn(host, player, new HashSet<GridPosition>(), new List<ICombatant>());
		EnemyActionResult result = behavior.TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Blocked));
		Assert.That(host.MoveForwardCallCount, Is.EqualTo(1), "A blocked first step must not attempt a second.");
	}

	[Test]
	public void ChargingBeetleBehavior_StopsTheChargeWhenItAttacks()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		FakeCombatant player = new(new GridPosition(0, 1));
		ChargingBeetleBehavior behavior = new();
		host.NextMoveResult = EnemyActionResult.Attacked("Charge Slam", null);

		behavior.TakeTurn(host, player, new HashSet<GridPosition>(), new List<ICombatant>());
		EnemyActionResult result = behavior.TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Attacked));
		Assert.That(result.AttackName, Is.EqualTo("Charge Slam"));
		Assert.That(host.MoveForwardCallCount, Is.EqualTo(1), "An attack on the first step must not attempt a second.");
	}

	[Test]
	public void PatrolBehavior_TurnsRightWhenBlocked()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		host.NextMoveResult = EnemyActionResult.Blocked;

		EnemyActionResult result = new PatrolBehavior().TakeTurn(
			host, null, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Blocked));
		Assert.That(host.TurnRightCallCount, Is.EqualTo(1));
	}

	[Test]
	public void PatrolBehavior_DoesNotTurnWhenItMoved()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		host.NextMoveResult = EnemyActionResult.Moved;

		new PatrolBehavior().TakeTurn(
			host, null, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(host.TurnRightCallCount, Is.EqualTo(0));
	}

	[Test]
	public void TurningWalkerBehavior_TurnsAfterMovingRegardlessOfBlocked()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		host.NextMoveResult = EnemyActionResult.Blocked;

		new TurningWalkerBehavior(turnRight: true).TakeTurn(
			host, null, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(host.TurnRightCallCount, Is.EqualTo(1));
		Assert.That(host.TurnLeftCallCount, Is.EqualTo(0));
	}

	[Test]
	public void TurningWalkerBehavior_AttackConsumesTheWholeBeatWithNoTurn()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		host.NextMoveResult = EnemyActionResult.Attacked("Bite", null);

		EnemyActionResult result = new TurningWalkerBehavior(turnRight: false).TakeTurn(
			host, null, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Attacked));
		Assert.That(result.AttackName, Is.EqualTo("Bite"));
		Assert.That(host.TurnLeftCallCount, Is.EqualTo(0));
		Assert.That(host.TurnRightCallCount, Is.EqualTo(0));
	}

	[Test]
	public void StationaryBehavior_NeverActs()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));

		EnemyActionResult result = new StationaryBehavior().TakeTurn(
			host, null, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Idle));
		Assert.That(host.MoveForwardCallCount, Is.EqualTo(0));
		Assert.That(host.TurnLeftCallCount, Is.EqualTo(0));
		Assert.That(host.TurnRightCallCount, Is.EqualTo(0));
	}

	private sealed class FakeCombatant : ICombatant
	{
		public System.Guid InstanceId { get; } = System.Guid.NewGuid();
		public GridPosition GridPosition { get; }
		public CombatFaction Faction => CombatFaction.Player;
		public bool IsAlive => true;
		public int Health => 1;

		public FakeCombatant(GridPosition gridPosition)
		{
			GridPosition = gridPosition;
		}

		public void TakeDamage(int damage)
		{
		}

		public void Knockback(Vector2 direction)
		{
		}
	}

	private sealed class FakeMovementHost : IEnemyMovementHost
	{
		public GridPosition GridPosition { get; }
		public Vector2 FacingDirection { get; private set; }
		public bool HasPreparedMove { get; private set; }
		public EnemyActionResult NextMoveResult { get; set; } = EnemyActionResult.Moved;
		public int MoveForwardCallCount { get; private set; }
		public int TurnLeftCallCount { get; private set; }
		public int TurnRightCallCount { get; private set; }
		public Func<GridPosition, bool> IsWallAtFunc { get; set; } = _ => false;

		public FakeMovementHost(GridPosition gridPosition)
		{
			GridPosition = gridPosition;
		}

		public bool IsWallAt(GridPosition position) => IsWallAtFunc(position);

		public void SetFacingDirection(Vector2 direction)
		{
			FacingDirection = direction;
		}

		public void SetFacingIndicatorVisible(bool visible)
		{
		}

		public void SetHasPreparedMove(bool hasPreparedMove)
		{
			HasPreparedMove = hasPreparedMove;
		}

		public void TurnLeft()
		{
			TurnLeftCallCount++;
		}

		public void TurnRight()
		{
			TurnRightCallCount++;
		}

		public EnemyActionResult TryMoveForward(
			HashSet<GridPosition> occupiedEnemyPositions,
			IReadOnlyList<ICombatant> combatants)
		{
			MoveForwardCallCount++;
			return NextMoveResult;
		}
	}
}
