using Godot;
using System;
using System.Collections.Generic;

public static class AttackResolver
{
	private const float TileSize = 32.0f;

	public static AttackTurnResult TryAttack(
		ICombatant attacker,
		Vector2 requestedDirection,
		AttackState attackState,
		IReadOnlyList<ICombatant> combatants,
		Func<Vector2, bool> isWallAt)
	{
		if (attackState.IsPreparing)
		{
			if (!attackState.AdvancePreparation())
				return AttackTurnResult.Preparing;

			Vector2 preparedDirection =
				attackState.PreparedDirection;

			ExecuteAttack(
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

		ExecuteAttack(
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
		Func<Vector2, bool> isWallAt)
	{
		foreach (AttackOffset offset in definition.DetectionOffsets)
		{
			Vector2 position = GetPatternPosition(
				attacker.Position,
				direction,
				offset
			);

			if (definition.StopsAtWalls && isWallAt(position))
				continue;

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

	private static void ExecuteAttack(
		ICombatant attacker,
		Vector2 direction,
		AttackDefinition definition,
		IReadOnlyList<ICombatant> combatants,
		Func<Vector2, bool> isWallAt)
	{
		HashSet<ICombatant> hitTargets = new();

		foreach (AttackOffset offset in definition.AttackOffsets)
		{
			Vector2 position = GetPatternPosition(
				attacker.Position,
				direction,
				offset
			);

			if (definition.StopsAtWalls && isWallAt(position))
				continue;

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
		}
	}

	private static Vector2 GetPatternPosition(
		Vector2 origin,
		Vector2 direction,
		AttackOffset offset)
	{
		Vector2 right = new(-direction.Y, direction.X);
		Vector2 gridOffset =
			direction * offset.Forward +
			right * offset.Right;

		return origin + gridOffset * TileSize;
	}

	private static ICombatant FindCombatantAt(
		Vector2 position,
		ICombatant attacker,
		IReadOnlyList<ICombatant> combatants)
	{
		foreach (ICombatant combatant in combatants)
		{
			if (combatant == attacker || !combatant.IsAlive)
				continue;

			if (combatant.Position.IsEqualApprox(position))
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
