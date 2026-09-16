using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

// Spike trap cycling (DungeonMap.TickSpikeTraps/DungeonCell.TickSpikeTrap)
// and damage (EnvironmentPhaseResolver.ResolveSpikeTraps) - NEXT_STEPS
// roadmap item 3. Hand-built maps, matching DynamicTerrainTests' pattern,
// since spike traps are not yet placed by procedural generation.
[TestFixture]
public sealed class SpikeTrapTests
{
	private const int Size = 5;

	[Test]
	public void TickSpikeTraps_CyclesSafeThenWarningThenActiveThenBackToSafe()
	{
		DungeonMap map = CreateMapWithSpikeTrap(out GridPosition spike);
		List<SpikeTrapPhase> observedPhases = new() { map.GetCell(spike.X, spike.Y).SpikeTrapPhase };

		for (int tick = 0; tick < 4; tick++)
		{
			map.TickSpikeTraps();
			observedPhases.Add(map.GetCell(spike.X, spike.Y).SpikeTrapPhase);
		}

		// Safe(2 turns) -> Warning(1) -> Active(1) -> Safe again.
		Assert.That(observedPhases, Is.EqualTo(new[]
		{
			SpikeTrapPhase.Safe,
			SpikeTrapPhase.Safe,
			SpikeTrapPhase.Warning,
			SpikeTrapPhase.Active,
			SpikeTrapPhase.Safe
		}));
	}

	[Test]
	public void TickSpikeTraps_ReportsActiveCellsOnlyWhileActive()
	{
		DungeonMap map = CreateMapWithSpikeTrap(out GridPosition spike);

		(_, IReadOnlyList<GridPosition> activeTurn1) = map.TickSpikeTraps();
		Assert.That(activeTurn1, Is.Empty, "Still Safe after the first tick.");

		(_, IReadOnlyList<GridPosition> activeTurn2) = map.TickSpikeTraps();
		Assert.That(activeTurn2, Is.Empty, "Warning is not Active.");

		(_, IReadOnlyList<GridPosition> activeTurn3) = map.TickSpikeTraps();
		Assert.That(activeTurn3, Is.EqualTo(new[] { spike }));
	}

	[Test]
	public void TickSpikeTraps_ChangedCellsIncludesLeavingActive()
	{
		DungeonMap map = CreateMapWithSpikeTrap(out GridPosition spike);

		map.TickSpikeTraps();
		map.TickSpikeTraps();
		(IReadOnlyList<GridPosition> changedIntoActive, _) = map.TickSpikeTraps();
		Assert.That(changedIntoActive, Is.EqualTo(new[] { spike }));

		(IReadOnlyList<GridPosition> changedOutOfActive, _) = map.TickSpikeTraps();
		Assert.That(changedOutOfActive, Is.EqualTo(new[] { spike }), "Leaving Active must also report a change.");
	}

	[Test]
	public void TickSpikeTraps_OnlyEverReportsTheSpikeTrapCellNotSurroundingFloorOrWalls()
	{
		DungeonMap map = CreateMapWithSpikeTrap(out GridPosition spike);

		for (int tick = 0; tick < 6; tick++)
		{
			(IReadOnlyList<GridPosition> changed, IReadOnlyList<GridPosition> active) = map.TickSpikeTraps();

			foreach (GridPosition position in changed.Concat(active))
				Assert.That(position, Is.EqualTo(spike));
		}
	}

	[Test]
	public void ResolveSpikeTraps_DamagesACombatantStandingOnAnActiveCell()
	{
		DungeonMap map = CreateMapWithSpikeTrap(out GridPosition spike);
		FakeCombatant onSpike = new(spike);
		FakeCombatant elsewhere = new(new GridPosition(1, 1));

		// Two manual ticks bring the cell to the edge of Warning->Active;
		// ResolveSpikeTraps's own tick (the third) is the one that actually
		// activates it, so the damage must apply within that same call.
		map.TickSpikeTraps();
		map.TickSpikeTraps();
		EnvironmentPhaseResolver.ResolveSpikeTraps(map, new List<ICombatant> { onSpike, elsewhere });

		Assert.That(map.GetCell(spike.X, spike.Y).SpikeTrapPhase, Is.EqualTo(SpikeTrapPhase.Active));
		Assert.That(onSpike.Health, Is.EqualTo(2), "1 contact damage from the starting 3.");
		Assert.That(elsewhere.Health, Is.EqualTo(3), "Not standing on the active cell.");
	}

