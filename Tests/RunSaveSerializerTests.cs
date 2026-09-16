using Godot;
using NUnit.Framework;

[TestFixture]
public sealed class RunSaveSerializerTests
{
	private const int Width = 24;
	private const int Height = 16;
	private const int ZoneCount = 5;

	[Test]
	public void RoundTrip_PreservesSeedTurnNumberAndStatus()
	{
		DungeonMap map = Generate(seed: 12345);
		ActorState player = CreatePlayerState();
		GameState state = new(map, player);
		state.CompleteTurn();
		state.CompleteTurn();
		state.SetStatus(RunStatus.Won);

		RunSaveEnvelope envelope = RunSaveEnvelope.Capture(state);
		string json = RunSaveSerializer.ToJson(envelope);
		RunSaveLoadOutcome outcome = RunSaveSerializer.FromJson(json);

		Assert.That(outcome.Result, Is.EqualTo(RunSaveLoadResult.Loaded));
		Assert.That(outcome.Envelope.Seed, Is.EqualTo(12345));
		Assert.That(outcome.Envelope.Snapshot.TurnNumber, Is.EqualTo(2));
		Assert.That(outcome.Envelope.Snapshot.Status, Is.EqualTo(RunStatus.Won));
	}

	[Test]
	public void RoundTrip_PreservesGridCellByCell()
	{
		DungeonMap map = Generate(seed: 1);
		GameState state = new(map, CreatePlayerState());

		string json = RunSaveSerializer.ToJson(RunSaveEnvelope.Capture(state));
		RunSaveLoadOutcome outcome = RunSaveSerializer.FromJson(json);
		GridSnapshot grid = outcome.Envelope.Snapshot.Grid;

		Assert.That(grid.Width, Is.EqualTo(map.Width));
		Assert.That(grid.Height, Is.EqualTo(map.Height));

		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				DungeonCell originalCell = map.GetCell(x, y);
				CellSnapshot cellSnapshot = grid.GetCell(x, y);

				Assert.That(cellSnapshot.Position, Is.EqualTo(originalCell.Position));
				Assert.That(cellSnapshot.Terrain, Is.EqualTo(originalCell.Terrain.Kind));
				Assert.That(cellSnapshot.Durability, Is.EqualTo(originalCell.Durability));
				Assert.That(cellSnapshot.IsOpen, Is.EqualTo(originalCell.IsOpen));
				Assert.That(cellSnapshot.ZoneId, Is.EqualTo(originalCell.ZoneId));
			}
		}
	}

	[Test]
	public void RoundTrip_PreservesActorFieldsIncludingFacingAndAttackPreparation()
	{
		ActorState player = CreatePlayerState();
		player.SetFacing(Vector2.Up);
		player.SetEquipment("core.basic_sword", "core.basic_shovel");
		AttackState attack = new(WeaponDefinitions.BasicSword.PrimaryAttack);
		player.SetAttack(attack);
		player.TakeDamage(1);

		GameState state = new(Generate(seed: 1), player);

		string json = RunSaveSerializer.ToJson(RunSaveEnvelope.Capture(state));
		RunSaveLoadOutcome outcome = RunSaveSerializer.FromJson(json);
		ActorSnapshot restoredPlayer = outcome.Envelope.Snapshot.Player;

		Assert.That(restoredPlayer.InstanceId, Is.EqualTo(player.InstanceId));
		Assert.That(restoredPlayer.DefinitionId, Is.EqualTo("core.player"));
		Assert.That(restoredPlayer.Position, Is.EqualTo(player.GridPosition));
		Assert.That(restoredPlayer.Health, Is.EqualTo(2));
		Assert.That(restoredPlayer.MaxHealth, Is.EqualTo(3));
		Assert.That(restoredPlayer.Facing, Is.EqualTo(Vector2.Up));
		Assert.That(restoredPlayer.WeaponId, Is.EqualTo("core.basic_sword"));
		Assert.That(restoredPlayer.ToolId, Is.EqualTo("core.basic_shovel"));
	}

	[Test]
	public void FromJson_RejectsAnUnsupportedSchemaVersion()
	{
		DungeonMap map = Generate(seed: 1);
		GameState state = new(map, CreatePlayerState());
		RunSaveEnvelope envelope = new(
			schemaVersion: 999,
			seed: 1,
			snapshot: GameSnapshot.Capture(state)
		);

		RunSaveLoadOutcome outcome = RunSaveSerializer.FromJson(RunSaveSerializer.ToJson(envelope));

		Assert.That(outcome.Result, Is.EqualTo(RunSaveLoadResult.UnsupportedVersion));
		Assert.That(outcome.Envelope, Is.Null);
	}

	[Test]
	public void FromJson_RejectsGarbageInput()
	{
		RunSaveLoadOutcome outcome = RunSaveSerializer.FromJson("not json at all {{{");

		Assert.That(outcome.Result, Is.EqualTo(RunSaveLoadResult.Invalid));
		Assert.That(outcome.Envelope, Is.Null);
	}

	[Test]
	public void ToJson_RejectsNullEnvelope()
	{
		System.Action toJson = () => RunSaveSerializer.ToJson(null);

		Assert.Throws<System.ArgumentNullException>(toJson);
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
}
