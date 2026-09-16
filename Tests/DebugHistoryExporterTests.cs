using Godot;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.Json;

[TestFixture]
public sealed class DebugHistoryExporterTests
{
	private const int Width = 24;
	private const int Height = 16;
	private const int ZoneCount = 5;

	[Test]
	public void ToJson_ProducesParsableJsonWithGridAndActors()
	{
		DungeonMap map = Generate(seed: 1);
		ActorState player = new(new GridPosition(0, 0), maxHealth: 3, "core.player");
		GameState state = new(map, player);
		DebugHistory history = new(GameSnapshot.Capture(state));

		string json = DebugHistoryExporter.ToJson(history);
		using JsonDocument document = JsonDocument.Parse(json);

		JsonElement root = document.RootElement;
		JsonElement boundary = root.GetProperty("boundarySnapshot");
		JsonElement grid = boundary.GetProperty("grid");

		Assert.That(grid.GetProperty("width").GetInt32(), Is.EqualTo(map.Width));
		Assert.That(grid.GetProperty("height").GetInt32(), Is.EqualTo(map.Height));
		Assert.That(grid.GetProperty("rows").GetArrayLength(), Is.EqualTo(map.Height));
		Assert.That(
			grid.GetProperty("rows")[0].GetArrayLength(),
			Is.EqualTo(map.Width)
		);

		JsonElement playerJson = boundary.GetProperty("player");
		Assert.That(
			playerJson.GetProperty("definitionId").GetString(),
			Is.EqualTo("core.player")
		);
	}

	[Test]
	public void ToJson_RendersEnumsAsReadableNamesNotNumbers()
	{
		DungeonMap map = Generate(seed: 86420);
		GridPosition wall = FindCell(map, TerrainKind.BreakableWall);
		ActorState player = new(new GridPosition(0, 0), maxHealth: 3, "core.player");
		GameState state = new(map, player);
		DebugHistory history = new(GameSnapshot.Capture(state));

		string json = DebugHistoryExporter.ToJson(history);
		using JsonDocument document = JsonDocument.Parse(json);

		JsonElement cell = document.RootElement
			.GetProperty("boundarySnapshot")
			.GetProperty("grid")
			.GetProperty("rows")[wall.Y][wall.X];

		Assert.That(cell.GetProperty("terrain").GetString(), Is.EqualTo("BreakableWall"));
		Assert.That(
			document.RootElement.GetProperty("boundarySnapshot").GetProperty("status").GetString(),
			Is.EqualTo("InProgress")
		);
	}

	[Test]
	public void ToJson_IncludesTransitionsWithOutcomesAndDirection()
	{
		DungeonMap map = Generate(seed: 1);
		ActorState player = new(new GridPosition(0, 0), maxHealth: 3, "core.player");
		GameState state = new(map, player);
		DebugHistory history = new(GameSnapshot.Capture(state));

		state.CompleteTurn();
		history.AppendTransition(new TurnTransition(
			state.TurnNumber,
			Vector2.Right,
			PlayerActionOutcome.Moved(new GridPosition(1, 0), doorOpened: false),
			new List<EnemyActionOutcome>(),
			GameSnapshot.Capture(state)
		));

		string json = DebugHistoryExporter.ToJson(history);
		using JsonDocument document = JsonDocument.Parse(json);

		JsonElement transitions = document.RootElement.GetProperty("transitions");
		Assert.That(transitions.GetArrayLength(), Is.EqualTo(1));

		JsonElement transition = transitions[0];
		Assert.That(transition.GetProperty("turnNumber").GetInt32(), Is.EqualTo(1));
		Assert.That(
			transition.GetProperty("playerOutcome").GetProperty("kind").GetString(),
			Is.EqualTo("Moved")
		);
		Assert.That(transition.GetProperty("enemyOutcomes").GetArrayLength(), Is.EqualTo(0));
	}

	[Test]
	public void ToJson_WithContext_ExportsOnlySelectedTransitionsAndTheirPrecedingSnapshot()
	{
		DungeonMap map = Generate(seed: 1);
		ActorState player = new(new GridPosition(0, 0), maxHealth: 3, "core.player");
		GameState state = new(map, player);
		DebugHistory history = new(GameSnapshot.Capture(state));

		for (int turn = 1; turn <= 11; turn++)
		{
			state.CompleteTurn();
			history.AppendTransition(new TurnTransition(
				state.TurnNumber,
				Vector2.Right,
				PlayerActionOutcome.Moved(new GridPosition(turn, 0), doorOpened: false),
				new List<EnemyActionOutcome>(),
				GameSnapshot.Capture(state)
			));
		}

		DebugHistoryExportContext context = new(
			Guid.NewGuid(),
			seed: 1,
			floorId: null,
			buildVersion: null,
			weapons: new List<WeaponSummary> { WeaponSummary.From(WeaponDefinitions.BasicSword) },
			monsters: new List<MonsterSummary>()
		);

		string json = DebugHistoryExporter.ToJson(history, transitionCount: 5, context);
		using JsonDocument document = JsonDocument.Parse(json);
		JsonElement root = document.RootElement;

		Assert.That(
			root.GetProperty("beforeSnapshot").GetProperty("turnNumber").GetInt32(),
			Is.EqualTo(6)
		);

		JsonElement transitions = root.GetProperty("transitions");
		Assert.That(transitions.GetArrayLength(), Is.EqualTo(5));
		Assert.That(transitions[0].GetProperty("turnNumber").GetInt32(), Is.EqualTo(7));
		Assert.That(transitions[4].GetProperty("turnNumber").GetInt32(), Is.EqualTo(11));
	}

