using NUnit.Framework;

[TestFixture]
public sealed class GameStateTests
{
	private const int Width = 24;
	private const int Height = 16;
	private const int ZoneCount = 5;

	[Test]
	public void Constructor_SetsMapPlayerAndDefaults()
	{
		DungeonMap map = Generate(seed: 1);
		ActorState player = CreatePlayerState();

		GameState state = new(map, player);

		Assert.That(state.Map, Is.SameAs(map));
		Assert.That(state.Player, Is.SameAs(player));
		Assert.That(state.Enemies, Is.Empty);
		Assert.That(state.TurnNumber, Is.EqualTo(0));
		Assert.That(state.Status, Is.EqualTo(RunStatus.InProgress));
	}

	[Test]
	public void Constructor_RejectsNullMapOrPlayer()
	{
		DungeonMap map = Generate(seed: 1);
		ActorState player = CreatePlayerState();

		System.Action constructWithNullMap = () => new GameState(null, player);
		System.Action constructWithNullPlayer = () => new GameState(map, null);

		Assert.Throws<System.ArgumentNullException>(constructWithNullMap);
		Assert.Throws<System.ArgumentNullException>(constructWithNullPlayer);
	}

	[Test]
	public void AddEnemy_AppearsInEnemiesInOrder()
	{
		GameState state = new(Generate(seed: 1), CreatePlayerState());
		ActorState first = CreateEnemyState();
		ActorState second = CreateEnemyState();

		state.AddEnemy(first);
		state.AddEnemy(second);

		Assert.That(state.Enemies, Is.EqualTo(new[] { first, second }));
	}

	[Test]
	public void RemoveDefeatedEnemies_DropsOnlyDeadEnemies()
	{
		GameState state = new(Generate(seed: 1), CreatePlayerState());
		ActorState alive = CreateEnemyState();
		ActorState dead = CreateEnemyState();
		dead.TakeDamage(1);

		state.AddEnemy(alive);
		state.AddEnemy(dead);

		state.RemoveDefeatedEnemies();

		Assert.That(state.Enemies, Is.EqualTo(new[] { alive }));
	}

	[Test]
	public void CompleteTurn_IncrementsTurnNumber()
	{
		GameState state = new(Generate(seed: 1), CreatePlayerState());

		state.CompleteTurn();
		state.CompleteTurn();

		Assert.That(state.TurnNumber, Is.EqualTo(2));
	}

	[Test]
	public void SetStatus_ChangesStatus()
	{
		GameState state = new(Generate(seed: 1), CreatePlayerState());

		state.SetStatus(RunStatus.Won);

		Assert.That(state.Status, Is.EqualTo(RunStatus.Won));
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
}
