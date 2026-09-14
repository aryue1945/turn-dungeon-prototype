using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

[TestFixture]
public sealed class DungeonGeneratorTests
{
	private const int Width = 24;
	private const int Height = 16;
	private const int ZoneCount = 5;

	[Test]
	public void Generate_CreatesRequestedZonesAndRoles()
	{
		DungeonMap map = Generate(seed: 12345);

		Assert.That(map.Zones.Count, Is.EqualTo(ZoneCount));
		Assert.That(map.Rooms.Count, Is.EqualTo(ZoneCount));
		Assert.That(CountZones(map, DungeonZoneType.Start), Is.EqualTo(1));
		Assert.That(CountZones(map, DungeonZoneType.Shop), Is.EqualTo(1));
		Assert.That(CountZones(map, DungeonZoneType.Exit), Is.EqualTo(1));
		Assert.That(CountZones(map, DungeonZoneType.Combat), Is.EqualTo(2));
	}

	[Test]
	public void Generate_ConnectsEveryPhysicallyAdjacentZoneWithOneDoor()
	{
		DungeonMap map = Generate(seed: 54321);
		int connectionCount = 0;

		for (int first = 0; first < map.Zones.Count; first++)
		{
			for (int second = first + 1; second < map.Zones.Count; second++)
			{
				DungeonZone firstZone = map.Zones[first];
				DungeonZone secondZone = map.Zones[second];
				int distance = Math.Abs(
					firstZone.LayoutPosition.X - secondZone.LayoutPosition.X
				) + Math.Abs(
					firstZone.LayoutPosition.Y - secondZone.LayoutPosition.Y
				);

				if (distance != 1)
					continue;

				connectionCount++;
				Assert.That(
					firstZone.ConnectedZoneIds,
					Does.Contain(secondZone.Id)
				);
				Assert.That(
					secondZone.ConnectedZoneIds,
					Does.Contain(firstZone.Id)
				);
				Assert.That(
					CountDoorsConnecting(map, firstZone.Id, secondZone.Id),
					Is.EqualTo(1)
				);
			}
		}

		Assert.That(CountCells(map, TerrainKind.Door), Is.EqualTo(connectionCount));
	}

	[Test]
	public void Generate_MakesEveryWalkableCellReachable()
	{
		DungeonMap map = Generate(seed: 24680);
		DungeonZone startZone = map.Zones.Single(
			zone => zone.Type == DungeonZoneType.Start
		);

		HashSet<GridPosition> visited = FloodFill(map, startZone.Room.Center);

		Assert.That(visited.Count, Is.EqualTo(CountWalkableCells(map)));
	}

