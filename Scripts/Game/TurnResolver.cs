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

	// Null for Attacked/Preparing - a single cell cannot describe a
	// multi-cell attack pattern, and (0,0) previously stood in for "not
	// applicable" indistinguishably from a real cell. Use AttackDetail's
	// AffectedCells for those kinds instead.
	public GridPosition? TargetCell { get; }
	public string AttackName { get; }
	public int RemainingDurability { get; }
	public bool DoorOpened { get; }

	// Set only for Attacked - who attacked, every cell the attack's pattern
	// swept, and each target actually hit (docs/SAVE_AND_DEBUG_HISTORY.md).
	public AttackExecutionDetail AttackDetail { get; }

	private PlayerActionOutcome(
		PlayerActionKind kind,
		GridPosition? targetCell,
		string attackName,
		int remainingDurability,
		bool doorOpened,
		AttackExecutionDetail attackDetail)
	{
		Kind = kind;
		TargetCell = targetCell;
		AttackName = attackName;
		RemainingDurability = remainingDurability;
		DoorOpened = doorOpened;
		AttackDetail = attackDetail;
	}

	public static PlayerActionOutcome Attacked(
		string attackName,
		AttackExecutionDetail attackDetail) =>
		new(PlayerActionKind.Attacked, null, attackName, 0, false, attackDetail);

	public static PlayerActionOutcome Preparing(string attackName) =>
		new(PlayerActionKind.Preparing, null, attackName, 0, false, null);

	public static PlayerActionOutcome TerrainDug(
		GridPosition targetCell,
		int remainingDurability) =>
		new(PlayerActionKind.TerrainDug, targetCell, null, remainingDurability, false, null);

	public static PlayerActionOutcome TerrainDestroyed(GridPosition targetCell) =>
		new(PlayerActionKind.TerrainDestroyed, targetCell, null, 0, false, null);

	public static PlayerActionOutcome Moved(GridPosition targetCell, bool doorOpened) =>
		new(PlayerActionKind.Moved, targetCell, null, 0, doorOpened, null);

	public static PlayerActionOutcome Blocked(GridPosition targetCell) =>
		new(PlayerActionKind.Blocked, targetCell, null, 0, false, null);
}

// What one enemy's turn did (from its EnemyActionResult - see
// Scripts/Monsters/EnemyMovementBehaviors.cs), where it ended up, and
// whether that opened a door, so the caller can narrate it and decide
// whether to refresh that cell's rendering.
public sealed class EnemyActionOutcome
{
	// Which enemy this outcome belongs to - previously only implied by list
	// position, which the debug export cannot reconstruct on its own
	// (docs/SAVE_AND_DEBUG_HISTORY.md).
	public Guid ActorInstanceId { get; }
	public EnemyActionKind Kind { get; }
	public string AttackName { get; }
	public GridPosition ResultingPosition { get; }
	public bool DoorOpened { get; }
	public AttackExecutionDetail AttackDetail { get; }

	public EnemyActionOutcome(
		Guid actorInstanceId,
		EnemyActionResult actionResult,
		GridPosition resultingPosition,
		bool doorOpened)
	{
		ActorInstanceId = actorInstanceId;
		Kind = actionResult.Kind;
		AttackName = actionResult.AttackName;
		ResultingPosition = resultingPosition;
		DoorOpened = doorOpened;
		AttackDetail = actionResult.AttackDetail;
	}
}

// Milestone 2 (NEXT_STEPS.md): resolves the player's half of a turn (attack
// -> dig -> move/bump) and one enemy's turn, both as typed outcomes. The
// player side needs no Godot node to test (IPlayerTurnActor); the enemy
// side still calls the concrete Enemy/Player classes (movement behaviors
// go through IEnemyMovementHost, which only Enemy implements), so it needs
// a real node the way Enemy/Player themselves already do.
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
			isWallAt,
			out AttackExecutionDetail attackDetail
		);

		if (attackResult == AttackTurnResult.Attacked)
			return PlayerActionOutcome.Attacked(player.Attack.Definition.Name, attackDetail);

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

	public static EnemyActionOutcome ResolveEnemyAction(
		Enemy enemy,
		Player player,
		HashSet<GridPosition> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants,
		DungeonMap map)
	{
		EnemyActionResult actionResult = enemy.TakeTurn(
			player,
			occupiedEnemyPositions,
			combatants
		);

		bool doorOpened = map.OpenDoor(
			enemy.GridPosition.X,
			enemy.GridPosition.Y
		);

		return new EnemyActionOutcome(
			enemy.InstanceId,
			actionResult,
			enemy.GridPosition,
			doorOpened
		);
	}
}
