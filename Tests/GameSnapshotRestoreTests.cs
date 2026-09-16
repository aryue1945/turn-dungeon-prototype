using NUnit.Framework;

[TestFixture]
public sealed class GameSnapshotRestoreTests
{
	private const int Width = 24;
	private const int Height = 16;
	private const int ZoneCount = 5;

	[Test]
	public void RestoreMap_MatchesEveryCellOfTheOriginal()
	{
		DungeonMap original = Generate(seed: 1);
		GridSnapshot grid = GameSnapshot.Capture(
			new GameState(original, CreatePlayerState())
		).Grid;

		DungeonMap restored = GameSnapshotRestore.RestoreMap(grid, seed: 999);

		Assert.That(restored.Width, Is.EqualTo(original.Width));
		Assert.That(restored.Height, Is.EqualTo(original.Height));

		for (int y = 0; y < original.Height; y++)
		{
			for (int x = 0; x < original.Width; x++)
			{
				DungeonCell originalCell = original.GetCell(x, y);
				DungeonCell restoredCell = restored.GetCell(x, y);

				Assert.That(restoredCell.Terrain.Kind, Is.EqualTo(originalCell.Terrain.Kind));
				Assert.That(restoredCell.Durability, Is.EqualTo(originalCell.Durability));
				Assert.That(restoredCell.IsOpen, Is.EqualTo(originalCell.IsOpen));
				Assert.That(restoredCell.ZoneId, Is.EqualTo(originalCell.ZoneId));
				Assert.That(restoredCell.ConnectedZoneA, Is.EqualTo(originalCell.ConnectedZoneA));
				Assert.That(restoredCell.ConnectedZoneB, Is.EqualTo(originalCell.ConnectedZoneB));
			}
		}
	}

	[Test]
	public void RestoreMap_PreservesAnOpenedDoor()
	{
		DungeonMap original = Generate(seed: 54321);
		GridPosition door = FindCell(original, TerrainKind.Door);
		Assert.That(original.OpenDoor(door.X, door.Y), Is.True, "Setup: door should open once.");

		GridSnapshot grid = GameSnapshot.Capture(
			new GameState(original, CreatePlayerState())
		).Grid;
		DungeonMap restored = GameSnapshotRestore.RestoreMap(grid, seed: 1);

		Assert.That(restored.GetCell(door.X, door.Y).IsOpen, Is.True);
	}

	[Test]
	public void RestoreMap_PreservesDamagedButNotDestroyedTerrain()
	{
		DungeonMap original = Generate(seed: 86420);
		GridPosition wall = FindCell(original, TerrainKind.BreakableWall);

		GridSnapshot grid = GameSnapshot.Capture(
			new GameState(original, CreatePlayerState())
		).Grid;
		DungeonMap restored = GameSnapshotRestore.RestoreMap(grid, seed: 1);

		Assert.That(restored.GetCell(wall.X, wall.Y).Terrain.Kind, Is.EqualTo(TerrainKind.BreakableWall));
		Assert.That(
			restored.GetCell(wall.X, wall.Y).Durability,
			Is.EqualTo(original.GetCell(wall.X, wall.Y).Durability)
		);
	}

	[Test]
	public void RestoreMap_IsIndependentFromTheOriginalMap()
	{
		DungeonMap original = Generate(seed: 86420);
		GridPosition wall = FindCell(original, TerrainKind.BreakableWall);

		GridSnapshot grid = GameSnapshot.Capture(
			new GameState(original, CreatePlayerState())
		).Grid;
		DungeonMap restored = GameSnapshotRestore.RestoreMap(grid, seed: 1);

		original.DamageTerrain(wall.X, wall.Y, damage: 1);

		Assert.That(original.GetCell(wall.X, wall.Y).Terrain.Kind, Is.EqualTo(TerrainKind.Floor));
		Assert.That(
			restored.GetCell(wall.X, wall.Y).Terrain.Kind,
			Is.EqualTo(TerrainKind.BreakableWall),
			"Mutating the original map after restore must not affect the restored copy."
		);
	}

	[Test]
	public void RestoreMap_RejectsNullGrid()
	{
		System.Action restore = () => GameSnapshotRestore.RestoreMap(null, seed: 1);

		Assert.Throws<System.ArgumentNullException>(restore);
	}

	private static DungeonMap Generate(int seed)
	{
		return new DungeonGenerator().Generate(
			new DungeonGenerationRequest(Width, Height, ZoneCount, seed)
		);
	}

	private static ActorState CreatePlayerState()
	{
		return new ActorState(new GridPosition(0, 0), maxHealth: 3, "core.player");
	}

	private static GridPosition FindCell(DungeonMap map, TerrainKind terrainKind)
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
