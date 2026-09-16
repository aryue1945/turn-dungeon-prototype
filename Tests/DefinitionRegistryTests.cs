using NUnit.Framework;
using System.Linq;

// FindById on the weapon/digging-tool registries is milestone-4 Continue
// support: resolving a saved WeaponId/ToolId back into a real definition
// before restoring anything (Main.TryResolveSaveDefinitions). The
// uniqueness tests below are milestone-5 duplicate-id regression guards for
// the static (non-moddable) registries - MonsterModLoader/Main.BuildSpawnPool
// already reject a moddable duplicate at load time; these just confirm the
// hard-coded content itself was never edited into a collision.
[TestFixture]
public sealed class DefinitionRegistryTests
{
	[Test]
	public void WeaponDefinitions_AllHasNoDuplicateIds()
	{
		Assert.That(
			WeaponDefinitions.All.Select(weapon => weapon.Id).Distinct().Count(),
			Is.EqualTo(WeaponDefinitions.All.Count)
		);
	}

	[Test]
	public void WeaponDefinitions_AllCurrentWeaponsHaveAnIcon()
	{
		// SpritePath is optional on WeaponDefinition (a future weapon need
		// not have art ready immediately), but every weapon that exists
		// today should not silently regress to a missing icon.
		Assert.That(
			WeaponDefinitions.All.All(weapon => !string.IsNullOrWhiteSpace(weapon.SpritePath)),
			Is.True
		);
	}

	[Test]
	public void DiggingToolDefinitions_AllHasNoDuplicateIds()
	{
		Assert.That(
			DiggingToolDefinitions.All.Select(tool => tool.Id).Distinct().Count(),
			Is.EqualTo(DiggingToolDefinitions.All.Count)
		);
	}

	[Test]
	public void MonsterDefinitions_AllHasNoDuplicateIds()
	{
		Assert.That(
			MonsterDefinitions.All.Select(monster => monster.Id).Distinct().Count(),
			Is.EqualTo(MonsterDefinitions.All.Count)
		);
	}

	[Test]
	public void EnemyMovementBehaviors_KnownIdsHasNoDuplicates()
	{
		// The registry is backed by a Dictionary, which already refuses a
		// duplicate key at construction time - this documents that
		// guarantee rather than re-implementing it.
		Assert.That(
			EnemyMovementBehaviors.KnownIds.Distinct().Count(),
			Is.EqualTo(EnemyMovementBehaviors.KnownIds.Count)
		);
	}

	[Test]
	public void WeaponDefinitions_FindById_ReturnsTheMatchingDefinition()
	{
		WeaponDefinition found = WeaponDefinitions.FindById("core.long_sword");

		Assert.That(found, Is.SameAs(WeaponDefinitions.LongSword));
	}

	[Test]
	public void WeaponDefinitions_FindById_ReturnsNullForAnUnknownId()
	{
		Assert.That(WeaponDefinitions.FindById("mod.unknown_weapon"), Is.Null);
	}

	[Test]
	public void DiggingToolDefinitions_FindById_ReturnsTheMatchingDefinition()
	{
		DiggingToolDefinition found = DiggingToolDefinitions.FindById("core.basic_shovel");

		Assert.That(found, Is.SameAs(DiggingToolDefinitions.BasicShovel));
	}

	[Test]
	public void DiggingToolDefinitions_FindById_ReturnsNullForAnUnknownId()
	{
		Assert.That(DiggingToolDefinitions.FindById("mod.unknown_tool"), Is.Null);
	}
}
