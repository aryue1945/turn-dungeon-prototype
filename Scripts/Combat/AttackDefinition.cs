using Godot;
using System;

public readonly struct AttackOffset
{
	public int Forward { get; }
	public int Right { get; }

	public AttackOffset(int forward, int right)
	{
		Forward = forward;
		Right = right;
	}
}

public enum AttackTargetRule
{
	OpponentsOnly,
	AllExceptAttacker
}

public enum AttackTurnResult
{
	NoAttack,
	Preparing,
	Attacked
}

public enum CombatFaction
{
	Player,
	Enemy
}

public interface ICombatant
{
	Guid InstanceId { get; }
	GridPosition GridPosition { get; }
	CombatFaction Faction { get; }
	bool IsAlive { get; }
	int Health { get; }

	void TakeDamage(int damage);

	// Moves exactly one cell in direction (a cardinal unit vector), syncing
	// both authoritative GridPosition and pixel Position. The caller
	// (AttackResolver) is responsible for confirming the destination is
	// walkable and unoccupied first - this method itself does not check.
	void Knockback(Vector2 direction);
}

public sealed class AttackDefinition
{
	public string Name { get; }
	public int Damage { get; }
	public int PreparationTurns { get; }
	public AttackOffset[] DetectionOffsets { get; }
	public AttackOffset[] AttackOffsets { get; }
	public AttackTargetRule TargetRule { get; }
	public bool StopsAtWalls { get; }
	public int MaxTargets { get; }

	// War Hammer support: a surviving hit target is pushed one cell in the
	// attack direction if that destination is walkable and unoccupied
	// (AttackResolver.ExecuteAttack) - a plain bool rather than a distance,
	// since nothing yet needs more than one cell. Generalize only once a
	// second mechanic needs a different distance.
	public bool Knockback { get; }

	// Not yet consumed by AttackResolver. Carried through so data-driven
	// attacks (e.g. modded monsters) can tag a status effect ahead of the
	// combat system actually applying one.
	public string StatusEffectId { get; }

	public AttackDefinition(
		string name,
		int damage,
		int preparationTurns,
		AttackOffset[] detectionOffsets,
		AttackOffset[] attackOffsets,
		AttackTargetRule targetRule,
		bool stopsAtWalls,
		int maxTargets,
		string statusEffectId = null,
		bool knockback = false)
	{
		Name = name;
		Damage = damage;
		PreparationTurns = preparationTurns;
		DetectionOffsets = detectionOffsets;
		AttackOffsets = attackOffsets;
		TargetRule = targetRule;
		StopsAtWalls = stopsAtWalls;
		MaxTargets = maxTargets;
		StatusEffectId = statusEffectId;
		Knockback = knockback;
	}
}

public sealed class AttackState
{
	public AttackDefinition Definition { get; private set; }
	public bool IsPreparing { get; private set; }
	public int RemainingPreparationTurns { get; private set; }
	public Vector2 PreparedDirection { get; private set; }

	public AttackState(AttackDefinition definition)
	{
		Definition = definition;
	}

	public void Equip(AttackDefinition definition)
	{
		Definition = definition;
		CancelPreparation();
	}

	public void BeginPreparation(Vector2 direction)
	{
		PreparedDirection = direction;
		RemainingPreparationTurns = Definition.PreparationTurns;
		IsPreparing = true;
	}

	public bool AdvancePreparation()
	{
		if (!IsPreparing)
			return false;

		RemainingPreparationTurns--;

		return RemainingPreparationTurns <= 0;
	}

	public void CompletePreparation()
	{
		IsPreparing = false;
		RemainingPreparationTurns = 0;
		PreparedDirection = Vector2.Zero;
	}

	public void CancelPreparation()
	{
		IsPreparing = false;
		RemainingPreparationTurns = 0;
		PreparedDirection = Vector2.Zero;
	}

	// Sets preparation state directly, for milestone-4 save/resume - unlike
	// BeginPreparation, remainingPreparationTurns is not reset to
	// Definition.PreparationTurns, since a restored attack may be partway
	// through preparing.
	public void RestorePreparation(
		bool isPreparing,
		int remainingPreparationTurns,
		Vector2 preparedDirection)
	{
		IsPreparing = isPreparing;
		RemainingPreparationTurns = isPreparing ? remainingPreparationTurns : 0;
		PreparedDirection = isPreparing ? preparedDirection : Vector2.Zero;
	}
}

public static class AttackDefinitions
{
	private static readonly AttackOffset[] OneCellForward =
	{
		new(1, 0)
	};

	public static readonly AttackDefinition BasicEnemyStrike = new(
		name: "Basic Enemy Strike",
		damage: 1,
		preparationTurns: 0,
		detectionOffsets: OneCellForward,
		attackOffsets: OneCellForward,
		targetRule: AttackTargetRule.OpponentsOnly,
		stopsAtWalls: true,
		maxTargets: 1
	);

	// Charging Beetle's attack (NEXT_STEPS roadmap item 3) - triggers via
	// the same one-cell-forward detection as any other enemy attack, so a
	// charge that ends adjacent to a target attacks instead of moving onto
	// it, exactly like Basic Enemy Strike already does for slower enemies.
	// Hits harder to make the charge feel distinct.
	public static readonly AttackDefinition ChargeSlam = new(
		name: "Charge Slam",
		damage: 2,
		preparationTurns: 0,
		detectionOffsets: OneCellForward,
		attackOffsets: OneCellForward,
		targetRule: AttackTargetRule.OpponentsOnly,
		stopsAtWalls: true,
		maxTargets: 1
	);
}
