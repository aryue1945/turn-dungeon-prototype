using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// Compact description of one attack pattern offset, for the debug export -
// enough to reconstruct where an attack reaches without embedding the
// executable AttackOffset struct itself.
public sealed class AttackOffsetSummary
{
	public int Forward { get; }
	public int Right { get; }

	public AttackOffsetSummary(int forward, int right)
	{
		Forward = forward;
		Right = right;
	}

	public static AttackOffsetSummary From(AttackOffset offset) =>
		new(offset.Forward, offset.Right);
}

// Compact, read-only description of one AttackDefinition - enough to
// understand a custom weapon/monster attack's behavior from the export
// alone, per docs/SAVE_AND_DEBUG_HISTORY.md: "compact summaries... not
// executable mod code".
public sealed class AttackSummary
{
	public string Name { get; }
	public int Damage { get; }
	public int PreparationTurns { get; }
	public string TargetRule { get; }
	public bool StopsAtWalls { get; }
	public int MaxTargets { get; }
	public IReadOnlyList<AttackOffsetSummary> DetectionOffsets { get; }
	public IReadOnlyList<AttackOffsetSummary> AttackOffsets { get; }
	public string StatusEffectId { get; }

	public AttackSummary(
		string name,
		int damage,
		int preparationTurns,
		string targetRule,
		bool stopsAtWalls,
		int maxTargets,
		IReadOnlyList<AttackOffsetSummary> detectionOffsets,
		IReadOnlyList<AttackOffsetSummary> attackOffsets,
		string statusEffectId)
	{
		Name = name;
		Damage = damage;
		PreparationTurns = preparationTurns;
		TargetRule = targetRule;
		StopsAtWalls = stopsAtWalls;
		MaxTargets = maxTargets;
		DetectionOffsets = detectionOffsets;
		AttackOffsets = attackOffsets;
		StatusEffectId = statusEffectId;
	}

	public static AttackSummary From(AttackDefinition definition)
	{
		return new AttackSummary(
			definition.Name,
			definition.Damage,
			definition.PreparationTurns,
			definition.TargetRule.ToString(),
			definition.StopsAtWalls,
			definition.MaxTargets,
			definition.DetectionOffsets.Select(AttackOffsetSummary.From).ToList(),
			definition.AttackOffsets.Select(AttackOffsetSummary.From).ToList(),
			definition.StatusEffectId
		);
	}
}

public sealed class WeaponSummary
{
	public string Id { get; }
	public string Name { get; }
	public AttackSummary PrimaryAttack { get; }

	public WeaponSummary(string id, string name, AttackSummary primaryAttack)
	{
		Id = id;
		Name = name;
		PrimaryAttack = primaryAttack;
	}

	public static WeaponSummary From(WeaponDefinition weapon) =>
		new(weapon.Id, weapon.Name, AttackSummary.From(weapon.PrimaryAttack));
}

public sealed class MonsterSummary
{
	public string Id { get; }
	public string Name { get; }
	public int Health { get; }
	public string MovementBehaviorId { get; }
	public IReadOnlyList<AttackSummary> Attacks { get; }

	public MonsterSummary(
		string id,
		string name,
		int health,
		string movementBehaviorId,
		IReadOnlyList<AttackSummary> attacks)
	{
		Id = id;
		Name = name;
		Health = health;
		MovementBehaviorId = movementBehaviorId;
		Attacks = attacks;
	}

	public static MonsterSummary From(MonsterDefinition monster) =>
		new(
			monster.Id,
			monster.Name,
			monster.Health,
			monster.MovementBehaviorId,
			monster.Attacks.Select(AttackSummary.From).ToList()
		);
}