	[Test]
	public void ResolveSpikeTraps_DoesNotDamageDuringSafeOrWarning()
	{
		DungeonMap map = CreateMapWithSpikeTrap(out GridPosition spike);
		FakeCombatant onSpike = new(spike);

		// Still Safe.
		EnvironmentPhaseResolver.ResolveSpikeTraps(map, new List<ICombatant> { onSpike });
		Assert.That(onSpike.Health, Is.EqualTo(3));

		// Now Warning.
		EnvironmentPhaseResolver.ResolveSpikeTraps(map, new List<ICombatant> { onSpike });
		Assert.That(onSpike.Health, Is.EqualTo(3));
	}

	[Test]
	public void RestoreCell_PreservesSpikeTrapPhaseAndTimerExactly()
	{
		DungeonMap map = CreateMapWithSpikeTrap(out GridPosition spike);
		AdvanceToActive(map);

		DungeonCell original = map.GetCell(spike.X, spike.Y);
		DungeonMap restored = new(map.Width, map.Height, seed: 1);
		restored.RestoreCell(
			spike.X,
			spike.Y,
			original.Terrain.Kind,
			original.Durability,
			original.IsOpen,
			original.ZoneId,
			original.ConnectedZoneA,
			original.ConnectedZoneB,
			original.SpikeTrapPhase,
			original.SpikeTrapPhaseTurnsRemaining
		);

		DungeonCell restoredCell = restored.GetCell(spike.X, spike.Y);
		Assert.That(restoredCell.SpikeTrapPhase, Is.EqualTo(SpikeTrapPhase.Active));
		Assert.That(restoredCell.SpikeTrapPhaseTurnsRemaining, Is.EqualTo(original.SpikeTrapPhaseTurnsRemaining));
	}

	[Test]
	public void GameSnapshot_CapturesSpikeTrapPhaseAndTimer()
	{
		DungeonMap map = CreateMapWithSpikeTrap(out GridPosition spike);
		AdvanceToActive(map);

		ActorState player = new(new GridPosition(1, 1), maxHealth: 3, "core.player");
		GameState state = new(map, player);

		GameSnapshot snapshot = GameSnapshot.Capture(state);
		CellSnapshot cellSnapshot = snapshot.Grid.GetCell(spike.X, spike.Y);

		Assert.That(cellSnapshot.SpikeTrapPhase, Is.EqualTo(SpikeTrapPhase.Active));
		Assert.That(cellSnapshot.SpikeTrapPhaseTurnsRemaining, Is.EqualTo(map.GetCell(spike.X, spike.Y).SpikeTrapPhaseTurnsRemaining));
	}

	[Test]
	public void GameSnapshotRestore_RoundTripsSpikeTrapPhaseAndTimer()
	{
		DungeonMap map = CreateMapWithSpikeTrap(out GridPosition spike);
		AdvanceToActive(map);

		ActorState player = new(new GridPosition(1, 1), maxHealth: 3, "core.player");
		GameState state = new(map, player);

		GameSnapshot snapshot = GameSnapshot.Capture(state);
		DungeonMap restored = GameSnapshotRestore.RestoreMap(snapshot.Grid, map.Seed);

		Assert.That(restored.GetCell(spike.X, spike.Y).SpikeTrapPhase, Is.EqualTo(SpikeTrapPhase.Active));
		Assert.That(
			restored.GetCell(spike.X, spike.Y).SpikeTrapPhaseTurnsRemaining,
			Is.EqualTo(map.GetCell(spike.X, spike.Y).SpikeTrapPhaseTurnsRemaining)
		);
	}

	private static void AdvanceToActive(DungeonMap map)
	{
		map.TickSpikeTraps();
		map.TickSpikeTraps();
		map.TickSpikeTraps();
	}

	private static DungeonMap CreateMapWithSpikeTrap(out GridPosition placedAt)
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

		placedAt = new GridPosition(2, 2);
		map.SetTerrain(placedAt.X, placedAt.Y, TerrainKind.SpikeTrap);
		return map;
	}

	private sealed class FakeCombatant : ICombatant
	{
		public Guid InstanceId { get; } = Guid.NewGuid();
		public GridPosition GridPosition { get; }
		public CombatFaction Faction => CombatFaction.Player;
		public int Health { get; private set; } = 3;
		public bool IsAlive => Health > 0;

		public FakeCombatant(GridPosition gridPosition)
		{
			GridPosition = gridPosition;
		}

		public void TakeDamage(int damage)
		{
			Health = Math.Max(0, Health - damage);
		}

		public void Knockback(Godot.Vector2 direction)
		{
		}
	}
}
