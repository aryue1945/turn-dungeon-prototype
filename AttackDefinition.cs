using Godot;

public enum AttackPattern
{
	FirstTarget,
	PiercingLine
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
	Vector2 Position { get; }
	CombatFaction Faction { get; }
	bool IsAlive { get; }

	void TakeDamage(int damage);
}

public sealed class AttackDefinition
{
	public string Name { get; }
	public int Damage { get; }
	public int ActivationRange { get; }
	public int EffectRange { get; }
	public int PreparationTurns { get; }
	public AttackPattern Pattern { get; }
	public AttackTargetRule TargetRule { get; }
	public bool StopsAtWalls { get; }
	public bool StopsAtActors { get; }

	public AttackDefinition(
		string name,
		int damage,
		int activationRange,
		int effectRange,
		int preparationTurns,
		AttackPattern pattern,
		AttackTargetRule targetRule,
		bool stopsAtWalls,
		bool stopsAtActors)
	{
		Name = name;
		Damage = damage;
		ActivationRange = activationRange;
		EffectRange = effectRange;
		PreparationTurns = preparationTurns;
		Pattern = pattern;
		TargetRule = targetRule;
		StopsAtWalls = stopsAtWalls;
		StopsAtActors = stopsAtActors;
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
	}

	public void CancelPreparation()
	{
		IsPreparing = false;
		RemainingPreparationTurns = 0;
		PreparedDirection = Vector2.Zero;
	}
}

public static class AttackDefinitions
{
	public static readonly AttackDefinition BasicStrike = new(
		name: "Basic Strike",
		damage: 1,
		activationRange: 1,
		effectRange: 1,
		preparationTurns: 0,
		pattern: AttackPattern.FirstTarget,
		targetRule: AttackTargetRule.OpponentsOnly,
		stopsAtWalls: true,
		stopsAtActors: true
	);
}
