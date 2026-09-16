using Godot;
using System;
using System.Collections.Generic;

public static class AttackResolver
{
	public static AttackTurnResult TryAttack(
		ICombatant attacker,
		Vector2 requestedDirection,
		AttackState attackState,
		IReadOnlyList<ICombatant> combatants,
		Func<GridPosition, bool> isWallAt,
		out AttackExecutionDetail detail)
	{
		detail = null;

		if (attackState.IsPreparing)
		{
			if (!attackState.AdvancePreparation())
				return AttackTurnResult.Preparing;

			Vector2 preparedDirection =
				attackState.PreparedDirection;

			detail = ExecuteAttack(
				attacker,
				preparedDirection,
				attackState.Definition,
				combatants,
				isWallAt
			);

			attackState.CompletePreparation();
			return AttackTurnResult.Attacked;
		}

		if (requestedDirection.IsZeroApprox())
			return AttackTurnResult.NoAttack;

		AttackDefinition definition = attackState.Definition;

		if (!HasDetectionTarget(
			attacker,
			requestedDirection,
			definition,
			combatants,
			isWallAt
		))
		{
			return AttackTurnResult.NoAttack;
		}

		if (definition.PreparationTurns > 0)
		{
			attackState.BeginPreparation(requestedDirection);
			return AttackTurnResult.Preparing;
		}

		detail = ExecuteAttack(
			attacker,
			requestedDirection,
			definition,
			combatants,
			isWallAt
		);

		return AttackTurnResult.Attacked;
	}

	private static bool HasDetectionTarget(
		ICombatant attacker,
		Vector2 direction,
		AttackDefinition definition,
		IReadOnlyList<ICombatant> combatants,
		Func<GridPosition, bool> isWallAt)
	{
		foreach (AttackOffset offset in definition.DetectionOffsets)
		{
			GridPosition position = GetPatternPosition(
				attacker.GridPosition,
				direction,
				offset
			);

			if (definition.StopsAtWalls && isWallAt(position))
				break;

			ICombatant target = FindCombatantAt(
				position,
				attacker,
				combatants
			);

			if (target != null &&
				IsValidTarget(attacker, target, definition))
			{
				return true;
			}
		}

		return false;
	}

	private static AttackExecutionDetail ExecuteAttack(
		ICombatant attacker,
		Vector2 direction,
		AttackDefinition definition,
		IReadOnlyList<ICombatant> combatants,
		Func<GridPosition, bool> isWallAt)
	{
		HashSet<ICombatant> hitTargets = new();
		List<GridPosition> affectedCells = new();
		List<AttackHitDetail> hits = new();

		foreach (AttackOffset offset in definition.AttackOffsets)
		{
			GridPosition position = GetPatternPosition(
				attacker.GridPosition,
				direction,
				offset
			);

			if (definition.StopsAtWalls && isWallAt(position))
				break;

			affectedCells.Add(position);

			ICombatant target = FindCombatantAt(
				position,
				attacker,
				combatants
			);

			if (target == null ||
				hitTargets.Contains(target) ||
				!IsValidTarget(attacker, target, definition))
			{
				continue;
			}

			target.TakeDamage(definition.Damage);
			hitTargets.Add(target);

			bool defeated = !target.IsAlive;
			GridPosition? knockedBackTo = null;

			if (!defeated && definition.Knockback)
				knockedBackTo = TryKnockback(target, direction, attacker, combatants, isWallAt);

			hits.Add(new AttackHitDetail(
				target.InstanceId,
				position,
				definition.Damage,
				target.Health,
				defeated,
				knockedBackTo
			));

			if (hitTargets.Count >= definition.MaxTargets)
				break;
		}

		return new AttackExecutionDetail(attacker.InstanceId, affectedCells, hits);
	}

	// Pushes target one cell in direction if the destination is walkable
	// and unoccupied; a blocked push still leaves the damage already
	// applied by the caller in place, it just does not move anyone
	// (War Hammer spec). Returns the destination if the push happened,
	// null otherwise.
	private static GridPosition? TryKnockback(
		ICombatant target,
		Vector2 direction,
		ICombatant attacker,
		IReadOnlyList<ICombatant> combatants,
		Func<GridPosition, bool> isWallAt)
	{
		GridPosition destination = new(
			target.GridPosition.X + (int)direction.X,
			target.GridPosition.Y + (int)direction.Y
		);

		if (isWallAt(destination) || FindCombatantAt(destination, attacker, combatants) != null)
			return null;

		target.Knockback(direction);
		return destination;
	}

	private static GridPosition GetPatternPosition(
		GridPosition origin,
		Vector2 direction,
		AttackOffset offset)
	{
		Vector2 right = new(-direction.Y, direction.X);
		Vector2 cellOffset =
			direction * offset.Forward +
			right * offset.Right;

		return new GridPosition(
			origin.X + (int)cellOffset.X,
			origin.Y + (int)cellOffset.Y
		);
	}

	private static ICombatant FindCombatantAt(
		GridPosition position,
		ICombatant attacker,
		IReadOnlyList<ICombatant> combatants)
	{
		foreach (ICombatant combatant in combatants)
		{
			if (combatant == attacker || !combatant.IsAlive)
				continue;

			if (combatant.GridPosition == position)
				return combatant;
		}

		return null;
	}

	private static bool IsValidTarget(
		ICombatant attacker,
		ICombatant target,
		AttackDefinition definition)
	{
		return definition.TargetRule switch
		{
			AttackTargetRule.OpponentsOnly =>
				target.Faction != attacker.Faction,
			AttackTargetRule.AllExceptAttacker => true,
			_ => false
		};
	}
}
