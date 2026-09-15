using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

// JSON shape a mod author writes under mods/<mod-id>/monsters/*.json. Field
// names are deserialized case-insensitively, so "sprite" or "Sprite" both work.
public sealed class MonsterModJson
{
	public string Id { get; set; }
	public string Name { get; set; }
	public int Health { get; set; }
	public string Sprite { get; set; }
	public string Movement { get; set; }
	public List<MonsterAttackJson> Attacks { get; set; }
	public string LootTable { get; set; }
}

public sealed class MonsterAttackJson
{
	public string Pattern { get; set; }
	public int Damage { get; set; }
	public string Effect { get; set; }
}

public sealed class MonsterModLoadResult
{
	public IReadOnlyList<MonsterDefinition> Monsters { get; }
	public IReadOnlyList<string> Errors { get; }

	public MonsterModLoadResult(
		IReadOnlyList<MonsterDefinition> monsters,
		IReadOnlyList<string> errors)
	{
		Monsters = monsters;
		Errors = errors;
	}
}

// Loads data-only monster mods from disk:
//
//   mods/
//   └── fire-rat/
//       ├── mod.json           (currently informational only)
//       ├── monsters/
//       │   └── fire_rat.json
//       └── sprites/
//           └── fire_rat.png
//
// A bad monster file is disabled with a readable error rather than crashing
// the loader or the game — see the validation checks below. This class is
// deliberately Godot-free (plain System.IO/System.Text.Json) so it can be
// unit tested the same way DigResolver is; texture loading happens
// separately in MonsterSpriteLoader once a definition is actually used.
public static class MonsterModLoader
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public static MonsterModLoadResult LoadFromDirectory(string modsRootPath)
	{
		List<MonsterDefinition> monsters = new();
		List<string> errors = new();

		if (string.IsNullOrWhiteSpace(modsRootPath) || !Directory.Exists(modsRootPath))
			return new MonsterModLoadResult(monsters, errors);

		foreach (string modFolder in Directory.GetDirectories(modsRootPath))
		{
			string monstersFolder = Path.Combine(modFolder, "monsters");

			if (!Directory.Exists(monstersFolder))
				continue;

			foreach (string monsterFile in Directory.GetFiles(monstersFolder, "*.json"))
				LoadMonsterFile(modFolder, monsterFile, monsters, errors);
		}

		return new MonsterModLoadResult(monsters, errors);
	}

	private static void LoadMonsterFile(
		string modFolder,
		string monsterFilePath,
		List<MonsterDefinition> monsters,
		List<string> errors)
	{
		string fileLabel = Path.GetFileName(monsterFilePath);
		MonsterModJson json;

		try
		{
			string text = File.ReadAllText(monsterFilePath);
			json = JsonSerializer.Deserialize<MonsterModJson>(text, JsonOptions);
		}
		catch (Exception ex)
		{
			errors.Add($"{fileLabel} disabled: could not parse JSON ({ex.Message}).");
			return;
		}

		if (json == null)
		{
			errors.Add($"{fileLabel} disabled: empty or invalid JSON.");
			return;
		}

		string displayName = string.IsNullOrWhiteSpace(json.Name) ? fileLabel : json.Name;

		if (string.IsNullOrWhiteSpace(json.Id))
		{
			errors.Add($"{displayName} disabled: missing required field \"id\".");
			return;
		}

		if (string.IsNullOrWhiteSpace(json.Name))
		{
			errors.Add($"{displayName} disabled: missing required field \"name\".");
			return;
		}

		if (json.Health <= 0)
		{
			errors.Add($"{displayName} disabled: \"health\" must be greater than zero.");
			return;
		}

		if (string.IsNullOrWhiteSpace(json.Sprite))
		{
			errors.Add($"{displayName} disabled: missing required field \"sprite\".");
			return;
		}

		string spritePath = Path.Combine(modFolder, "sprites", json.Sprite);

		if (!File.Exists(spritePath))
		{
			errors.Add($"{displayName} disabled: sprite file \"{json.Sprite}\" was not found.");
			return;
		}

		if (!EnemyMovementBehaviors.IsKnown(json.Movement))
		{
			errors.Add(
				$"{displayName} disabled: unknown movement behavior \"{json.Movement}\". " +
				$"Expected one of: {string.Join(", ", EnemyMovementBehaviors.KnownIds)}."
			);
			return;
		}

		if (json.Attacks == null || json.Attacks.Count == 0)
		{
			errors.Add($"{displayName} disabled: at least one attack is required.");
			return;
		}

		if (!TryBuildAttacks(displayName, json.Attacks, errors, out List<AttackDefinition> attacks))
			return;

		try
		{
			monsters.Add(new MonsterDefinition(
				id: json.Id,
				name: json.Name,
				health: json.Health,
				spritePath: spritePath,
				movementBehaviorId: json.Movement,
				attacks: attacks,
				lootTableId: json.LootTable
			));
		}
		catch (Exception ex)
		{
			errors.Add($"{displayName} disabled: {ex.Message}");
		}
	}

	private static bool TryBuildAttacks(
		string displayName,
		List<MonsterAttackJson> attackJsons,
		List<string> errors,
		out List<AttackDefinition> attacks)
	{
		attacks = new List<AttackDefinition>();

		foreach (MonsterAttackJson attackJson in attackJsons)
		{
			if (!AttackPatterns.IsKnown(attackJson.Pattern))
			{
				errors.Add(
					$"{displayName} disabled: unknown attack pattern \"{attackJson.Pattern}\". " +
					$"Expected one of: {string.Join(", ", AttackPatterns.KnownIds)}."
				);
				return false;
			}

			if (attackJson.Damage <= 0)
			{
				errors.Add($"{displayName} disabled: attack damage must be greater than zero.");
				return false;
			}

			AttackOffset[] offsets = AttackPatterns.Get(attackJson.Pattern);

			attacks.Add(new AttackDefinition(
				name: $"{displayName} ({attackJson.Pattern})",
				damage: attackJson.Damage,
				preparationTurns: 0,
				detectionOffsets: offsets,
				attackOffsets: offsets,
				targetRule: AttackTargetRule.OpponentsOnly,
				stopsAtWalls: true,
				maxTargets: 1,
				statusEffectId: attackJson.Effect
			));
		}

		return true;
	}
}
