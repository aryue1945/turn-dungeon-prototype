using System.Collections.Generic;

// The game's built-in monster roster, expressed as data rather than
// hard-coded C# behavior. This preserves the five original enemy types
// exactly (same health, sprites, and movement) but sitting on the same
// MonsterDefinition shape that a mod's JSON produces, using the same
// stable-id convention ("core.*") mods are expected to follow ("mod-id.*").
public static class MonsterDefinitions
{
	public static readonly MonsterDefinition SlowChaser = new(
		id: "core.slow_chaser",
		name: "Slow Chaser",
		health: 2,
		spritePath: "res://Art/Actors/enemy_slow_chaser.png",
		movementBehaviorId: "chase_player",
		attacks: new[] { AttackDefinitions.BasicEnemyStrike }
	);

	public static readonly MonsterDefinition Patroller = new(
		id: "core.patroller",
		name: "Patroller",
		health: 2,
		spritePath: "res://Art/Actors/enemy_patroller.png",
		movementBehaviorId: "patrol",
		attacks: new[] { AttackDefinitions.BasicEnemyStrike }
	);

	public static readonly MonsterDefinition LeftTurner = new(
		id: "core.left_turner",
		name: "Left Turner",
		health: 2,
		spritePath: "res://Art/Actors/enemy_left_turner.png",
		movementBehaviorId: "turn_left",
		attacks: new[] { AttackDefinitions.BasicEnemyStrike }
	);

	public static readonly MonsterDefinition RightTurner = new(
		id: "core.right_turner",
		name: "Right Turner",
		health: 2,
		spritePath: "res://Art/Actors/enemy_right_turner.png",
		movementBehaviorId: "turn_right",
		attacks: new[] { AttackDefinitions.BasicEnemyStrike }
	);

	public static readonly MonsterDefinition Stationary = new(
		id: "core.stationary",
		name: "Stationary",
		health: 2,
		spritePath: "res://Art/Actors/enemy_stationary.png",
		movementBehaviorId: "stationary",
		attacks: new[] { AttackDefinitions.BasicEnemyStrike }
	);

	// First tactical-slice enemy (NEXT_STEPS roadmap item 3): telegraphs a
	// charge direction for one turn, then charges up to two cells
	// (ChargingBeetleBehavior). Reuses the Slow Chaser's sprite as a
	// placeholder pending real art.
	public static readonly MonsterDefinition ChargingBeetle = new(
		id: "core.charging_beetle",
		name: "Charging Beetle",
		health: 3,
		spritePath: "res://Art/Actors/enemy_slow_chaser.png",
		movementBehaviorId: "charge_beetle",
		attacks: new[] { AttackDefinitions.ChargeSlam }
	);

	public static readonly IReadOnlyList<MonsterDefinition> All = new[]
	{
		SlowChaser,
		Patroller,
		LeftTurner,
		RightTurner,
		Stationary,
		ChargingBeetle
	};
}
