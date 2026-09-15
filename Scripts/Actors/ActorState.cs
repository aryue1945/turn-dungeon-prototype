using System;

// The authoritative, engine-independent half of an actor: where it is on
// the grid and how much health it has left. Godot nodes (Player, and later
// Enemy) own one of these and treat it as the source of truth, deriving
// their pixel Position from it rather than the other way around.
//
// This is the first slice of the ActorState/GameState migration described
// in docs/ARCHITECTURE.md - GridPosition and health only for now. Facing,
// equipment and attack/behavior state stay where they already live until
// Enemy is migrated too.
public sealed class ActorState
{
	public GridPosition GridPosition { get; private set; }
	public int Health { get; private set; }
	public int MaxHealth { get; }
	public bool IsAlive => Health > 0;

	public ActorState(GridPosition gridPosition, int maxHealth)
	{
		if (maxHealth <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxHealth));

		GridPosition = gridPosition;
		Health = maxHealth;
		MaxHealth = maxHealth;
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
}
