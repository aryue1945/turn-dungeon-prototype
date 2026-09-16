public interface IEquipment
{
	string Id { get; }
	string Name { get; }
}

public sealed class DiggingToolDefinition : IEquipment
{
	public string Id { get; }
	public string Name { get; }
	public int TerrainDamage { get; }

	public DiggingToolDefinition(string id, string name, int terrainDamage)
	{
		if (string.IsNullOrWhiteSpace(id))
			throw new System.ArgumentException("A digging tool requires an id.");

		if (string.IsNullOrWhiteSpace(name))
			throw new System.ArgumentException("A digging tool requires a name.");

		if (terrainDamage <= 0)
			throw new System.ArgumentOutOfRangeException(nameof(terrainDamage));

		Id = id;
		Name = name;
		TerrainDamage = terrainDamage;
	}
}

public static class DiggingToolDefinitions
{
	public static readonly DiggingToolDefinition BasicShovel = new(
		id: "core.basic_shovel",
		name: "Basic Shovel",
		terrainDamage: 1
	);

	public static readonly System.Collections.Generic.IReadOnlyList<DiggingToolDefinition> All = new[]
	{
		BasicShovel
	};

	// Resolves a saved ToolId back into its definition for milestone-4
	// Continue - null (not thrown) when the id is unknown, since the caller
	// needs to report a useful error rather than crash on a stale id.
	public static DiggingToolDefinition FindById(string id)
	{
		foreach (DiggingToolDefinition tool in All)
		{
			if (tool.Id == id)
				return tool;
		}

		return null;
	}
}
