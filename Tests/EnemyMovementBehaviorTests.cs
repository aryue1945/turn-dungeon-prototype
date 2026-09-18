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
	public void ChargingBeetleBehavior_FirstTurnPreparesTowardThePlayerInRange()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		FakeCombatant player = new(new GridPosition(0, 2));

		EnemyActionResult result = new ChargingBeetleBehavior().TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Prepared));
		Assert.That(host.HasPreparedMove, Is.True);
		Assert.That(host.FacingDirection, Is.EqualTo(Vector2.Down));
		Assert.That(host.MoveForwardCallCount, Is.EqualTo(0));
	}

	[TestCase(0, -2, TestName = "ChargingBeetleBehavior_DetectsThePlayerAbove")]
	[TestCase(0, 2, TestName = "ChargingBeetleBehavior_DetectsThePlayerBelow")]
	[TestCase(-2, 0, TestName = "ChargingBeetleBehavior_DetectsThePlayerToTheLeft")]
	[TestCase(2, 0, TestName = "ChargingBeetleBehavior_DetectsThePlayerToTheRight")]
	public void ChargingBeetleBehavior_DetectsAClearLaneInEveryCardinalDirection(int playerX, int playerY)
	{
		// Detection is computed fresh from the beetle's and player's grid
		// positions every turn - it never reads the beetle's current
		// facing, so a lane in any of the four cardinal directions is found
		// regardless of which way the beetle happened to be facing before
		// this turn (InitialFacingDirection is Down, left unrelated here).
		FakeMovementHost host = new(new GridPosition(0, 0));
		FakeCombatant player = new(new GridPosition(playerX, playerY));

		EnemyActionResult result = new ChargingBeetleBehavior().TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Prepared));
		Assert.That(host.FacingDirection, Is.EqualTo(new Vector2(playerX, playerY).Normalized()));
	}

	[Test]
	public void ChargingBeetleBehavior_SecondTurnChargesTwoCellsWhenClear()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		FakeCombatant player = new(new GridPosition(0, 2));
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
		FakeCombatant player = new(new GridPosition(0, 2));
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
	public void ChargingBeetleBehavior_DoesNotChargeAPlayerBeyondChargeRange()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		FakeCombatant player = new(new GridPosition(0, 3));
		host.NextMoveResult = EnemyActionResult.Moved;

		EnemyActionResult result = new ChargingBeetleBehavior().TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.Not.EqualTo(EnemyActionKind.Prepared));
		Assert.That(host.HasPreparedMove, Is.False, "Out of charge range - this should wander, not telegraph.");
	}

	[Test]
	public void ChargingBeetleBehavior_DoesNotChargeThroughAWallOnTheSameRow()
	{
		FakeMovementHost host = new(new GridPosition(0, 0))
		{
			// A wall sits between the beetle and the player on their shared
			// row, within charge range, so the lane is not clear even
			// though they are on-axis and in range.
			IsWallAtFunc = position => position == new GridPosition(1, 0)
		};
		FakeCombatant player = new(new GridPosition(2, 0));
		host.NextMoveResult = EnemyActionResult.Moved;

		EnemyActionResult result = new ChargingBeetleBehavior().TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.Not.EqualTo(EnemyActionKind.Prepared));
		Assert.That(host.HasPreparedMove, Is.False);
	}

	[Test]
	public void ChargingBeetleBehavior_WandersIntoARandomOpenDirectionWithNoTelegraph()
	{
		FakeMovementHost host = new(new GridPosition(0, 0));
		// Off-axis - no charge lane exists no matter the range.
		FakeCombatant player = new(new GridPosition(3, 4));
		host.NextMoveResult = EnemyActionResult.Moved;
		host.RandomIndexToReturn = 2; // All four cardinal directions are open; pick the third.

		EnemyActionResult result = new ChargingBeetleBehavior().TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Moved));
		Assert.That(host.HasPreparedMove, Is.False, "A wander step needs no telegraph.");
		Assert.That(host.MoveForwardCallCount, Is.EqualTo(1), "A wander closes exactly one cell.");
		Assert.That(host.FacingDirection, Is.EqualTo(Vector2.Left), "Up, Down, Left, Right in order - index 2 is Left.");
	}

	[Test]
	public void ChargingBeetleBehavior_WanderSkipsWalledDirections()
	{
		FakeMovementHost host = new(new GridPosition(0, 0))
		{
			// Up and Right are walls; only Down and Left remain open.
			IsWallAtFunc = position =>
				position == new GridPosition(0, -1) || position == new GridPosition(1, 0)
		};
		FakeCombatant player = new(new GridPosition(3, 4));
		host.NextMoveResult = EnemyActionResult.Moved;
		host.RandomIndexToReturn = 1; // Second of the two remaining open directions (Down, Left).

		new ChargingBeetleBehavior().TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(host.FacingDirection, Is.EqualTo(Vector2.Left));
	}

	[Test]
	public void ChargingBeetleBehavior_IdlesWhenWalledInOnEveryCardinalSide()
	{
		FakeMovementHost host = new(new GridPosition(0, 0))
		{
			IsWallAtFunc = position =>
				position == new GridPosition(1, 0) ||
				position == new GridPosition(-1, 0) ||
				position == new GridPosition(0, 1) ||
				position == new GridPosition(0, -1)
		};
		FakeCombatant player = new(new GridPosition(3, 4));

		EnemyActionResult result = new ChargingBeetleBehavior().TakeTurn(
			host, player, new HashSet<GridPosition>(), new List<ICombatant>());

		Assert.That(result.Kind, Is.EqualTo(EnemyActionKind.Idle));
		Assert.That(host.MoveForwardCallCount, Is.EqualTo(0));
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
		public int RandomIndexToReturn { get; set; }

		public FakeMovementHost(GridPosition gridPosition)
		{
			GridPosition = gridPosition;
		}

		public bool IsWallAt(GridPosition position) => IsWallAtFunc(position);

		public int NextRandomIndex(int exclusiveUpperBound) => RandomIndexToReturn;

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
