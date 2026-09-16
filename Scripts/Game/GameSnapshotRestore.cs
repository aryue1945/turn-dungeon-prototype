using System;

// The inverse of GameSnapshot.Capture, for NEXT_STEPS milestone 4
// (save/resume): rebuilds a DungeonMap from a previously captured
// GridSnapshot instead of running fresh seeded generation. This is the
// terrain half of "restore actual terrain, actors, equipment and intent" -
// actor restoration, the save envelope, file I/O and Main's load flow are
// separate, later slices.
//
// Lives in Scripts/Game rather than Scripts/Dungeon on purpose: DungeonMap
// stays unaware of GameSnapshot/CellSnapshot (it only exposes the generic
// internal RestoreCell primitive), so the Dungeon layer does not depend on
// the Game layer that already depends on it.
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
}