// Identity/configuration and content context for one debug-history export,
// gathered by the caller (Main) since only it knows the run's seed, spawn
// pool and weapon roster - see docs/SAVE_AND_DEBUG_HISTORY.md's Identity/
// Configuration table. Fields the game has no real concept of yet
// (build/commit, floor id) stay null rather than inventing a fake value.
public sealed class DebugHistoryExportContext
{
	public Guid RunId { get; }
	public int Seed { get; }
	public string FloorId { get; }
	public string BuildVersion { get; }
	public IReadOnlyList<WeaponSummary> Weapons { get; }
	public IReadOnlyList<MonsterSummary> Monsters { get; }

	public DebugHistoryExportContext(
		Guid runId,
		int seed,
		string floorId,
		string buildVersion,
		IReadOnlyList<WeaponSummary> weapons,
		IReadOnlyList<MonsterSummary> monsters)
	{
		RunId = runId;
		Seed = seed;
		FloorId = floorId;
		BuildVersion = buildVersion;
		Weapons = weapons ?? Array.Empty<WeaponSummary>();
		Monsters = monsters ?? Array.Empty<MonsterSummary>();
	}

	// A short, order-independent fingerprint of the current weapon/monster
	// roster's gameplay-relevant fields, so an export can flag "this was
	// generated against different content" without embedding executable mod
	// code (docs/SAVE_AND_DEBUG_HISTORY.md).
	public string ComputeContentFingerprint()
	{
		StringBuilder builder = new();

		foreach (WeaponSummary weapon in Weapons.OrderBy(w => w.Id, StringComparer.Ordinal))
			AppendAttack(builder, weapon.Id, weapon.PrimaryAttack);

		foreach (MonsterSummary monster in Monsters.OrderBy(m => m.Id, StringComparer.Ordinal))
		{
			builder.Append(monster.Id).Append('|').Append(monster.Health).Append('|')
				.Append(monster.MovementBehaviorId).Append(';');

			foreach (AttackSummary attack in monster.Attacks)
				AppendAttack(builder, monster.Id, attack);
		}

		byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
		return Convert.ToHexString(hash)[..16];
	}

	private static void AppendAttack(StringBuilder builder, string ownerId, AttackSummary attack)
	{
		builder.Append(ownerId).Append('|').Append(attack.Name).Append('|')
			.Append(attack.Damage).Append('|').Append(attack.PreparationTurns).Append('|')
			.Append(attack.TargetRule).Append('|').Append(attack.StopsAtWalls).Append('|')
			.Append(attack.MaxTargets).Append(';');
	}
}

// The full export payload: identity/config, content summaries, the snapshot
// immediately before the first exported transition, and the transitions
// themselves - per docs/SAVE_AND_DEBUG_HISTORY.md and the export-menu spec
// ("exporting turns 7-11 must contain state 6 followed by transitions
// 7-11").
public sealed class DebugHistoryExport
{
	public const int CurrentSchemaVersion = 2;

	public int SchemaVersion { get; }
	public Guid RunId { get; }
	public int Seed { get; }
	public string FloorId { get; }
	public string BuildVersion { get; }
	public string ContentFingerprint { get; }
	public IReadOnlyList<WeaponSummary> Weapons { get; }
	public IReadOnlyList<MonsterSummary> Monsters { get; }
	public GameSnapshot BeforeSnapshot { get; }
	public IReadOnlyList<TurnTransition> Transitions { get; }

	public DebugHistoryExport(
		int schemaVersion,
		Guid runId,
		int seed,
		string floorId,
		string buildVersion,
		string contentFingerprint,
		IReadOnlyList<WeaponSummary> weapons,
		IReadOnlyList<MonsterSummary> monsters,
		GameSnapshot beforeSnapshot,
		IReadOnlyList<TurnTransition> transitions)
	{
		SchemaVersion = schemaVersion;
		RunId = runId;
		Seed = seed;
		FloorId = floorId;
		BuildVersion = buildVersion;
		ContentFingerprint = contentFingerprint;
		Weapons = weapons;
		Monsters = monsters;
		BeforeSnapshot = beforeSnapshot ?? throw new ArgumentNullException(nameof(beforeSnapshot));
		Transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
	}
}
