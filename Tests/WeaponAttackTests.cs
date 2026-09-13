using Godot;
using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public sealed class WeaponAttackTests
{
	private const float TileSize = 32.0f;

	[Test]
	public void BasicSword_HitsEnemyOneCellForward()
	{
		FakeCombatant player = PlayerAt(Vector2.Zero);
		FakeCombatant enemy = EnemyAt(Vector2.Right * TileSize);

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
		FakeCombatant player = PlayerAt(Vector2.Zero);
		FakeCombatant enemy = EnemyAt(Vector2.Right * TileSize * 2);

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
		FakeCombatant player = PlayerAt(Vector2.Zero);
		FakeCombatant enemy = EnemyAt(Vector2.Right * TileSize * 2);

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
		FakeCombatant player = PlayerAt(Vector2.Zero);
		FakeCombatant nearEnemy = EnemyAt(Vector2.Right * TileSize);
		FakeCombatant farEnemy = EnemyAt(Vector2.Right * TileSize * 2);

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
		FakeCombatant player = PlayerAt(Vector2.Zero);
		FakeCombatant enemy = EnemyAt(Vector2.Right * TileSize * 2);
		HashSet<Vector2> walls = new()
		{
			Vector2.Right * TileSize
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
		FakeCombatant player = PlayerAt(Vector2.Zero);
		FakeCombatant enemy = EnemyAt(Vector2.Up * TileSize * 2);

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
		HashSet<Vector2> walls = null,
		Vector2? direction = null)
	{
		List<ICombatant> combatants = new()
		{
			player
		};

		foreach (FakeCombatant enemy in enemies)
			combatants.Add(enemy);

		walls ??= new HashSet<Vector2>();

		return AttackResolver.TryAttack(
			player,
			direction ?? Vector2.Right,
			new AttackState(weapon.PrimaryAttack),
			combatants,
			walls.Contains
		);
	}

	private static FakeCombatant PlayerAt(Vector2 position)
	{
		return new FakeCombatant(position, CombatFaction.Player);
	}

	private static FakeCombatant EnemyAt(Vector2 position)
	{
		return new FakeCombatant(position, CombatFaction.Enemy);
	}

	private sealed class FakeCombatant : ICombatant
	{
		public Vector2 Position { get; }
		public CombatFaction Faction { get; }
		public int Health { get; private set; } = 3;
		public bool IsAlive => Health > 0;

		public FakeCombatant(Vector2 position, CombatFaction faction)
		{
			Position = position;
			Faction = faction;
		}

		public void TakeDamage(int damage)
		{
			Health = System.Math.Max(0, Health - damage);
		}
	}
}
