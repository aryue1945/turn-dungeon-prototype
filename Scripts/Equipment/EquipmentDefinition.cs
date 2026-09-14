public interface IEquipment
{
	string Name { get; }
}

public sealed class DiggingToolDefinition : IEquipment
{
	public string Name { get; }
	public int TerrainDamage { get; }

	public DiggingToolDefinition(string name, int terrainDamage)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new System.ArgumentException("A digging tool requires a name.");

		if (terrainDamage <= 0)
			throw new System.ArgumentOutOfRangeException(nameof(terrainDamage));

		Name = name;
		TerrainDamage = terrainDamage;
	}
}

public static class DiggingToolDefinitions
{
	public static readonly DiggingToolDefinition BasicShovel = new(
		name: "Basic Shovel",
		terrainDamage: 1
	);
}
