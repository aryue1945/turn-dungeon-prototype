using Godot;
using NUnit.Framework;

[TestFixture]
public sealed class ActorStateTests
{
	private const string TestDefinitionId = "test.actor";

	[Test]
	public void Constructor_SetsGridPositionAndFullHealth()
	{
		ActorState state = CreateState(new GridPosition(2, 3), maxHealth: 5);

		Assert.That(state.GridPosition, Is.EqualTo(new GridPosition(2, 3)));
		Assert.That(state.Health, Is.EqualTo(5));
		Assert.That(state.MaxHealth, Is.EqualTo(5));
		Assert.That(state.IsAlive, Is.True);
	}

	[Test]
	public void Constructor_SetsDefinitionIdAndAUniqueInstanceId()
	{
		ActorState first = CreateState(new GridPosition(0, 0), maxHealth: 1);
		ActorState second = CreateState(new GridPosition(0, 0), maxHealth: 1);

		Assert.That(first.DefinitionId, Is.EqualTo(TestDefinitionId));
		Assert.That(first.InstanceId, Is.Not.EqualTo(System.Guid.Empty));
		Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
	}

	[Test]
	public void Constructor_DefaultsFacingAndHasPreparedMove()
	{
		ActorState state = CreateState(new GridPosition(0, 0), maxHealth: 1);

		Assert.That(state.Facing, Is.EqualTo(Vector2.Zero));
		Assert.That(state.HasPreparedMove, Is.False);
	}

	[Test]
	public void Constructor_RejectsNonPositiveMaxHealth()
	{
		System.Action construct = () =>
			new ActorState(new GridPosition(0, 0), maxHealth: 0, TestDefinitionId);

		Assert.Throws<System.ArgumentOutOfRangeException>(construct);
	}

	[Test]
	public void Constructor_RejectsBlankDefinitionId()
	{
		System.Action construct = () =>
			new ActorState(new GridPosition(0, 0), maxHealth: 1, "   ");

		Assert.Throws<System.ArgumentException>(construct);
	}

	[Test]
	public void SetFacing_ReplacesFacing()
	{
		ActorState state = CreateState(new GridPosition(0, 0), maxHealth: 1);

		state.SetFacing(Vector2.Up);

		Assert.That(state.Facing, Is.EqualTo(Vector2.Up));
	}

	[Test]
	public void SetHasPreparedMove_TogglesFlag()
	{
		ActorState state = CreateState(new GridPosition(0, 0), maxHealth: 1);

		state.SetHasPreparedMove(true);
		Assert.That(state.HasPreparedMove, Is.True);

		state.SetHasPreparedMove(false);
		Assert.That(state.HasPreparedMove, Is.False);
	}

	[Test]
	public void MoveTo_ReplacesGridPosition()
	{
		ActorState state = CreateState(new GridPosition(0, 0), maxHealth: 1);

		state.MoveTo(new GridPosition(7, 4));

		Assert.That(state.GridPosition, Is.EqualTo(new GridPosition(7, 4)));
	}

	[Test]
	public void MoveBy_OffsetsGridPosition()
	{
		ActorState state = CreateState(new GridPosition(5, 5), maxHealth: 1);

		state.MoveBy(-1, 0);
		state.MoveBy(0, 1);

		Assert.That(state.GridPosition, Is.EqualTo(new GridPosition(4, 6)));
	}

	[Test]
	public void TakeDamage_ClampsAtZeroAndUpdatesIsAlive()
	{
		ActorState state = CreateState(new GridPosition(0, 0), maxHealth: 3);

		state.TakeDamage(2);
		Assert.That(state.Health, Is.EqualTo(1));
		Assert.That(state.IsAlive, Is.True);

		state.TakeDamage(99);
		Assert.That(state.Health, Is.EqualTo(0));
		Assert.That(state.IsAlive, Is.False);
	}

	[Test]
	public void TakeDamage_IgnoresNonPositiveDamageAndDamageAfterDeath()
	{
		ActorState state = CreateState(new GridPosition(0, 0), maxHealth: 2);

		state.TakeDamage(0);
		state.TakeDamage(-5);
		Assert.That(state.Health, Is.EqualTo(2));

		state.TakeDamage(10);
		Assert.That(state.Health, Is.EqualTo(0));

		state.TakeDamage(1);
		Assert.That(state.Health, Is.EqualTo(0), "Damage after death must not go negative.");
	}

	private static ActorState CreateState(GridPosition gridPosition, int maxHealth)
	{
		return new ActorState(gridPosition, maxHealth, TestDefinitionId);
	}
}
