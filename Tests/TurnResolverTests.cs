using Godot;
using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public sealed class TurnResolverTests
{
	private const int Size = 5;

	[Test]
	public void ResolvePlayerAction_AttacksAdjacentEnemy()
	{
		DungeonMap map = CreateOpenRoom();
		FakePlayerActor player = new(new GridPosition(2, 2), WeaponDefinitions.BasicSword.PrimaryAttack);
		FakeCombatant enemy = new(new GridPosition(3, 2), CombatFaction.Enemy);

		PlayerActionOutcome outcome = TurnResolver.ResolvePlayerAction(
			player,
			Vector2.Right,
			new List<ICombatant> { player, enemy },
			map,
			position => !map.IsWalkable(position.X, position.Y)
		);

		Assert.That(outcome.Kind, Is.EqualTo(PlayerActionKind.Attacked));
		Assert.That(outcome.AttackName, Is.EqualTo(WeaponDefinitions.BasicSword.PrimaryAttack.Name));
		Assert.That(enemy.Health, Is.EqualTo(2));
		Assert.That(player.MoveCallCount, Is.EqualTo(0));
	}

	[Test]
	public void ResolvePlayerAction_ReturnsPreparingForAPreparationAttack()
	{
		DungeonMap map = CreateOpenRoom();
		AttackDefinition chargedStrike = new(
			name: "Charged Strike",
			damage: 3,
			preparationTurns: 1,
			detectionOffsets: new[] { new AttackOffset(1, 0) },
			attackOffsets: new[] { new AttackOffset(1, 0) },
			targetRule: AttackTargetRule.OpponentsOnly,
			stopsAtWalls: true,
			maxTargets: 1
		);
		FakePlayerActor player = new(new GridPosition(2, 2), chargedStrike);
		FakeCombatant enemy = new(new GridPosition(3, 2), CombatFaction.Enemy);

		PlayerActionOutcome outcome = TurnResolver.ResolvePlayerAction(
			player,
			Vector2.Right,
			new List<ICombatant> { player, enemy },
			map,
			position => !map.IsWalkable(position.X, position.Y)
		);

		Assert.That(outcome.Kind, Is.EqualTo(PlayerActionKind.Preparing));
		Assert.That(enemy.Health, Is.EqualTo(3), "Preparing must not deal damage yet.");
		Assert.That(player.MoveCallCount, Is.EqualTo(0));
	}

	[Test]
	public void ResolvePlayerAction_DestroysBreakableWallWhenNoAttackTarget()
	{
		DungeonMap map = CreateOpenRoom();
		map.SetTerrain(3, 2, TerrainKind.BreakableWall);
		FakePlayerActor player = new(new GridPosition(2, 2), WeaponDefinitions.BasicSword.PrimaryAttack);

		PlayerActionOutcome outcome = TurnResolver.ResolvePlayerAction(
			player,
			Vector2.Right,
			new List<ICombatant> { player },
			map,
			position => !map.IsWalkable(position.X, position.Y)
		);

		Assert.That(outcome.Kind, Is.EqualTo(PlayerActionKind.TerrainDestroyed));
		Assert.That(outcome.TargetCell, Is.EqualTo(new GridPosition(3, 2)));
		Assert.That(map.GetCell(3, 2).Terrain.Kind, Is.EqualTo(TerrainKind.Floor));
		Assert.That(player.MoveCallCount, Is.EqualTo(0));
	}

	[Test]
	public void ResolvePlayerAction_MovesIntoOpenFloor()
	{
		DungeonMap map = CreateOpenRoom();
		FakePlayerActor player = new(new GridPosition(2, 2), WeaponDefinitions.BasicSword.PrimaryAttack);

		PlayerActionOutcome outcome = TurnResolver.ResolvePlayerAction(
			player,
			Vector2.Right,
			new List<ICombatant> { player },
			map,
			position => !map.IsWalkable(position.X, position.Y)
		);

		Assert.That(outcome.Kind, Is.EqualTo(PlayerActionKind.Moved));
		Assert.That(outcome.DoorOpened, Is.False);
		Assert.That(player.MoveCallCount, Is.EqualTo(1));
		Assert.That(player.GridPosition, Is.EqualTo(new GridPosition(3, 2)));
	}

	[Test]
	public void ResolvePlayerAction_OpensDoorWhenMovingOntoIt()
	{
		DungeonMap map = CreateOpenRoom();
		map.SetDoor(3, 2, connectedZoneA: 0, connectedZoneB: 1);
		FakePlayerActor player = new(new GridPosition(2, 2), WeaponDefinitions.BasicSword.PrimaryAttack);

		PlayerActionOutcome outcome = TurnResolver.ResolvePlayerAction(
			player,
			Vector2.Right,
			new List<ICombatant> { player },
			map,
			position => !map.IsWalkable(position.X, position.Y)
		);

		Assert.That(outcome.Kind, Is.EqualTo(PlayerActionKind.Moved));
		Assert.That(outcome.DoorOpened, Is.True);
		Assert.That(map.GetCell(3, 2).IsOpen, Is.True);
	}

	[Test]
	public void ResolvePlayerAction_ReturnsBlockedAtSolidWall()
	{
		DungeonMap map = CreateOpenRoom();
		FakePlayerActor player = new(new GridPosition(1, 2), WeaponDefinitions.BasicSword.PrimaryAttack);

		PlayerActionOutcome outcome = TurnResolver.ResolvePlayerAction(
			player,
			Vector2.Left,
			new List<ICombatant> { player },
			map,
			position => !map.IsWalkable(position.X, position.Y)
		);

		Assert.That(outcome.Kind, Is.EqualTo(PlayerActionKind.Blocked));
		Assert.That(outcome.TargetCell, Is.EqualTo(new GridPosition(0, 2)));
		Assert.That(player.MoveCallCount, Is.EqualTo(0));
	}

	// A solid-walled 5x5 room, floor everywhere inside.
	private static DungeonMap CreateOpenRoom()
	{
		DungeonMap map = new(Size, Size, seed: 1);

		for (int y = 0; y < Size; y++)
		{
			for (int x = 0; x < Size; x++)
			{
				bool isBoundary = x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
				map.SetTerrain(x, y, isBoundary ? TerrainKind.SolidWall : TerrainKind.Floor);
			}
		}

		return map;
	}

	private sealed class FakePlayerActor : IPlayerTurnActor
	{
		public System.Guid InstanceId { get; } = System.Guid.NewGuid();
		public GridPosition GridPosition { get; private set; }
		public CombatFaction Faction => CombatFaction.Player;
		public int Health { get; private set; } = 3;
		public bool IsAlive => Health > 0;
		public AttackState Attack { get; }
		public DiggingToolDefinition DiggingTool => DiggingToolDefinitions.BasicShovel;
		public int MoveCallCount { get; private set; }

		public FakePlayerActor(GridPosition gridPosition, AttackDefinition attack)
		{
			GridPosition = gridPosition;
			Attack = new AttackState(attack);
		}

		public void TakeDamage(int damage)
		{
			Health = System.Math.Max(0, Health - damage);
		}

		public void Move(Vector2 direction)
		{
			MoveCallCount++;
			GridPosition = new GridPosition(
				GridPosition.X + (int)direction.X,
				GridPosition.Y + (int)direction.Y
			);
		}

		public void Knockback(Vector2 direction)
		{
			GridPosition = new GridPosition(
				GridPosition.X + (int)direction.X,
				GridPosition.Y + (int)direction.Y
			);
		}
	}

	private sealed class FakeCombatant : ICombatant
	{
		public System.Guid InstanceId { get; } = System.Guid.NewGuid();
		public GridPosition GridPosition { get; private set; }
		public CombatFaction Faction { get; }
		public int Health { get; private set; } = 3;
		public bool IsAlive => Health > 0;

		public FakeCombatant(GridPosition gridPosition, CombatFaction faction)
		{
			GridPosition = gridPosition;
			Faction = faction;
		}

		public void Knockback(Vector2 direction)
		{
			GridPosition = new GridPosition(
				GridPosition.X + (int)direction.X,
				GridPosition.Y + (int)direction.Y
			);
		}

		public void TakeDamage(int damage)
		{
			Health = System.Math.Max(0, Health - damage);
		}
	}
}
