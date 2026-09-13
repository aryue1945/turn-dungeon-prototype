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

		if (!HasActivationTarget(
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

	private static bool HasActivationTarget(
		ICombatant attacker,
		Vector2 direction,
		AttackDefinition definition,
		IReadOnlyList<ICombatant> combatants,
		Func<Vector2, bool> isWallAt)
	{
		for (int distance = 1;
			distance <= definition.ActivationRange;
			distance++)
		{
			Vector2 position =
				attacker.Position +
				direction * TileSize * distance;

			if (isWallAt(position) && definition.StopsAtWalls)
				return false;

			ICombatant target = FindCombatantAt(
				position,
				attacker,
				combatants
			);

			if (target == null)
				continue;

			if (IsValidTarget(attacker, target, definition))
				return true;

			if (definition.StopsAtActors)
				return false;
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
		for (int distance = 1;
			distance <= definition.EffectRange;
			distance++)
		{
			Vector2 position =
				attacker.Position +
				direction * TileSize * distance;

			if (isWallAt(position) && definition.StopsAtWalls)
				return;

			ICombatant target = FindCombatantAt(
				position,
				attacker,
				combatants
			);

			if (target == null)
				continue;

			bool isValidTarget =
				IsValidTarget(attacker, target, definition);

			if (isValidTarget)
				target.TakeDamage(definition.Damage);

			if (isValidTarget &&
				definition.Pattern == AttackPattern.FirstTarget)
			{
				return;
			}

			if (definition.StopsAtActors)
				return;
		}
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
