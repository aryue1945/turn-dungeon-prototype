using Godot;
using System;

// The authoritative, engine-independent half of an actor: where it is on
// the grid, how much health it has left, which way it is facing, and
// whether a movement behavior has a move locked in for next turn. Godot
// nodes (Player, Enemy) own one of these and treat it as the source of
// truth, deriving their pixel Position from it rather than the other way
// around.
//
// This is the ActorState/GameState migration described in
// docs/ARCHITECTURE.md - a unique instance id, a definition id, GridPosition,
// health, facing, the one bit of enemy-behavior state (HasPreparedMove) that
// previously lived only on ChasePlayerBehavior, equipment ids, and a
// reference to the actor's AttackState. Equipment/Attack are still owned and
// mutated by Player/Enemy directly (weapon swaps, attack preparation) -
// ActorState just holds the same references/ids so a reader (GameState,
// eventually a snapshot) does not need the Godot node to see them.
public sealed class ActorState
{
	public Guid InstanceId { get; } = Guid.NewGuid();
	public string DefinitionId { get; }
	public GridPosition GridPosition { get; private set; }
	public int Health { get; private set; }
	public int MaxHealth { get; }
	public bool IsAlive => Health > 0;
	public Vector2 Facing { get; private set; }
	public bool HasPreparedMove { get; private set; }
	public AttackState Attack { get; private set; }
	public string WeaponId { get; private set; }
	public string ToolId { get; private set; }

	public ActorState(GridPosition gridPosition, int maxHealth, string definitionId)
	{
		if (maxHealth <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxHealth));

		if (string.IsNullOrWhiteSpace(definitionId))
			throw new ArgumentException(
				"An actor state requires a definition id.",
				nameof(definitionId)
			);

		GridPosition = gridPosition;
		Health = maxHealth;
		MaxHealth = maxHealth;
		DefinitionId = definitionId;
	}

	public void MoveTo(GridPosition gridPosition)
	{
		GridPosition = gridPosition;
	}

	public void MoveBy(int deltaX, int deltaY)
	{
		GridPosition = new GridPosition(
			GridPosition.X + deltaX,
			GridPosition.Y + deltaY
		);
	}

	public void TakeDamage(int damage)
	{
		if (damage <= 0 || Health <= 0)
			return;

		Health = Math.Max(0, Health - damage);
	}

	public void SetFacing(Vector2 facing)
	{
		Facing = facing;
	}

	public void SetHasPreparedMove(bool hasPreparedMove)
	{
		HasPreparedMove = hasPreparedMove;
	}

	// The same AttackState instance the owning Player/Enemy already mutates
	// (preparation, equip). Call once; reads through ActorState.Attack
	// automatically see later changes since it is a shared reference, not a
	// copy.
	public void SetAttack(AttackState attack)
	{
		Attack = attack ?? throw new ArgumentNullException(nameof(attack));
	}

	// Player-only for now (Enemy has no separate weapon/tool, just its
	// definition's attack) - call again whenever equipment changes.
	public void SetEquipment(string weaponId, string toolId)
	{
		WeaponId = weaponId;
		ToolId = toolId;
	}
}
