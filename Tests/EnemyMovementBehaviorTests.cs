using Godot;
using NUnit.Framework;
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

		public FakeMovementHost(GridPosition gridPosition)
		{
			GridPosition = gridPosition;
		}

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
