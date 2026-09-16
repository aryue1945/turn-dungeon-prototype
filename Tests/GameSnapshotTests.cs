using Godot;
using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public sealed class GameSnapshotTests
{
	private const int Width = 24;
	private const int Height = 16;
	private const int ZoneCount = 5;

	[Test]
	public void Capture_CopiesGridDimensionsAndTerrain()
	{
		DungeonMap map = Generate(seed: 1);
		GameState state = new(map, CreatePlayerState());

		GameSnapshot snapshot = GameSnapshot.Capture(state);

		Assert.That(snapshot.Grid.Width, Is.EqualTo(map.Width));
		Assert.That(snapshot.Grid.Height, Is.EqualTo(map.Height));

		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				DungeonCell cell = map.GetCell(x, y);
				CellSnapshot cellSnapshot = snapshot.Grid.GetCell(x, y);

				Assert.That(cellSnapshot.Position, Is.EqualTo(cell.Position));
				Assert.That(cellSnapshot.Terrain, Is.EqualTo(cell.Terrain.Kind));
				Assert.That(cellSnapshot.Durability, Is.EqualTo(cell.Durability));
				Assert.That(cellSnapshot.IsOpen, Is.EqualTo(cell.IsOpen));
				Assert.That(cellSnapshot.ZoneId, Is.EqualTo(cell.ZoneId));
			}
		}
	}

	[Test]
	public void Capture_CopiesPlayerAndEnemyActorFields()
	{
		ActorState player = CreatePlayerState();
		player.SetFacing(Vector2.Up);
		player.SetEquipment("core.basic_sword", "core.basic_shovel");

		ActorState enemy = CreateEnemyState();
		enemy.SetHasPreparedMove(true);

		GameState state = new(Generate(seed: 1), player);
		state.AddEnemy(enemy);

		GameSnapshot snapshot = GameSnapshot.Capture(state);

		Assert.That(snapshot.Player.InstanceId, Is.EqualTo(player.InstanceId));
		Assert.That(snapshot.Player.DefinitionId, Is.EqualTo(player.DefinitionId));
		Assert.That(snapshot.Player.Position, Is.EqualTo(player.GridPosition));
		Assert.That(snapshot.Player.Health, Is.EqualTo(player.Health));
		Assert.That(snapshot.Player.MaxHealth, Is.EqualTo(player.MaxHealth));
		Assert.That(snapshot.Player.Facing, Is.EqualTo(Vector2.Up));
		Assert.That(snapshot.Player.WeaponId, Is.EqualTo("core.basic_sword"));
		Assert.That(snapshot.Player.ToolId, Is.EqualTo("core.basic_shovel"));

		Assert.That(snapshot.Enemies, Has.Count.EqualTo(1));
		Assert.That(snapshot.Enemies[0].InstanceId, Is.EqualTo(enemy.InstanceId));
		Assert.That(snapshot.Enemies[0].HasPreparedMove, Is.True);
	}

	[Test]
	public void Capture_CopiesAttackPreparationState()
	{
		ActorState player = CreatePlayerState();
		AttackDefinition chargedStrike = new(
			name: "Charged Strike",
			damage: 3,
			preparationTurns: 1,
			detectionOffsets: new[] { new AttackOffset(1, 0) },
			attackOffsets: new[] { new AttackOffset(1, 0) },
			targetRule: AttackTargetRule.OpponentsOnly,
			stopsAtWalls: true,
			maxTargets: 1
		);
		AttackState attack = new(chargedStrike);
		attack.BeginPreparation(Vector2.Right);
		player.SetAttack(attack);

		GameState state = new(Generate(seed: 1), player);
		GameSnapshot snapshot = GameSnapshot.Capture(state);

		Assert.That(snapshot.Player.IsAttackPreparing, Is.True);
		Assert.That(snapshot.Player.AttackRemainingPreparationTurns, Is.EqualTo(1));
		Assert.That(snapshot.Player.AttackPreparedDirection, Is.EqualTo(Vector2.Right));
	}

	[Test]
	public void Capture_IsIndependentFromLaterStateMutation()
	{
		ActorState player = CreatePlayerState();
		ActorState enemy = CreateEnemyState();
		DungeonMap map = Generate(seed: 1);
		GameState state = new(map, player);
		state.AddEnemy(enemy);

		GameSnapshot snapshot = GameSnapshot.Capture(state);
		GridPosition originalPlayerPosition = snapshot.Player.Position;
		int originalEnemyCount = snapshot.Enemies.Count;

		player.MoveBy(1, 0);
		player.TakeDamage(1);
		enemy.TakeDamage(999);
		state.RemoveDefeatedEnemies();
		state.CompleteTurn();

		Assert.That(snapshot.Player.Position, Is.EqualTo(originalPlayerPosition));
		Assert.That(snapshot.Player.Health, Is.EqualTo(3));
		Assert.That(snapshot.Enemies, Has.Count.EqualTo(originalEnemyCount));
		Assert.That(snapshot.TurnNumber, Is.EqualTo(0));
	}

	[Test]
	public void Capture_MutatingTerrainAfterwardsDoesNotChangeTheSnapshot()
	{
		DungeonMap map = Generate(seed: 86420);
		GridPosition wall = FindCell(map, TerrainKind.BreakableWall);
		GameState state = new(map, CreatePlayerState());

		GameSnapshot snapshot = GameSnapshot.Capture(state);
		Assert.That(
			snapshot.Grid.GetCell(wall.X, wall.Y).Terrain,
			Is.EqualTo(TerrainKind.BreakableWall)
		);

		map.DamageTerrain(wall.X, wall.Y, damage: 1);

		Assert.That(
			snapshot.Grid.GetCell(wall.X, wall.Y).Terrain,
			Is.EqualTo(TerrainKind.BreakableWall),
			"The snapshot must not see terrain changes made after capture."
		);
		Assert.That(map.GetCell(wall.X, wall.Y).Terrain.Kind, Is.EqualTo(TerrainKind.Floor));
	}

	[Test]
	public void Capture_RejectsNullState()
	{
		System.Action capture = () => GameSnapshot.Capture(null);

		Assert.Throws<System.ArgumentNullException>(capture);
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

	private static ActorState CreateEnemyState()
	{
		return new ActorState(new GridPosition(1, 1), maxHealth: 1, "core.test_enemy");
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