	[Test]
	public void Generate_EnclosesEveryWalkableCell()
	{
		DungeonMap map = Generate(seed: 13579);
		GridPosition[] directions = CardinalDirections();

		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				if (!map.IsWalkable(x, y))
					continue;

				foreach (GridPosition direction in directions)
				{
					DungeonCell neighbor = map.GetCell(
						x + direction.X,
						y + direction.Y
					);

					Assert.That(neighbor, Is.Not.Null);
					Assert.That(
						neighbor.Terrain.Kind,
						Is.Not.EqualTo(TerrainKind.Empty)
					);
				}
			}
		}
	}

	[Test]
	public void Generate_WithSameRequestProducesSameMapAndZones()
	{
		DungeonMap first = Generate(seed: 777);
		DungeonMap second = Generate(seed: 777);

		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				DungeonCell firstCell = first.GetCell(x, y);
				DungeonCell secondCell = second.GetCell(x, y);

				Assert.That(secondCell.Terrain.Kind, Is.EqualTo(firstCell.Terrain.Kind));
				Assert.That(secondCell.ZoneId, Is.EqualTo(firstCell.ZoneId));
				Assert.That(secondCell.ConnectedZoneA, Is.EqualTo(firstCell.ConnectedZoneA));
				Assert.That(secondCell.ConnectedZoneB, Is.EqualTo(firstCell.ConnectedZoneB));
			}
		}

		for (int index = 0; index < first.Zones.Count; index++)
		{
			DungeonZone firstZone = first.Zones[index];
			DungeonZone secondZone = second.Zones[index];

			Assert.That(secondZone.Type, Is.EqualTo(firstZone.Type));
			Assert.That(secondZone.TemplateName, Is.EqualTo(firstZone.TemplateName));
			Assert.That(secondZone.TemplateRotation, Is.EqualTo(firstZone.TemplateRotation));
			Assert.That(secondZone.TemplateMirrored, Is.EqualTo(firstZone.TemplateMirrored));
			Assert.That(secondZone.LayoutPosition, Is.EqualTo(firstZone.LayoutPosition));
			Assert.That(secondZone.ConnectedZoneIds, Is.EqualTo(firstZone.ConnectedZoneIds));
		}
	}

	[Test]
	public void DamageTerrain_BreakableWallBecomesFloor()
	{
		DungeonMap map = Generate(seed: 86420);
		GridPosition wall = FindCell(map, TerrainKind.BreakableWall);

		bool destroyed = map.DamageTerrain(wall.X, wall.Y, damage: 1);

		Assert.That(destroyed, Is.True);
		Assert.That(map.GetCell(wall.X, wall.Y).Terrain.Kind, Is.EqualTo(TerrainKind.Floor));
		Assert.That(map.IsWalkable(wall.X, wall.Y), Is.True);
	}

	[Test]
	public void DamageTerrain_SolidWallIsNotDestroyed()
	{
		DungeonMap map = Generate(seed: 11223);
		GridPosition wall = FindCell(map, TerrainKind.SolidWall);

		bool destroyed = map.DamageTerrain(wall.X, wall.Y, damage: 99);

		Assert.That(destroyed, Is.False);
		Assert.That(map.GetCell(wall.X, wall.Y).Terrain.Kind, Is.EqualTo(TerrainKind.SolidWall));
	}

	[Test]
	public void ZoneTemplate_RotatesAndMirrorsItsCompleteLayout()
	{
		ZoneTemplate template = new(
			"Asymmetric",
			"##D##",
			"#B..#",
			"D...D",
			"#...#",
			"##D##"
		);

		Assert.That(template.GetSymbol(3, 1, quarterTurns: 0, mirrored: false), Is.EqualTo('.'));
		Assert.That(template.GetSymbol(3, 1, quarterTurns: 1, mirrored: false), Is.EqualTo('B'));
		Assert.That(template.GetSymbol(1, 1, quarterTurns: 0, mirrored: true), Is.EqualTo('.'));
	}

	private static DungeonMap Generate(int seed)
	{
		return new DungeonGenerator().Generate(
			new DungeonGenerationRequest(Width, Height, ZoneCount, seed)
		);
	}

	private static int CountZones(DungeonMap map, DungeonZoneType type)
	{
		return map.Zones.Count(zone => zone.Type == type);
	}

	private static int CountDoorsConnecting(DungeonMap map, int firstId, int secondId)
	{
		int count = 0;

		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				DungeonCell cell = map.GetCell(x, y);
				bool connects = cell.ConnectedZoneA == firstId &&
						cell.ConnectedZoneB == secondId ||
					cell.ConnectedZoneA == secondId &&
						cell.ConnectedZoneB == firstId;

				if (cell.Terrain.Kind == TerrainKind.Door && connects)
					count++;
			}
		}

		return count;
	}

	private static GridPosition FindCell(DungeonMap map, TerrainKind terrainKind)
	{
		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				if (map.GetCell(x, y).Terrain.Kind == terrainKind)
					return new GridPosition(x, y);
			}
		}

		throw new AssertionException($"No {terrainKind} cell exists.");
	}

	private static int CountCells(DungeonMap map, TerrainKind terrainKind)
	{
		int count = 0;

		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				if (map.GetCell(x, y).Terrain.Kind == terrainKind)
					count++;
			}
		}

		return count;
	}

	private static int CountWalkableCells(DungeonMap map)
	{
		int count = 0;

		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				if (map.IsWalkable(x, y))
					count++;
			}
		}

		return count;
	}

	private static HashSet<GridPosition> FloodFill(DungeonMap map, GridPosition start)
	{
		HashSet<GridPosition> visited = new() { start };
		Queue<GridPosition> remaining = new();
		remaining.Enqueue(start);

		while (remaining.Count > 0)
		{
			GridPosition current = remaining.Dequeue();

			foreach (GridPosition direction in CardinalDirections())
			{
				GridPosition next = new(
					current.X + direction.X,
					current.Y + direction.Y
				);

				if (map.IsWalkable(next.X, next.Y) && visited.Add(next))
					remaining.Enqueue(next);
			}
		}

		return visited;
	}

	private static GridPosition[] CardinalDirections()
	{
		return new[]
		{
			new GridPosition(1, 0),
			new GridPosition(-1, 0),
			new GridPosition(0, 1),
			new GridPosition(0, -1)
		};
	}
}