	[Test]
	public void ToJson_WithContext_IncludesIdentityAndContentSummaries()
	{
		DungeonMap map = Generate(seed: 42);
		ActorState player = new(new GridPosition(0, 0), maxHealth: 3, "core.player");
		GameState state = new(map, player);
		DebugHistory history = new(GameSnapshot.Capture(state));
		Guid runId = Guid.NewGuid();

		DebugHistoryExportContext context = new(
			runId,
			seed: 42,
			floorId: "floor-1",
			buildVersion: "1.0.0",
			weapons: new List<WeaponSummary> { WeaponSummary.From(WeaponDefinitions.BasicSword) },
			monsters: new List<MonsterSummary> { MonsterSummary.From(MonsterDefinitions.All[0]) }
		);

		string json = DebugHistoryExporter.ToJson(history, transitionCount: 5, context);
		using JsonDocument document = JsonDocument.Parse(json);
		JsonElement root = document.RootElement;

		Assert.That(root.GetProperty("runId").GetGuid(), Is.EqualTo(runId));
		Assert.That(root.GetProperty("seed").GetInt32(), Is.EqualTo(42));
		Assert.That(root.GetProperty("floorId").GetString(), Is.EqualTo("floor-1"));
		Assert.That(root.GetProperty("buildVersion").GetString(), Is.EqualTo("1.0.0"));
		Assert.That(root.GetProperty("contentFingerprint").GetString(), Is.Not.Empty);
		Assert.That(root.GetProperty("weapons").GetArrayLength(), Is.EqualTo(1));
		Assert.That(
			root.GetProperty("weapons")[0].GetProperty("id").GetString(),
			Is.EqualTo(WeaponDefinitions.BasicSword.Id)
		);
		Assert.That(root.GetProperty("monsters").GetArrayLength(), Is.EqualTo(1));
	}

	[Test]
	public void ToJson_WithContext_AttackedOutcomeIncludesAttackDetailInsteadOfDefaultTargetCell()
	{
		DungeonMap map = new(5, 5, seed: 1);
		for (int y = 0; y < 5; y++)
		{
			for (int x = 0; x < 5; x++)
			{
				bool isBoundary = x == 0 || y == 0 || x == 4 || y == 4;
				map.SetTerrain(x, y, isBoundary ? TerrainKind.SolidWall : TerrainKind.Floor);
			}
		}

		ActorState player = new(new GridPosition(1, 1), maxHealth: 3, "core.player");
		ActorState enemy = new(new GridPosition(2, 1), maxHealth: 3, "core.test_enemy");
		GameState state = new(map, player);
		state.AddEnemy(enemy);
		DebugHistory history = new(GameSnapshot.Capture(state));

		AttackExecutionDetail attackDetail = new(
			player.InstanceId,
			new List<GridPosition> { new(2, 1) },
			new List<AttackHitDetail>
			{
				new(enemy.InstanceId, new GridPosition(2, 1), damage: 1, remainingHealth: 2, defeated: false)
			}
		);

		state.CompleteTurn();
		history.AppendTransition(new TurnTransition(
			state.TurnNumber,
			Vector2.Right,
			PlayerActionOutcome.Attacked("Sword Strike", attackDetail),
			new List<EnemyActionOutcome>(),
			GameSnapshot.Capture(state)
		));

		DebugHistoryExportContext context = new(
			Guid.NewGuid(), seed: 1, floorId: null, buildVersion: null,
			weapons: new List<WeaponSummary>(), monsters: new List<MonsterSummary>()
		);

		string json = DebugHistoryExporter.ToJson(history, transitionCount: 1, context);
		using JsonDocument document = JsonDocument.Parse(json);
		JsonElement outcome = document.RootElement
			.GetProperty("transitions")[0]
			.GetProperty("playerOutcome");

		Assert.That(outcome.TryGetProperty("targetCell", out JsonElement targetCell), Is.True);
		Assert.That(targetCell.ValueKind, Is.EqualTo(JsonValueKind.Null));

		JsonElement detail = outcome.GetProperty("attackDetail");
		Assert.That(detail.GetProperty("attackerInstanceId").GetGuid(), Is.EqualTo(player.InstanceId));
		Assert.That(detail.GetProperty("affectedCells").GetArrayLength(), Is.EqualTo(1));
		Assert.That(detail.GetProperty("hits")[0].GetProperty("targetInstanceId").GetGuid(), Is.EqualTo(enemy.InstanceId));
		Assert.That(detail.GetProperty("hits")[0].GetProperty("remainingHealth").GetInt32(), Is.EqualTo(2));
	}

	private static DungeonMap Generate(int seed)
	{
		return new DungeonGenerator().Generate(
			new DungeonGenerationRequest(Width, Height, ZoneCount, seed)
		);
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
