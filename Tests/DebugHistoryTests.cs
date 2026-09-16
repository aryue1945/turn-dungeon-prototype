using Godot;
using NUnit.Framework;
using System;
using System.Collections.Generic;

[TestFixture]
public sealed class DebugHistoryTests
{
	private const int Width = 24;
	private const int Height = 16;
	private const int ZoneCount = 5;

	[Test]
	public void Constructor_SetsBoundarySnapshotWithNoTransitions()
	{
		GameSnapshot initial = CreateSnapshot(turnNumber: 0);

		DebugHistory history = new(initial);

		Assert.That(history.BoundarySnapshot, Is.SameAs(initial));
		Assert.That(history.Transitions, Is.Empty);
	}

	[Test]
	public void Constructor_RejectsNullSnapshot()
	{
		Action construct = () => new DebugHistory(null);

		Assert.Throws<ArgumentNullException>(construct);
	}

	[Test]
	public void AppendTransition_AddsWithoutMovingTheBoundaryWhileUnderTen()
	{
		GameSnapshot initial = CreateSnapshot(turnNumber: 0);
		DebugHistory history = new(initial);

		for (int turn = 1; turn <= 10; turn++)
			history.AppendTransition(CreateTransition(turn));

		Assert.That(history.Transitions, Has.Count.EqualTo(10));
		Assert.That(history.BoundarySnapshot, Is.SameAs(initial));
		Assert.That(history.Transitions[0].TurnNumber, Is.EqualTo(1));
		Assert.That(history.Transitions[9].TurnNumber, Is.EqualTo(10));
	}

	[Test]
	public void AppendTransition_RejectsNull()
	{
		DebugHistory history = new(CreateSnapshot(0));
		Action append = () => history.AppendTransition(null);

		Assert.Throws<ArgumentNullException>(append);
	}

	[Test]
	public void AppendTransition_EvictsOldestAndMovesBoundaryOnceOverTen()
	{
		DebugHistory history = new(CreateSnapshot(turnNumber: 0));

		for (int turn = 1; turn <= 11; turn++)
			history.AppendTransition(CreateTransition(turn));

		Assert.That(history.Transitions, Has.Count.EqualTo(10));
		Assert.That(history.Transitions[0].TurnNumber, Is.EqualTo(2));
		Assert.That(history.Transitions[9].TurnNumber, Is.EqualTo(11));
		Assert.That(history.BoundarySnapshot.TurnNumber, Is.EqualTo(1));
	}

	[Test]
	public void AppendTransition_AtTurn25RetainsStates15Through25()
	{
		DebugHistory history = new(CreateSnapshot(turnNumber: 0));

		for (int turn = 1; turn <= 25; turn++)
			history.AppendTransition(CreateTransition(turn));

		Assert.That(history.BoundarySnapshot.TurnNumber, Is.EqualTo(15));
		Assert.That(history.Transitions, Has.Count.EqualTo(10));
		Assert.That(history.Transitions[0].TurnNumber, Is.EqualTo(16));
		Assert.That(history.Transitions[9].TurnNumber, Is.EqualTo(25));

		List<int> retainedStates = new() { history.BoundarySnapshot.TurnNumber };
		foreach (TurnTransition transition in history.Transitions)
			retainedStates.Add(transition.ResultingSnapshot.TurnNumber);

		Assert.That(retainedStates, Is.EqualTo(new List<int>
		{
			15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25
		}));
	}

	[Test]
	public void Reset_ClearsTransitionsAndSetsNewBoundary()
	{
		DebugHistory history = new(CreateSnapshot(turnNumber: 0));

		for (int turn = 1; turn <= 5; turn++)
			history.AppendTransition(CreateTransition(turn));

		GameSnapshot newInitial = CreateSnapshot(turnNumber: 0);
		history.Reset(newInitial);

		Assert.That(history.Transitions, Is.Empty);
		Assert.That(history.BoundarySnapshot, Is.SameAs(newInitial));
	}

	[Test]
	public void Reset_RejectsNullSnapshot()
	{
		DebugHistory history = new(CreateSnapshot(0));
		Action reset = () => history.Reset(null);

		Assert.Throws<ArgumentNullException>(reset);
	}

	[Test]
	public void TurnTransition_RejectsNullOutcomesOrSnapshot()
	{
		PlayerActionOutcome playerOutcome = PlayerActionOutcome.Moved(new GridPosition(0, 0), false);
		GameSnapshot snapshot = CreateSnapshot(0);

		Action nullPlayerOutcome = () => new TurnTransition(
			1, Vector2.Right, null, Array.Empty<EnemyActionOutcome>(), snapshot);
		Action nullEnemyOutcomes = () => new TurnTransition(
			1, Vector2.Right, playerOutcome, null, snapshot);
		Action nullSnapshot = () => new TurnTransition(
			1, Vector2.Right, playerOutcome, Array.Empty<EnemyActionOutcome>(), null);

		Assert.Throws<ArgumentNullException>(nullPlayerOutcome);
		Assert.Throws<ArgumentNullException>(nullEnemyOutcomes);
		Assert.Throws<ArgumentNullException>(nullSnapshot);
	}

	private static GameSnapshot CreateSnapshot(int turnNumber)
	{
		DungeonMap map = new DungeonGenerator().Generate(
			new DungeonGenerationRequest(Width, Height, ZoneCount, seed: 1)
		);
		ActorState player = new(new GridPosition(0, 0), maxHealth: 3, "core.player");
		GameState state = new(map, player);

		for (int i = 0; i < turnNumber; i++)
			state.CompleteTurn();

		return GameSnapshot.Capture(state);
	}

	private static TurnTransition CreateTransition(int turnNumber)
	{
		return new TurnTransition(
			turnNumber,
			Vector2.Right,
			PlayerActionOutcome.Moved(new GridPosition(turnNumber, 0), doorOpened: false),
			Array.Empty<EnemyActionOutcome>(),
			CreateSnapshot(turnNumber)
		);
	}
}
