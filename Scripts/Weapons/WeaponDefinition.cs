public sealed class WeaponDefinition : IEquipment
{
	public string Name { get; }
	public AttackDefinition PrimaryAttack { get; }

	public WeaponDefinition(
		string name,
		AttackDefinition primaryAttack)
	{
		Name = name;
		PrimaryAttack = primaryAttack;
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
		)
	);

	public static readonly WeaponDefinition LongSword = new(
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
		)
	);
}
