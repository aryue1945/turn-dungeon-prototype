using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

[TestFixture]
public sealed class MonsterModLoaderTests
{
	private string _modsRoot;

	[SetUp]
	public void SetUp()
	{
		_modsRoot = Path.Combine(Path.GetTempPath(), "TurnDungeonModTests_" + Guid.NewGuid());
		Directory.CreateDirectory(_modsRoot);
	}

	[TearDown]
	public void TearDown()
	{
		if (Directory.Exists(_modsRoot))
			Directory.Delete(_modsRoot, recursive: true);
	}

	[Test]
	public void LoadFromDirectory_MissingRoot_ReturnsEmptyWithNoErrors()
	{
		MonsterModLoadResult result =
			MonsterModLoader.LoadFromDirectory(Path.Combine(_modsRoot, "does-not-exist"));

		Assert.That(result.Monsters, Is.Empty);
		Assert.That(result.Errors, Is.Empty);
	}

	[Test]
	public void LoadFromDirectory_ValidMod_ProducesMonsterDefinition()
	{
		CreateMod(
			"fire-rat",
			"fire_rat.json",
			"""
			{
				"id": "example.fire_rat",
				"name": "Fire Rat",
				"health": 4,
				"sprite": "fire_rat.png",
				"movement": "chase_player",
				"attacks": [
					{ "pattern": "adjacent", "damage": 2, "effect": "burn" }
				],
				"lootTable": "basic_monster"
			}
			""",
			spriteFileName: "fire_rat.png"
		);

		MonsterModLoadResult result = MonsterModLoader.LoadFromDirectory(_modsRoot);

		Assert.That(result.Errors, Is.Empty);
		Assert.That(result.Monsters, Has.Count.EqualTo(1));

		MonsterDefinition monster = result.Monsters[0];
		Assert.That(monster.Id, Is.EqualTo("example.fire_rat"));
		Assert.That(monster.Name, Is.EqualTo("Fire Rat"));
		Assert.That(monster.Health, Is.EqualTo(4));
		Assert.That(monster.MovementBehaviorId, Is.EqualTo("chase_player"));
		Assert.That(monster.LootTableId, Is.EqualTo("basic_monster"));
		Assert.That(monster.PrimaryAttack.Damage, Is.EqualTo(2));
		Assert.That(monster.PrimaryAttack.StatusEffectId, Is.EqualTo("burn"));
	}

	[Test]
	public void LoadFromDirectory_UnknownMovementBehavior_DisablesWithReadableError()
	{
		CreateMod(
			"fire-rat",
			"fire_rat.json",
			"""
			{
				"id": "example.fire_rat",
				"name": "Fire Rat",
				"health": 4,
				"sprite": "fire_rat.png",
				"movement": "chase_players",
				"attacks": [
					{ "pattern": "adjacent", "damage": 2 }
				]
			}
			""",
			spriteFileName: "fire_rat.png"
		);

		MonsterModLoadResult result = MonsterModLoader.LoadFromDirectory(_modsRoot);

		Assert.That(result.Monsters, Is.Empty);
		Assert.That(result.Errors, Has.Count.EqualTo(1));
		Assert.That(result.Errors[0], Does.Contain("Fire Rat disabled"));
		Assert.That(result.Errors[0], Does.Contain("unknown movement behavior \"chase_players\""));
		Assert.That(result.Errors[0], Does.Contain("chase_player"));
	}

	[Test]
	public void LoadFromDirectory_UnknownAttackPattern_DisablesWithReadableError()
	{
		CreateMod(
			"fire-rat",
			"fire_rat.json",
			"""
			{
				"id": "example.fire_rat",
				"name": "Fire Rat",
				"health": 4,
				"sprite": "fire_rat.png",
				"movement": "chase_player",
				"attacks": [
					{ "pattern": "diagonal", "damage": 2 }
				]
			}
			""",
			spriteFileName: "fire_rat.png"
		);

		MonsterModLoadResult result = MonsterModLoader.LoadFromDirectory(_modsRoot);

		Assert.That(result.Monsters, Is.Empty);
		Assert.That(result.Errors[0], Does.Contain("unknown attack pattern \"diagonal\""));
	}

