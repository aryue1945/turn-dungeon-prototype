using System;

public enum DigResult
{
	NoTarget,
	Damaged,
	Destroyed
}

public static class DigResolver
{
	public static DigResult TryDig(
		DungeonMap map,
		GridPosition target,
		DiggingToolDefinition tool)
	{
		if (map == null)
			throw new ArgumentNullException(nameof(map));

		if (tool == null)
			throw new ArgumentNullException(nameof(tool));

		DungeonCell cell = map.GetCell(target.X, target.Y);

		if (cell == null || !cell.Terrain.IsDestructible)
			return DigResult.NoTarget;

		bool destroyed = map.DamageTerrain(
			target.X,
			target.Y,
			tool.TerrainDamage
		);

		return destroyed ? DigResult.Destroyed : DigResult.Damaged;
	}
}
