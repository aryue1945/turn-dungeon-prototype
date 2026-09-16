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
}
