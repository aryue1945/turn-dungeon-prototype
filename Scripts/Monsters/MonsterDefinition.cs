using System;
using System.Collections.Generic;
using System.Linq;

// Data-only description of a monster. Both the built-in roster
// (MonsterDefinitions) and mod-loaded monsters (MonsterModLoader) produce
// these. Runtime code (Enemy) reads a MonsterDefinition instead of hard-coding
// stats, sprites, or behavior, so a modder can add a monster without touching
// C#.
public sealed class MonsterDefinition
{
	public string Id { get; }
	public string Name { get; }
	public int Health { get; }
	public string SpritePath { get; }
	public string MovementBehaviorId { get; }
	public IReadOnlyList<AttackDefinition> Attacks { get; }
	public string LootTableId { get; }

	public AttackDefinition PrimaryAttack => Attacks[0];

	public MonsterDefinition(
		string id,
		string name,
		int health,
		string spritePath,
		string movementBehaviorId,
		IReadOnlyList<AttackDefinition> attacks,
		string lootTableId = null)
	{
		if (string.IsNullOrWhiteSpace(id))
			throw new ArgumentException("A monster definition requires an id.");

		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("A monster definition requires a name.");

		if (health <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(health),
				"A monster definition requires positive health."
			);

		if (string.IsNullOrWhiteSpace(spritePath))
			throw new ArgumentException("A monster definition requires a sprite path.");

		if (string.IsNullOrWhiteSpace(movementBehaviorId))
			throw new ArgumentException("A monster definition requires a movement behavior id.");

		if (attacks == null || attacks.Count == 0)
			throw new ArgumentException("A monster definition requires at least one attack.");

		Id = id;
		Name = name;
		Health = health;
		SpritePath = spritePath;
		MovementBehaviorId = movementBehaviorId;
		Attacks = attacks.ToList();
		LootTableId = lootTableId;
	}
}
