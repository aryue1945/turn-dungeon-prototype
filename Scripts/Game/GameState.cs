using System;
using System.Collections.Generic;

public enum RunStatus
{
	InProgress,
	Won,
	Lost
}

// The engine-independent aggregate described in docs/ARCHITECTURE.md: the
// current map, the actors playing out on it, the turn number and the run's
// outcome. It only references types that already exist independently of
// Godot (DungeonMap, ActorState) - Player and Enemy hand it their ActorState
// via State, not themselves.
//
// This is a thin container, not a rules engine: Main still owns turn
// execution and keeps this in sync (AddEnemy, RemoveDefeatedEnemies,
// CompleteTurn, SetStatus). It is a real input now, though - Main decides
// victory/death by reading IsPlayerDefeated/AreAllEnemiesDefeated from here
// instead of its own actor list/health field. Extracting a TurnResolver
// that reads and drives the rest of GameState directly is a later
// migration step - see NEXT_STEPS.md.
public sealed class GameState
{
	private readonly List<ActorState> _enemies = new();

	public DungeonMap Map { get; }
	public ActorState Player { get; }
	public IReadOnlyList<ActorState> Enemies => _enemies;
	public int TurnNumber { get; private set; }
	public RunStatus Status { get; private set; } = RunStatus.InProgress;
	public bool IsPlayerDefeated => !Player.IsAlive;
	public bool AreAllEnemiesDefeated => Enemies.Count == 0;

	public GameState(DungeonMap map, ActorState player)
	{
		Map = map ?? throw new ArgumentNullException(nameof(map));
		Player = player ?? throw new ArgumentNullException(nameof(player));
	}

	public void AddEnemy(ActorState enemy)
	{
		if (enemy == null)
			throw new ArgumentNullException(nameof(enemy));

		_enemies.Add(enemy);
	}

	public void RemoveDefeatedEnemies()
	{
		_enemies.RemoveAll(enemy => !enemy.IsAlive);
	}

	public void CompleteTurn()
	{
		TurnNumber++;
	}

	public void SetStatus(RunStatus status)
	{
		Status = status;
	}
}
