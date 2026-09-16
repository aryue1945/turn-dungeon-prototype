using Godot;
using NUnit.Framework;
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
