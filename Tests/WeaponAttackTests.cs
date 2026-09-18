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
	public void BasicSword_AttackDetailRecordsAttackerTargetDamageAndHealth()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(1, 0));

		AttackResolver.TryAttack(
			player,
			Vector2.Right,
			new AttackState(WeaponDefinitions.BasicSword.PrimaryAttack),
			new List<ICombatant> { player, enemy },
			_ => false,
			out AttackExecutionDetail detail
		);

		Assert.That(detail.AttackerInstanceId, Is.EqualTo(player.InstanceId));
		Assert.That(detail.AffectedCells, Is.EqualTo(new List<GridPosition> { new(1, 0) }));
		Assert.That(detail.Hits, Has.Count.EqualTo(1));
		Assert.That(detail.Hits[0].TargetInstanceId, Is.EqualTo(enemy.InstanceId));
		Assert.That(detail.Hits[0].Damage, Is.EqualTo(1));
		Assert.That(detail.Hits[0].RemainingHealth, Is.EqualTo(2));
		Assert.That(detail.Hits[0].Defeated, Is.False);
	}

	[Test]
	public void BasicSword_AttackDetailMarksTargetDefeatedAtZeroHealth()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(1, 0));
		enemy.SetHealth(1);

		AttackResolver.TryAttack(
			player,
			Vector2.Right,
			new AttackState(WeaponDefinitions.BasicSword.PrimaryAttack),
			new List<ICombatant> { player, enemy },
			_ => false,
			out AttackExecutionDetail detail
		);

		Assert.That(detail.Hits[0].RemainingHealth, Is.EqualTo(0));
		Assert.That(detail.Hits[0].Defeated, Is.True);
	}

	[Test]
	public void BasicSword_NoDetectionTargetLeavesDetailNull()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));

		AttackTurnResult result = AttackResolver.TryAttack(
			player,
			Vector2.Right,
			new AttackState(WeaponDefinitions.BasicSword.PrimaryAttack),
			new List<ICombatant> { player },
			_ => false,
			out AttackExecutionDetail detail
		);

		Assert.That(result, Is.EqualTo(AttackTurnResult.NoAttack));
		Assert.That(detail, Is.Null, "No detection target means no attack executes at all.");
	}

	[Test]
	public void WarHammer_PushesSurvivingTargetOneCellBack()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(1, 0));

		AttackTurnResult result = AttackResolver.TryAttack(
			player,
			Vector2.Right,
			new AttackState(WeaponDefinitions.WarHammer.PrimaryAttack),
			new List<ICombatant> { player, enemy },
			_ => false,
			out AttackExecutionDetail detail
		);

		Assert.That(result, Is.EqualTo(AttackTurnResult.Attacked));
		Assert.That(enemy.Health, Is.EqualTo(2));
		Assert.That(enemy.GridPosition, Is.EqualTo(new GridPosition(2, 0)));
		Assert.That(detail.Hits[0].KnockedBackTo, Is.EqualTo(new GridPosition(2, 0)));
	}

	[Test]
	public void WarHammer_BlockedByWallStillDamagesButDoesNotMove()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(1, 0));
		HashSet<GridPosition> walls = new() { new GridPosition(2, 0) };

		AttackResolver.TryAttack(
			player,
			Vector2.Right,
			new AttackState(WeaponDefinitions.WarHammer.PrimaryAttack),
			new List<ICombatant> { player, enemy },
			walls.Contains,
			out AttackExecutionDetail detail
		);

		Assert.That(enemy.Health, Is.EqualTo(2), "Damage still applies even when the push is blocked.");
		Assert.That(enemy.GridPosition, Is.EqualTo(new GridPosition(1, 0)), "A wall behind the target blocks the push.");
		Assert.That(detail.Hits[0].KnockedBackTo, Is.Null);
	}

	[Test]
	public void WarHammer_BlockedByAnotherActorStillDamagesButDoesNotMove()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(1, 0));
		FakeCombatant bystander = EnemyAt(new GridPosition(2, 0));

		AttackResolver.TryAttack(
			player,
			Vector2.Right,
			new AttackState(WeaponDefinitions.WarHammer.PrimaryAttack),
			new List<ICombatant> { player, enemy, bystander },
			_ => false,
			out AttackExecutionDetail detail
		);

		Assert.That(enemy.Health, Is.EqualTo(2));
		Assert.That(enemy.GridPosition, Is.EqualTo(new GridPosition(1, 0)));
		Assert.That(detail.Hits[0].KnockedBackTo, Is.Null);
	}

	[Test]
	public void WarHammer_DefeatedTargetIsNotKnockedBack()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(1, 0));
		enemy.SetHealth(1);

		AttackResolver.TryAttack(
			player,
			Vector2.Right,
			new AttackState(WeaponDefinitions.WarHammer.PrimaryAttack),
			new List<ICombatant> { player, enemy },
			_ => false,
			out AttackExecutionDetail detail
		);

		Assert.That(enemy.Health, Is.EqualTo(0));
		Assert.That(enemy.GridPosition, Is.EqualTo(new GridPosition(1, 0)));
		Assert.That(detail.Hits[0].Defeated, Is.True);
		Assert.That(detail.Hits[0].KnockedBackTo, Is.Null);
	}

	[Test]
	public void BasicSword_DoesNotKnockBackTheTarget()
	{
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(1, 0));

		UseWeapon(WeaponDefinitions.BasicSword, player, new[] { enemy });

		Assert.That(enemy.GridPosition, Is.EqualTo(new GridPosition(1, 0)));
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

	// Regression coverage (NEXT_STEPS step 5, slice a): the existing
	// direction-specific tests above only exercise Vector2.Right (and one
	// Vector2.Up case). These cover all four cardinal directions so a
	// future change to GetPatternPosition's rotation math cannot silently
	// break Up/Down/Left while Right still passes.
	[TestCase(1, 0)]
	[TestCase(-1, 0)]
	[TestCase(0, 1)]
	[TestCase(0, -1)]
	public void BasicSword_HitsEnemyOneCellForward_InEveryCardinalDirection(int dx, int dy)
	{
		Vector2 direction = new(dx, dy);
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(dx, dy));

		AttackTurnResult result = UseWeapon(
			WeaponDefinitions.BasicSword,
			player,
			new[] { enemy },
			direction: direction
		);

		Assert.That(result, Is.EqualTo(AttackTurnResult.Attacked));
		Assert.That(enemy.Health, Is.EqualTo(2));
	}

	[TestCase(1, 0)]
	[TestCase(-1, 0)]
	[TestCase(0, 1)]
	[TestCase(0, -1)]
	public void LongSword_HitsEnemyTwoCellsForward_InEveryCardinalDirection(int dx, int dy)
	{
		Vector2 direction = new(dx, dy);
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(dx * 2, dy * 2));

		AttackTurnResult result = UseWeapon(
			WeaponDefinitions.LongSword,
			player,
			new[] { enemy },
			direction: direction
		);

		Assert.That(result, Is.EqualTo(AttackTurnResult.Attacked));
		Assert.That(enemy.Health, Is.EqualTo(2));
	}

	[TestCase(1, 0)]
	[TestCase(-1, 0)]
	[TestCase(0, 1)]
	[TestCase(0, -1)]
	public void WarHammer_PushesSurvivingTargetOneCellBack_InEveryCardinalDirection(int dx, int dy)
	{
		Vector2 direction = new(dx, dy);
		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant enemy = EnemyAt(new GridPosition(dx, dy));

		AttackResolver.TryAttack(
			player,
			direction,
			new AttackState(WeaponDefinitions.WarHammer.PrimaryAttack),
			new List<ICombatant> { player, enemy },
			_ => false,
			out AttackExecutionDetail detail
		);

		GridPosition expectedDestination = new(dx * 2, dy * 2);
		Assert.That(enemy.Health, Is.EqualTo(2));
		Assert.That(enemy.GridPosition, Is.EqualTo(expectedDestination));
		Assert.That(detail.Hits[0].KnockedBackTo, Is.EqualTo(expectedDestination));
	}

	// Every built-in weapon currently only hits AttackOffset(Forward, 0) -
	// Right is always zero, so a rotation bug in the sideways axis would
	// pass every test above. This uses a custom pattern with a nonzero
	// Right component (front-left, AttackOffset(1, -1)) to prove the
	// sideways offset rotates correctly with facing, not just Forward.
	[TestCase(1, 0, /* right in world space when facing +X */ 0, 1)]
	[TestCase(-1, 0, 0, -1)]
	[TestCase(0, 1, -1, 0)]
	[TestCase(0, -1, 1, 0)]
	public void CustomFrontLeftOffset_HitsCorrectCell_InEveryCardinalDirection(
		int facingX, int facingY, int worldRightX, int worldRightY)
	{
		Vector2 facing = new(facingX, facingY);
		AttackDefinition frontLeft = new(
			name: "Front-Left Test Pattern",
			damage: 1,
			preparationTurns: 0,
			detectionOffsets: new[] { new AttackOffset(1, -1) },
			attackOffsets: new[] { new AttackOffset(1, -1) },
			targetRule: AttackTargetRule.OpponentsOnly,
			stopsAtWalls: true,
			maxTargets: 1
		);

		// Front-left of `facing` is one cell forward, minus one cell of
		// world-right (i.e. plus one cell of world-left).
		GridPosition frontLeftCell = new(
			facingX - worldRightX,
			facingY - worldRightY
		);
		// Directly forward must NOT be hit by a pattern with Right = -1.
		GridPosition straightAheadCell = new(facingX, facingY);

		FakeCombatant player = PlayerAt(new GridPosition(0, 0));
		FakeCombatant frontLeftEnemy = EnemyAt(frontLeftCell);
		FakeCombatant straightAheadEnemy = EnemyAt(straightAheadCell);

		AttackTurnResult result = AttackResolver.TryAttack(
			player,
			facing,
			new AttackState(frontLeft),
			new List<ICombatant> { player, frontLeftEnemy, straightAheadEnemy },
			_ => false,
			out AttackExecutionDetail detail
		);

		Assert.That(result, Is.EqualTo(AttackTurnResult.Attacked));
		Assert.That(frontLeftEnemy.Health, Is.EqualTo(2), "The front-left cell should be hit.");
		Assert.That(straightAheadEnemy.Health, Is.EqualTo(3), "Straight ahead is a different cell and must not be hit.");
		Assert.That(detail.AffectedCells, Is.EqualTo(new List<GridPosition> { frontLeftCell }));
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
			walls.Contains,
			out _
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
		public System.Guid InstanceId { get; } = System.Guid.NewGuid();
		public GridPosition GridPosition { get; private set; }
		public CombatFaction Faction { get; }
		public int Health { get; private set; } = 3;
		public bool IsAlive => Health > 0;

		public FakeCombatant(GridPosition gridPosition, CombatFaction faction)
		{
			GridPosition = gridPosition;
			Faction = faction;
		}

		public void Knockback(Vector2 direction)
		{
			GridPosition = new GridPosition(
				GridPosition.X + (int)direction.X,
				GridPosition.Y + (int)direction.Y
			);
		}

		public void TakeDamage(int damage)
		{
			Health = System.Math.Max(0, Health - damage);
		}

		public void SetHealth(int health)
		{
			Health = health;
		}
	}
}
