using NUnit.Framework;
using System.Collections.Generic;

// ContentFingerprinter.ComputeForRun is the exact mechanism
// Main.TryResolveSaveDefinitions uses to decide whether Continue may
// proceed (NEXT_STEPS milestone 5's save-content validation) - Main itself
// is a Godot node and not unit tested, but the fingerprint comparison it
// relies on is fully covered here.
[TestFixture]
public sealed class ContentFingerprinterTests
{
	[Test]
	public void ComputeForRun_UnchangedDefinitionsProduceTheSameFingerprint()
	{
		string saved = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			new List<MonsterSummary> { MonsterSummary.From(MonsterDefinitions.SlowChaser) }
		);

		string current = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			new List<MonsterSummary> { MonsterSummary.From(MonsterDefinitions.SlowChaser) }
		);

		Assert.That(current, Is.EqualTo(saved), "Unchanged required definitions must pass save validation.");
	}

	[Test]
	public void ComputeForRun_DifferentWeaponChangesTheFingerprint()
	{
		string saved = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			new List<MonsterSummary>()
		);

		string current = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.LongSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			new List<MonsterSummary>()
		);

		Assert.That(current, Is.Not.EqualTo(saved));
	}

	[Test]
	public void ComputeForRun_ChangedMonsterAttackDamageChangesTheFingerprint()
	{
		MonsterDefinition original = MonsterDefinitions.SlowChaser;
		MonsterDefinition changed = new(
			id: original.Id,
			name: original.Name,
			health: original.Health,
			spritePath: original.SpritePath,
			movementBehaviorId: original.MovementBehaviorId,
			attacks: new[]
			{
				new AttackDefinition(
					name: original.PrimaryAttack.Name,
					damage: original.PrimaryAttack.Damage + 1,
					preparationTurns: original.PrimaryAttack.PreparationTurns,
					detectionOffsets: original.PrimaryAttack.DetectionOffsets,
					attackOffsets: original.PrimaryAttack.AttackOffsets,
					targetRule: original.PrimaryAttack.TargetRule,
					stopsAtWalls: original.PrimaryAttack.StopsAtWalls,
					maxTargets: original.PrimaryAttack.MaxTargets
				)
			}
		);

		string saved = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			new List<MonsterSummary> { MonsterSummary.From(original) }
		);

		string current = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			new List<MonsterSummary> { MonsterSummary.From(changed) }
		);

		Assert.That(current, Is.Not.EqualTo(saved), "A changed attack shape must reject Continue.");
	}

	[Test]
	public void ComputeForRun_ChangedMovementBehaviorChangesTheFingerprint()
	{
		MonsterDefinition original = MonsterDefinitions.SlowChaser;
		MonsterDefinition changed = new(
			id: original.Id,
			name: original.Name,
			health: original.Health,
			spritePath: original.SpritePath,
			movementBehaviorId: "patrol",
			attacks: original.Attacks
		);

		string saved = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			new List<MonsterSummary> { MonsterSummary.From(original) }
		);

		string current = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			new List<MonsterSummary> { MonsterSummary.From(changed) }
		);

		Assert.That(current, Is.Not.EqualTo(saved));
	}

	[Test]
	public void ComputeForRun_IsScopedToOnlyTheGivenDefinitions()
	{
		// A run that only ever used SlowChaser must not care that Patroller
		// also exists in the game - the fingerprint is scoped to exactly
		// the definitions passed in, not the entire current roster.
		string usedOnlyChaser = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			new List<MonsterSummary> { MonsterSummary.From(MonsterDefinitions.SlowChaser) }
		);

		string sameRunRecomputedLater = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			new List<MonsterSummary> { MonsterSummary.From(MonsterDefinitions.SlowChaser) }
		);

		string wouldBeIfPatrollerWereAlsoUsed = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			new List<MonsterSummary>
			{
				MonsterSummary.From(MonsterDefinitions.SlowChaser),
				MonsterSummary.From(MonsterDefinitions.Patroller)
			}
		);

		Assert.That(sameRunRecomputedLater, Is.EqualTo(usedOnlyChaser));
		Assert.That(wouldBeIfPatrollerWereAlsoUsed, Is.Not.EqualTo(usedOnlyChaser));
	}

	[Test]
	public void ComputeForRun_IsIndependentOfMonsterListOrder()
	{
		List<MonsterSummary> forward = new()
		{
			MonsterSummary.From(MonsterDefinitions.SlowChaser),
			MonsterSummary.From(MonsterDefinitions.Patroller)
		};
		List<MonsterSummary> reversed = new()
		{
			MonsterSummary.From(MonsterDefinitions.Patroller),
			MonsterSummary.From(MonsterDefinitions.SlowChaser)
		};

		string a = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			forward
		);
		string b = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(WeaponDefinitions.BasicSword),
			ToolSummary.From(DiggingToolDefinitions.BasicShovel),
			reversed
		);

		Assert.That(b, Is.EqualTo(a));
	}
}
