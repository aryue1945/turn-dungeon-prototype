using Godot;
using NUnit.Framework;

[TestFixture]
public sealed class ActorRestoreTests
{
	[Test]
	public void RestoreActor_MatchesPositionHealthFacingAndEquipment()
	{
		ActorState original = new(new GridPosition(3, 4), maxHealth: 5, "core.player");
		original.TakeDamage(2);
		original.SetFacing(Vector2.Up);
		original.SetHasPreparedMove(true);
		original.SetEquipment("core.basic_sword", "core.basic_shovel");
		original.SetAttack(new AttackState(WeaponDefinitions.BasicSword.PrimaryAttack));

		ActorSnapshot snapshot = CaptureFor(original);
		ActorState restored = GameSnapshotRestore.RestoreActor(
			snapshot,
			new AttackState(WeaponDefinitions.BasicSword.PrimaryAttack)
		);

		Assert.That(restored.GridPosition, Is.EqualTo(original.GridPosition));
		Assert.That(restored.Health, Is.EqualTo(original.Health));
		Assert.That(restored.MaxHealth, Is.EqualTo(original.MaxHealth));
		Assert.That(restored.DefinitionId, Is.EqualTo(original.DefinitionId));
		Assert.That(restored.Facing, Is.EqualTo(Vector2.Up));
		Assert.That(restored.HasPreparedMove, Is.True);
		Assert.That(restored.WeaponId, Is.EqualTo("core.basic_sword"));
		Assert.That(restored.ToolId, Is.EqualTo("core.basic_shovel"));
	}

	[Test]
	public void RestoreActor_RestoresFullHealthWithoutCallingTakeDamage()
	{
		ActorState original = new(new GridPosition(0, 0), maxHealth: 3, "core.player");
		original.SetAttack(new AttackState(WeaponDefinitions.BasicSword.PrimaryAttack));

		ActorSnapshot snapshot = CaptureFor(original);
		ActorState restored = GameSnapshotRestore.RestoreActor(
			snapshot,
			new AttackState(WeaponDefinitions.BasicSword.PrimaryAttack)
		);

		Assert.That(restored.Health, Is.EqualTo(3));
		Assert.That(restored.IsAlive, Is.True);
	}

	[Test]
	public void RestoreActor_RestoresMidPreparationAttack()
	{
		ActorState original = new(new GridPosition(0, 0), maxHealth: 3, "core.test_enemy");
		AttackDefinition chargedStrike = new(
			name: "Charged Strike",
			damage: 3,
			preparationTurns: 2,
			detectionOffsets: new[] { new AttackOffset(1, 0) },
			attackOffsets: new[] { new AttackOffset(1, 0) },
			targetRule: AttackTargetRule.OpponentsOnly,
			stopsAtWalls: true,
			maxTargets: 1
		);
		AttackState attack = new(chargedStrike);
		attack.BeginPreparation(Vector2.Left);
		attack.AdvancePreparation();
		original.SetAttack(attack);

		ActorSnapshot snapshot = CaptureFor(original);
		AttackState restoredAttack = new(chargedStrike);
		ActorState restored = GameSnapshotRestore.RestoreActor(snapshot, restoredAttack);

		Assert.That(restored.Attack.IsPreparing, Is.True);
		Assert.That(restored.Attack.RemainingPreparationTurns, Is.EqualTo(1));
		Assert.That(restored.Attack.PreparedDirection, Is.EqualTo(Vector2.Left));
	}

	[Test]
	public void RestoreActor_RejectsNullSnapshotOrAttack()
	{
		ActorSnapshot snapshot = CaptureFor(
			new ActorState(new GridPosition(0, 0), maxHealth: 1, "core.player")
		);
		AttackState attack = new(WeaponDefinitions.BasicSword.PrimaryAttack);

		System.Action nullSnapshot = () => GameSnapshotRestore.RestoreActor(null, attack);
		System.Action nullAttack = () => GameSnapshotRestore.RestoreActor(snapshot, null);

		Assert.Throws<System.ArgumentNullException>(nullSnapshot);
		Assert.Throws<System.ArgumentNullException>(nullAttack);
	}

	private static ActorSnapshot CaptureFor(ActorState player)
	{
		DungeonMap map = new DungeonGenerator().Generate(
			new DungeonGenerationRequest(24, 16, 5, seed: 1)
		);
		GameState state = new(map, player);
		return GameSnapshot.Capture(state).Player;
	}
}
