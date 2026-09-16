using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// Canonical, stable content fingerprinting shared by two features that both
// need it, so neither maintains its own hashing algorithm:
// DebugHistoryExportContext fingerprints the *entire current* weapon/
// monster roster for diagnostics, while RunSaveEnvelope/
// Main.TryResolveSaveDefinitions fingerprint only the weapon/tool/enemy
// definitions one specific run actually used (NEXT_STEPS milestone 5's
// save-content validation) - different scopes, same canonicalize-and-hash
// core.
public static class ContentFingerprinter
{
	// Sorts the given content-description lines (never trust the caller's
	// own enumeration order) and hashes them into a short, stable hex
	// fingerprint.
	public static string Compute(IEnumerable<string> lines)
	{
		string canonical = string.Join(
			"\n",
			lines.OrderBy(line => line, StringComparer.Ordinal)
		);

		byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
		return Convert.ToHexString(hash)[..16];
	}

	public static string DescribeAttack(string ownerId, AttackSummary attack)
	{
		return $"{ownerId}|{attack.Name}|{attack.Damage}|{attack.PreparationTurns}|" +
			$"{attack.TargetRule}|{attack.StopsAtWalls}|{attack.MaxTargets}";
	}

	public static string DescribeWeapon(WeaponSummary weapon) =>
		$"weapon|{weapon.Id}|{DescribeAttack(weapon.Id, weapon.PrimaryAttack)}";

	public static string DescribeTool(ToolSummary tool) =>
		$"tool|{tool.Id}|{tool.TerrainDamage}";

	public static string DescribeMonster(MonsterSummary monster)
	{
		StringBuilder builder = new();
		builder.Append("monster|").Append(monster.Id).Append('|').Append(monster.Health)
			.Append('|').Append(monster.MovementBehaviorId);

		foreach (AttackSummary attack in monster.Attacks)
			builder.Append(';').Append(DescribeAttack(monster.Id, attack));

		return builder.ToString();
	}

	// The scoped fingerprint save-content validation uses: only the
	// weapon/tool/enemy definitions a specific run actually referenced, so
	// adding unrelated content elsewhere can never invalidate an existing
	// save (NEXT_STEPS milestone 5).
	public static string ComputeForRun(
		WeaponSummary weapon,
		ToolSummary tool,
		IEnumerable<MonsterSummary> monsters)
	{
		List<string> lines = new() { DescribeWeapon(weapon), DescribeTool(tool) };
		lines.AddRange(monsters.Select(DescribeMonster));
		return Compute(lines);
	}
}
