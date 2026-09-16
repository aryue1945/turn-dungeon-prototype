public sealed class WeaponDefinition : IEquipment
{
	public string Id { get; }
	public string Name { get; }
	public AttackDefinition PrimaryAttack { get; }

	// Optional (null when a weapon has no icon yet) - a res:// path to a
	// small icon, sized in whole grid cells matching the weapon's reach
	// (32x32 per cell it hits forward - see Art/Weapons). Loaded by Main,
	// not here; this class stays engine-agnostic like MonsterDefinition's
	// SpritePath.
	public string SpritePath { get; }

	public WeaponDefinition(
		string id,
		string name,
		AttackDefinition primaryAttack,
		string spritePath = null)
	{
		if (string.IsNullOrWhiteSpace(id))
			throw new System.ArgumentException("A weapon definition requires an id.");

		if (string.IsNullOrWhiteSpace(name))
			throw new System.ArgumentException("A weapon definition requires a name.");

		Id = id;
		Name = name;
		PrimaryAttack = primaryAttack;
		SpritePath = spritePath;
	}
}

public static class WeaponDefinitions
{
	private static readonly AttackOffset[] OneCellForward =
	{
		new(1, 0)
	};

	private static readonly AttackOffset[] TwoCellsForward =
	{
		new(1, 0),
		new(2, 0)
	};

	public static readonly WeaponDefinition BasicSword = new(
		id: "core.basic_sword",
		name: "Basic Sword",
		primaryAttack: new AttackDefinition(
			name: "Sword Strike",
			damage: 1,
			preparationTurns: 0,
			detectionOffsets: OneCellForward,
			attackOffsets: OneCellForward,
			targetRule: AttackTargetRule.OpponentsOnly,
			stopsAtWalls: true,
			maxTargets: 1
		),
		spritePath: "res://Art/Weapons/basic_sword.svg"
	);

	public static readonly WeaponDefinition LongSword = new(
		id: "core.long_sword",
		name: "Long Sword",
		primaryAttack: new AttackDefinition(
			name: "Long Sword Strike",
			damage: 1,
			preparationTurns: 0,
			detectionOffsets: TwoCellsForward,
			attackOffsets: TwoCellsForward,
			targetRule: AttackTargetRule.OpponentsOnly,
			stopsAtWalls: true,
			maxTargets: 1
		),
		spritePath: "res://Art/Weapons/long_sword.svg"
	);

	// First tactical-slice weapon (NEXT_STEPS roadmap item 3): same reach
	// and damage as the basic sword, but pushes a surviving target one
	// cell back when the destination is walkable and unoccupied
	// (AttackDefinition.Knockback, applied in AttackResolver.ExecuteAttack).
	public static readonly WeaponDefinition WarHammer = new(
		id: "core.war_hammer",
		name: "War Hammer",
		primaryAttack: new AttackDefinition(
			name: "Hammer Slam",
			damage: 1,
			preparationTurns: 0,
			detectionOffsets: OneCellForward,
			attackOffsets: OneCellForward,
			targetRule: AttackTargetRule.OpponentsOnly,
			stopsAtWalls: true,
			maxTargets: 1,
			knockback: true
		),
		spritePath: "res://Art/Weapons/war_hammer.svg"
	);

	public static readonly System.Collections.Generic.IReadOnlyList<WeaponDefinition> All = new[]
	{
		BasicSword,
		LongSword,
		WarHammer
	};

	// Resolves a saved WeaponId back into its definition for milestone-4
	// Continue - null (not thrown) when the id is unknown, since the caller
	// needs to report a useful error rather than crash on a stale/modded id.
	public static WeaponDefinition FindById(string id)
	{
		foreach (WeaponDefinition weapon in All)
		{
			if (weapon.Id == id)
				return weapon;
		}

		return null;
	}
}
