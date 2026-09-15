using Godot;
using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public sealed class WeaponAttackTests
{
	[Test]
	public void BasicSword_HitsEnemyOneCellForward()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(1, 0));

		AttackTurnResult result = UseWeapon(
			WeaponDefinitions.BasicSword,
			player,
			new[] { enemy }
		);

		Assert.That(result, Is.EqualTo(AttackTurnResult.Attacked));
		Assert.That(enemy.Health, Is.EqualTo(2));
	}

	[Test]
	public void BasicSword_DoesNotReachTwoCellsForward()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(2, 0));

		AttackTurnResult result = UseWeapon(
			WeaponDefinitions.BasicSword,
			player,
			new[] { enemy }
		);

		Assert.That(result, Is.EqualTo(AttackTurnResult.NoAttack));
		Assert.That(enemy.Health, Is.EqualTo(3));
	}

	[Test]
	public void LongSword_HitsEnemyTwoCellsForwardWhenFirstCellIsEmpty()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(2, 0));

		AttackTurnResult result = UseWeapon(
			WeaponDefinitions.LongSword,
			player,
			new[] { enemy }
		);

		Assert.That(result, Is.EqualTo(AttackTurnResult.Attacked));
		Assert.That(enemy.Health, Is.EqualTo(2));
	}

	[Test]
	public void LongSword_HitsOnlyNearestEnemy()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant nearEnemy = EnemyAt(new GridPosition(1, 0));
		FakeCombatant farEnemy = EnemyAt(new GridPosition(2, 0));

		AttackTurnResult result = UseWeapon(
			WeaponDefinitions.LongSword,
			player,
			new[] { nearEnemy, farEnemy }
		);

		Assert.That(result, Is.EqualTo(AttackTurnResult.Attacked));
		Assert.That(nearEnemy.Health, Is.EqualTo(2));
		Assert.That(farEnemy.Health, Is.EqualTo(3));
	}

	[Test]
	public void LongSword_CannotAttackThroughWall()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(2, 0));
		HashSet<GridPosition> walls = new()
		{
			new GridPosition(1, 0)
		};

		AttackTurnResult result = UseWeapon(
			WeaponDefinitions.LongSword,
			player,
			new[] { enemy },
			walls
		);

		Assert.That(result, Is.EqualTo(AttackTurnResult.NoAttack));
		Assert.That(enemy.Health, Is.EqualTo(3));
	}

	[Test]
	public void LongSword_RotatesPatternWithRequestedDirection()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(0, -2));

		AttackTurnResult result = UseWeapon(
			WeaponDefinitions.LongSword,
			player,
			new[] { enemy },
			direction: Vector2.Up
		);

		Assert.That(result, Is.EqualTo(AttackTurnResult.Attacked));
		Assert.That(enemy.Health, Is.EqualTo(2));
	}

	private static AttackTurnResult UseWeapon(
		WeaponDefinition weapon,
		FakeCombatant player,
		IReadOnlyList<FakeCombatant> enemies,
		HashSet<GridPosition> walls = null,
		Vector2? direction = null)
	{
		List<ICombatant> combatants = new()
		{
			player
		};

		foreach (FakeCombatant enemy in enemies)
			combatants.Add(enemy);

		walls ??= new HashSet<GridPosition>();

		return AttackResolver.TryAttack(
			player,
			direction ?? Vector2.Right,
			new AttackState(weapon.PrimaryAttack),
			combatants,
			walls.Contains
		);
	}

	private static FakeCombatant PlayerAt(GridPosition gridPosition)
	{
		return new FakeCombatant(gridPosition, CombatFaction.Player);
	}

	private static FakeCombatant EnemyAt(GridPosition gridPosition)
	{
		return new FakeCombatant(gridPosition, CombatFaction.Enemy);
	}

	private sealed class FakeCombatant : ICombatant
	{
		public GridPosition GridPosition { get; }
		public CombatFaction Faction { get; }
		public int Health { get; private set; } = 3;
		public bool IsAlive => Health > 0;

		public FakeCombatant(GridPosition gridPosition, CombatFaction faction)
		{
			GridPosition = gridPosition;
			Faction = faction;
		}

		public void TakeDamage(int damage)
		{
			Health = System.Math.Max(0, Health - damage);
		}
	}
}
