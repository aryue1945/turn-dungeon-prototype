using NUnit.Framework;

// FindById on the weapon/digging-tool registries is milestone-4 Continue
// support: resolving a saved WeaponId/ToolId back into a real definition
// before restoring anything (Main.TryResolveSaveDefinitions).
[TestFixture]
public sealed class DefinitionRegistryTests
{
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
