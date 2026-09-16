using System.Collections.Generic;

// The per-turn environment phase (NEXT_STEPS roadmap item 3): ticks every
// spike trap's cycle and damages whoever is standing on a cell now in the
// Active phase. One function specific to spike traps, not a generic hazard
// framework - extract one only once a second environmental mechanic needs
// the same lifecycle. Pure logic (no Godot), same shape as TurnResolver:
// Main just calls this once per turn (after enemy actions, before the
// turn's snapshot/autosave) and narrates/refreshes whatever it reports.
public static class EnvironmentPhaseResolver
{
	// Returns every spike trap cell whose phase changed this turn (not just
	// the ones that became Active - a cell leaving Active also needs its
	// tint refreshed), so the caller can refresh their visuals.
	public static IReadOnlyList<GridPosition> ResolveSpikeTraps(
		DungeonMap map,
		IReadOnlyList<ICombatant> combatants)
	{
		(IReadOnlyList<GridPosition> changedCells, IReadOnlyList<GridPosition> activeCells) =
			map.TickSpikeTraps();

		if (activeCells.Count > 0)
		{
			int contactDamage = TerrainCatalog.Get(TerrainKind.SpikeTrap).ContactDamage;

			foreach (GridPosition position in activeCells)
			{
				foreach (ICombatant combatant in combatants)
				{
					if (combatant.IsAlive && combatant.GridPosition == position)
						combatant.TakeDamage(contactDamage);
				}
			}
		}

		return changedCells;
	}
}
