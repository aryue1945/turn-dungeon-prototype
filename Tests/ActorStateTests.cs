using NUnit.Framework;

[TestFixture]
public sealed class ActorStateTests
{
	[Test]
	public void Constructor_SetsGridPositionAndFullHealth()
	{
		ActorState state = new(new GridPosition(2, 3), maxHealth: 5);

		Assert.That(state.GridPosition, Is.EqualTo(new GridPosition(2, 3)));
		Assert.That(state.Health, Is.EqualTo(5));
		Assert.That(state.MaxHealth, Is.EqualTo(5));
		Assert.That(state.IsAlive, Is.True);
	}

	[Test]
	public void Constructor_RejectsNonPositiveMaxHealth()
	{
		System.Action construct = () =>
			new ActorState(new GridPosition(0, 0), maxHealth: 0);

		Assert.Throws<System.ArgumentOutOfRangeException>(construct);
	}

	[Test]
	public void MoveTo_ReplacesGridPosition()
	{
		ActorState state = new(new GridPosition(0, 0), maxHealth: 1);

		state.MoveTo(new GridPosition(7, 4));

		Assert.That(state.GridPosition, Is.EqualTo(new GridPosition(7, 4)));
	}

	[Test]
	public void MoveBy_OffsetsGridPosition()
	{
		ActorState state = new(new GridPosition(5, 5), maxHealth: 1);

		state.MoveBy(-1, 0);
		state.MoveBy(0, 1);

		Assert.That(state.GridPosition, Is.EqualTo(new GridPosition(4, 6)));
	}

	[Test]
	public void TakeDamage_ClampsAtZeroAndUpdatesIsAlive()
	{
		ActorState state = new(new GridPosition(0, 0), maxHealth: 3);

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
		ActorState state = new(new GridPosition(0, 0), maxHealth: 2);

		state.TakeDamage(0);
		state.TakeDamage(-5);
		Assert.That(state.Health, Is.EqualTo(2));

		state.TakeDamage(10);
		Assert.That(state.Health, Is.EqualTo(0));

		state.TakeDamage(1);
		Assert.That(state.Health, Is.EqualTo(0), "Damage after death must not go negative.");
	}
}
