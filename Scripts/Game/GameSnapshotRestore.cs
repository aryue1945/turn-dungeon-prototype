using System;

// The inverse of GameSnapshot.Capture, for NEXT_STEPS milestone 4
// (save/resume): rebuilds map and actor state from previously captured
// snapshots instead of running fresh generation/setup. The save envelope,
// file I/O and Main's load flow (which knows how to turn a DefinitionId or
// WeaponId back into a MonsterDefinition/WeaponDefinition and build the
// actual Enemy/Player nodes) are separate, later slices.
//
// Lives in Scripts/Game rather than Scripts/Dungeon or Scripts/Actors on
// purpose: DungeonMap/ActorState/AttackState stay unaware of
// GameSnapshot/CellSnapshot/ActorSnapshot (they only expose generic
// primitives - RestoreCell, the ActorState constructor plus existing
// setters, RestorePreparation), so those layers do not depend on the Game
// layer that already depends on them.
public static class GameSnapshotRestore
{
	public static DungeonMap RestoreMap(GridSnapshot grid, int seed)
	{
		if (grid == null)
			throw new ArgumentNullException(nameof(grid));

		DungeonMap map = new(grid.Width, grid.Height, seed);

		for (int y = 0; y < grid.Height; y++)
		{
			for (int x = 0; x < grid.Width; x++)
			{
				CellSnapshot cell = grid.Rows[y][x];
				map.RestoreCell(
					x,
					y,
					cell.Terrain,
					cell.Durability,
					cell.IsOpen,
					cell.ZoneId,
					cell.ConnectedZoneA,
					cell.ConnectedZoneB
				);
			}
		}

		return map;
	}

	// Rebuilds an ActorState from an ActorSnapshot, using only ActorState's
	// existing public constructor/setters (no new backdoor mutators). attack
	// is the AttackState the actor should reference - the caller resolves
	// which AttackDefinition that is (from the snapshot's WeaponId for a
	// player or DefinitionId for an enemy), since that lookup depends on
	// which kind of actor this is, unlike everything else here.
	public static ActorState RestoreActor(ActorSnapshot snapshot, AttackState attack)
	{
		if (snapshot == null)
			throw new ArgumentNullException(nameof(snapshot));

		if (attack == null)
			throw new ArgumentNullException(nameof(attack));

		ActorState state = new(snapshot.Position, snapshot.MaxHealth, snapshot.DefinitionId);

		int damageTaken = snapshot.MaxHealth - snapshot.Health;
		if (damageTaken > 0)
			state.TakeDamage(damageTaken);

		state.SetFacing(snapshot.Facing);
		state.SetHasPreparedMove(snapshot.HasPreparedMove);
		state.SetEquipment(snapshot.WeaponId, snapshot.ToolId);

		attack.RestorePreparation(
			snapshot.IsAttackPreparing,
			snapshot.AttackRemainingPreparationTurns,
			snapshot.AttackPreparedDirection
		);
		state.SetAttack(attack);

		return state;
	}
}
