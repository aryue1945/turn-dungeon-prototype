using NUnit.Framework;

[TestFixture]
public sealed class DigResolverTests
{
	private const int Width = 24;
	private const int Height = 16;
	private const int ZoneCount = 5;

	[Test]
	public void TryDig_BreakableWallTakesDamageAndBecomesFloor()
	{
		DungeonMap map = Generate(seed: 86420);
		GridPosition wall = FindCell(map, TerrainKind.BreakableWall);

		DigResult result = DigResolver.TryDig(
			map,
			wall,
			DiggingToolDefinitions.BasicShovel
		);

		Assert.That(result, Is.EqualTo(DigResult.Destroyed));
		Assert.That(
			map.GetCell(wall.X, wall.Y).Terrain.Kind,
			Is.EqualTo(TerrainKind.Floor)
		);
		Assert.That(map.IsWalkable(wall.X, wall.Y), Is.True);
	}

	[Test]
	public void TryDig_SolidWallIsNotADigTarget()
	{
		DungeonMap map = Generate(seed: 11223);
		GridPosition wall = FindCell(map, TerrainKind.SolidWall);

		DigResult result = DigResolver.TryDig(
			map,
			wall,
			DiggingToolDefinitions.BasicShovel
		);

		Assert.That(result, Is.EqualTo(DigResult.NoTarget));
		Assert.That(
			map.GetCell(wall.X, wall.Y).Terrain.Kind,
			Is.EqualTo(TerrainKind.SolidWall)
		);
	}

	[Test]
	public void BasicShovel_IsIndependentFromPlayerWeaponDefinitions()
	{
		Assert.That(DiggingToolDefinitions.BasicShovel.Name, Is.EqualTo("Basic Shovel"));
		Assert.That(DiggingToolDefinitions.BasicShovel.TerrainDamage, Is.EqualTo(1));
		Assert.That(WeaponDefinitions.BasicSword.Name, Is.EqualTo("Basic Sword"));
	}

	private static DungeonMap Generate(int seed)
	{
		return new DungeonGenerator().Generate(
			new DungeonGenerationRequest(Width, Height, ZoneCount, seed)
		);
	}

	private static GridPosition FindCell(
		DungeonMap map,
		TerrainKind terrainKind)
	{
		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				if (map.GetCell(x, y).Terrain.Kind == terrainKind)
					return new GridPosition(x, y);
			}
		}

		throw new AssertionException($"No {terrainKind} cell exists.");
	}
}