	[Test]
	public void LoadFromDirectory_MissingSpriteFile_DisablesWithReadableError()
	{
		CreateMod(
			"fire-rat",
			"fire_rat.json",
			"""
			{
				"id": "example.fire_rat",
				"name": "Fire Rat",
				"health": 4,
				"sprite": "does_not_exist.png",
				"movement": "chase_player",
				"attacks": [
					{ "pattern": "adjacent", "damage": 2 }
				]
			}
			""",
			spriteFileName: null
		);

		MonsterModLoadResult result = MonsterModLoader.LoadFromDirectory(_modsRoot);

		Assert.That(result.Monsters, Is.Empty);
		Assert.That(result.Errors[0], Does.Contain("sprite file \"does_not_exist.png\" was not found"));
	}

	[Test]
	public void LoadFromDirectory_NonPositiveHealth_DisablesWithReadableError()
	{
		CreateMod(
			"fire-rat",
			"fire_rat.json",
			"""
			{
				"id": "example.fire_rat",
				"name": "Fire Rat",
				"health": 0,
				"sprite": "fire_rat.png",
				"movement": "chase_player",
				"attacks": [
					{ "pattern": "adjacent", "damage": 2 }
				]
			}
			""",
			spriteFileName: "fire_rat.png"
		);

		MonsterModLoadResult result = MonsterModLoader.LoadFromDirectory(_modsRoot);

		Assert.That(result.Monsters, Is.Empty);
		Assert.That(result.Errors[0], Does.Contain("\"health\" must be greater than zero"));
	}

	[Test]
	public void LoadFromDirectory_MalformedJson_DisablesWithReadableErrorInsteadOfThrowing()
	{
		string modFolder = Path.Combine(_modsRoot, "broken-mod");
		string monstersFolder = Path.Combine(modFolder, "monsters");
		Directory.CreateDirectory(monstersFolder);
		File.WriteAllText(Path.Combine(monstersFolder, "broken.json"), "{ this is not valid json");

		MonsterModLoadResult result = MonsterModLoader.LoadFromDirectory(_modsRoot);

		Assert.That(result.Monsters, Is.Empty);
		Assert.That(result.Errors, Has.Count.EqualTo(1));
		Assert.That(result.Errors[0], Does.Contain("broken.json disabled"));
	}

	[Test]
	public void LoadFromDirectory_OneBadModDoesNotBlockAGoodOne()
	{
		CreateMod(
			"good-mod",
			"good.json",
			"""
			{
				"id": "example.good",
				"name": "Good Monster",
				"health": 3,
				"sprite": "good.png",
				"movement": "stationary",
				"attacks": [
					{ "pattern": "adjacent", "damage": 1 }
				]
			}
			""",
			spriteFileName: "good.png"
		);

		CreateMod(
			"bad-mod",
			"bad.json",
			"""
			{
				"id": "example.bad",
				"name": "Bad Monster",
				"health": 3,
				"sprite": "bad.png",
				"movement": "not_a_real_behavior",
				"attacks": [
					{ "pattern": "adjacent", "damage": 1 }
				]
			}
			""",
			spriteFileName: "bad.png"
		);

		MonsterModLoadResult result = MonsterModLoader.LoadFromDirectory(_modsRoot);

		Assert.That(result.Monsters, Has.Count.EqualTo(1));
		Assert.That(result.Monsters[0].Id, Is.EqualTo("example.good"));
		Assert.That(result.Errors, Has.Count.EqualTo(1));
		Assert.That(result.Errors[0], Does.Contain("Bad Monster disabled"));
	}

	private void CreateMod(
		string modFolderName,
		string monsterFileName,
		string monsterJson,
		string spriteFileName)
	{
		string modFolder = Path.Combine(_modsRoot, modFolderName);
		string monstersFolder = Path.Combine(modFolder, "monsters");
		Directory.CreateDirectory(monstersFolder);
		File.WriteAllText(Path.Combine(monstersFolder, monsterFileName), monsterJson);

		if (spriteFileName != null)
		{
			string spritesFolder = Path.Combine(modFolder, "sprites");
			Directory.CreateDirectory(spritesFolder);
			File.WriteAllBytes(Path.Combine(spritesFolder, spriteFileName), Array.Empty<byte>());
		}
	}
}
