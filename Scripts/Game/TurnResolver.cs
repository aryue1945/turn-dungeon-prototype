using Godot;
using System;
using System.Collections.Generic;

// The minimum a turn resolver needs from the player to resolve one action:
// combat identity (ICombatant), an attack to try, a digging tool, and the
// ability to actually move. Player already has all of these publicly, so it
// satisfies this with no changes; a test double can implement it without any
// Godot dependency at all.
public interface IPlayerTurnActor : ICombatant
{
	AttackState Attack { get; }
	DiggingToolDefinition DiggingTool { get; }

	void Move(Vector2 direction);
}

public enum PlayerActionKind
{
	Attacked,
	Preparing,
	TerrainDug,
	TerrainDestroyed,
	Moved,
	Blocked
}

// What resolving one player action decided, in enough detail for the caller
// to narrate it and refresh only the cells that actually changed - the same
// typed-outcome shape docs/SAVE_AND_DEBUG_HISTORY.md asks for (Attack,
// Prepare, Dig, TerrainDestroyed, Move, Blocked), scoped to the player's turn
// for now.
public sealed class PlayerActionOutcome
{
	public PlayerActionKind Kind { get; }
	public GridPosition TargetCell { get; }
	public string AttackName { get; }
	public int RemainingDurability { get; }
	public bool DoorOpened { get; }

	private PlayerActionOutcome(
		PlayerActionKind kind,
		GridPosition targetCell,
		string attackName,
		int remainingDurability,
		bool doorOpened)
	{
		Kind = kind;
		TargetCell = targetCell;
		AttackName = attackName;
		RemainingDurability = remainingDurability;
		DoorOpened = doorOpened;
	}

	public static PlayerActionOutcome Attacked(string attackName) =>
		new(PlayerActionKind.Attacked, default, attackName, 0, false);

	public static PlayerActionOutcome Preparing(string attackName) =>
		new(PlayerActionKind.Preparing, default, attackName, 0, false);

	public static PlayerActionOutcome TerrainDug(
		GridPosition targetCell,
		int remainingDurability) =>
		new(PlayerActionKind.TerrainDug, targetCell, null, remainingDurability, false);

	public static PlayerActionOutcome TerrainDestroyed(GridPosition targetCell) =>
		new(PlayerActionKind.TerrainDestroyed, targetCell, null, 0, false);

	public static PlayerActionOutcome Moved(GridPosition targetCell, bool doorOpened) =>
		new(PlayerActionKind.Moved, targetCell, null, 0, doorOpened);

	public static PlayerActionOutcome Blocked(GridPosition targetCell) =>
		new(PlayerActionKind.Blocked, targetCell, null, 0, false);
}

// First slice of NEXT_STEPS milestone 2: resolves the player's half of a
// turn (attack -> dig -> move/bump), the same priority and rules Main used
// to run inline. Enemy turns are not extracted yet - see TakeEnemyTurns in
// Main.cs.
public static class TurnResolver
{
	public static PlayerActionOutcome ResolvePlayerAction(
		IPlayerTurnActor player,
		Vector2 direction,
		IReadOnlyList<ICombatant> combatants,
		DungeonMap map,
		Func<GridPosition, bool> isWallAt)
	{
		GridPosition targetCell = new(
			player.GridPosition.X + (int)direction.X,
			player.GridPosition.Y + (int)direction.Y
		);

		AttackTurnResult attackResult = AttackResolver.TryAttack(
			player,
			direction,
			player.Attack,
			combatants,
			isWallAt
		);

		if (attackResult == AttackTurnResult.Attacked)
			return PlayerActionOutcome.Attacked(player.Attack.Definition.Name);

		if (attackResult == AttackTurnResult.Preparing)
			return PlayerActionOutcome.Preparing(player.Attack.Definition.Name);

		DigResult digResult = DigResolver.TryDig(map, targetCell, player.DiggingTool);

		if (digResult == DigResult.Destroyed)
			return PlayerActionOutcome.TerrainDestroyed(targetCell);

		if (digResult == DigResult.Damaged)
		{
			int remainingDurability = map.GetCell(targetCell.X, targetCell.Y).Durability;
			return PlayerActionOutcome.TerrainDug(targetCell, remainingDurability);
		}

		if (isWallAt(targetCell))
			return PlayerActionOutcome.Blocked(targetCell);

		player.Move(direction);
		bool doorOpened = map.OpenDoor(targetCell.X, targetCell.Y);
		return PlayerActionOutcome.Moved(targetCell, doorOpened);
	}
}
