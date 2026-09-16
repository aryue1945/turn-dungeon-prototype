using System;
using System.Collections.Generic;

// One target actually hit by an executed attack, for the debug-history
// export's attack detail (docs/SAVE_AND_DEBUG_HISTORY.md: "attacker ID,
// affected cells, target IDs, damage, remaining health, and defeated
// status"). Built from data AttackResolver already computes at the moment
// of the hit - no separate lookup or authority of its own.
public sealed class AttackHitDetail
{
	public Guid TargetInstanceId { get; }
	public GridPosition Position { get; }
	public int Damage { get; }
	public int RemainingHealth { get; }
	public bool Defeated { get; }

	// Set only when this attack has knockback, the target survived, and the
	// push actually happened (the destination was walkable and unoccupied -
	// a blocked push still deals damage but leaves this null, per the War
	// Hammer spec).
	public GridPosition? KnockedBackTo { get; }

	public AttackHitDetail(
		Guid targetInstanceId,
		GridPosition position,
		int damage,
		int remainingHealth,
		bool defeated,
		GridPosition? knockedBackTo = null)
	{
		TargetInstanceId = targetInstanceId;
		Position = position;
		Damage = damage;
		RemainingHealth = remainingHealth;
		Defeated = defeated;
		KnockedBackTo = knockedBackTo;
	}
}

// What one executed attack (AttackTurnResult.Attacked) actually did: who
// attacked, every cell the attack's pattern swept (regardless of whether it
// hit anything - replaces the previous default-(0,0) TargetCell), and each
// target actually hit. Null while an attack is only preparing, since nothing
// has executed yet.
public sealed class AttackExecutionDetail
{
	public Guid AttackerInstanceId { get; }
	public IReadOnlyList<GridPosition> AffectedCells { get; }
	public IReadOnlyList<AttackHitDetail> Hits { get; }

	public AttackExecutionDetail(
		Guid attackerInstanceId,
		IReadOnlyList<GridPosition> affectedCells,
		IReadOnlyList<AttackHitDetail> hits)
	{
		AttackerInstanceId = attackerInstanceId;
		AffectedCells = affectedCells;
		Hits = hits;
	}
}
