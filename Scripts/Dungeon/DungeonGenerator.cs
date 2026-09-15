using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DungeonGenerator
{
	private static readonly GridPosition[] LayoutDirections =
	{
		new(1, 0),
		new(-1, 0),
		new(0, 1),
		new(0, -1)
	};

	public DungeonMap Generate(DungeonGenerationRequest request)
	{
		Validate(request);

		Random random = new(request.Seed);
		DungeonMap map = new(request.Width, request.Height, request.Seed);
		int templateSize = ZoneTemplateCatalog.TemplateSize;
		int templatePitch = templateSize - 1;
		int layoutColumns = (request.Width - 1) / templatePitch;
		int layoutRows = (request.Height - 1) / templatePitch;
		int layoutWidth = layoutColumns * templatePitch + 1;
		int layoutHeight = layoutRows * templatePitch + 1;
		GridPosition layoutOrigin = new(
			(request.Width - layoutWidth) / 2,
			(request.Height - layoutHeight) / 2
		);

		List<GridPosition> layoutPositions = PlaceConnectedZones(
			layoutColumns,
			layoutRows,
			request.TargetZoneCount,
			random
		);
		List<DungeonZone> zones = CreateZones(
			layoutPositions,
			layoutOrigin,
			templatePitch
		);

		ConnectAdjacentZones(zones);
		AssignZoneTypes(zones, random);
		RenderTemplatesIntoMap(map, zones, random);
		CreateSharedWallsAndDoors(map, zones, templateSize);

		map.SetRooms(zones.Select(zone => zone.Room).ToList().AsReadOnly());
		map.SetZones(zones.AsReadOnly());
		return map;
	}

	private static void Validate(DungeonGenerationRequest request)
	{
		if (request == null)
			throw new ArgumentNullException(nameof(request));

		int size = ZoneTemplateCatalog.TemplateSize;
		int pitch = size - 1;

		if (request.Width < size)
			throw new ArgumentOutOfRangeException(nameof(request.Width));

		if (request.Height < size)
			throw new ArgumentOutOfRangeException(nameof(request.Height));

		if (request.TargetZoneCount < 1)
			throw new ArgumentOutOfRangeException(
				nameof(request.TargetZoneCount)
			);

		int columns = (request.Width - 1) / pitch;
		int rows = (request.Height - 1) / pitch;

		if (request.TargetZoneCount > columns * rows)
		{
			throw new ArgumentOutOfRangeException(
				nameof(request.TargetZoneCount),
				"The requested map is too small for that many zones."
			);
		}
	}

	private static List<GridPosition> PlaceConnectedZones(
		int columns,
		int rows,
		int zoneCount,
		Random random)
	{
		GridPosition start = new(columns / 2, rows / 2);
		List<GridPosition> placed = new() { start };
		HashSet<GridPosition> occupied = new() { start };

		while (placed.Count < zoneCount)
		{
			HashSet<GridPosition> candidates = new();

			foreach (GridPosition position in placed)
			{
				foreach (GridPosition direction in LayoutDirections)
				{
					GridPosition candidate = new(
						position.X + direction.X,
						position.Y + direction.Y
					);

					if (candidate.X < 0 || candidate.X >= columns ||
						candidate.Y < 0 || candidate.Y >= rows ||
						occupied.Contains(candidate))
					{
						continue;
					}

					candidates.Add(candidate);
				}
			}

			if (candidates.Count == 0)
				throw new InvalidOperationException("Zone placement became stuck.");

			List<GridPosition> orderedCandidates = candidates
				.OrderBy(position => position.Y)
				.ThenBy(position => position.X)
				.ToList();
			GridPosition next = orderedCandidates[
				random.Next(orderedCandidates.Count)
			];
			placed.Add(next);
			occupied.Add(next);
		}

		return placed;
	}

	private static List<DungeonZone> CreateZones(
		IReadOnlyList<GridPosition> layoutPositions,
		GridPosition layoutOrigin,
		int templatePitch)
	{
		List<DungeonZone> zones = new();

		for (int zoneId = 0; zoneId < layoutPositions.Count; zoneId++)
		{
			GridPosition layoutPosition = layoutPositions[zoneId];
			GridPosition templateOrigin = new(
				layoutOrigin.X + layoutPosition.X * templatePitch,
				layoutOrigin.Y + layoutPosition.Y * templatePitch
			);
			DungeonRoom room = new(
				templateOrigin.X + 1,
				templateOrigin.Y + 1,
				ZoneTemplateCatalog.TemplateSize - 2,
				ZoneTemplateCatalog.TemplateSize - 2
			)
			{
				ZoneId = zoneId
			};

			zones.Add(
				new DungeonZone(
					zoneId,
					room,
					layoutPosition,
					templateOrigin
				)
			);
		}

		return zones;
	}

	private static void ConnectAdjacentZones(IReadOnlyList<DungeonZone> zones)
	{
		for (int first = 0; first < zones.Count; first++)
		{
			for (int second = first + 1; second < zones.Count; second++)
			{
				DungeonZone firstZone = zones[first];
				DungeonZone secondZone = zones[second];
				int distance = Math.Abs(
					firstZone.LayoutPosition.X - secondZone.LayoutPosition.X
				) + Math.Abs(
					firstZone.LayoutPosition.Y - secondZone.LayoutPosition.Y
				);

				if (distance != 1)
					continue;

				firstZone.ConnectTo(secondZone.Id);
				secondZone.ConnectTo(firstZone.Id);
			}
		}
	}

	private static void AssignZoneTypes(
		IReadOnlyList<DungeonZone> zones,
		Random random)
	{
		foreach (DungeonZone zone in zones)
			zone.Type = DungeonZoneType.Combat;

		DungeonZone start = zones[0];
		start.Type = DungeonZoneType.Start;

		if (zones.Count == 1)
			return;

		Dictionary<int, int> distances = GetZoneDistances(zones, start.Id);
		DungeonZone exit = zones
			.Where(zone => zone.Id != start.Id)
			.OrderByDescending(zone => distances[zone.Id])
			.ThenBy(zone => zone.Id)
			.First();
		exit.Type = DungeonZoneType.Exit;

		List<DungeonZone> shopCandidates = zones
			.Where(zone =>
				zone.Id != start.Id &&
				zone.Id != exit.Id)
			.ToList();

		if (shopCandidates.Count > 0)
		{
			DungeonZone shop = shopCandidates[random.Next(shopCandidates.Count)];
			shop.Type = DungeonZoneType.Shop;
		}
	}

	private static Dictionary<int, int> GetZoneDistances(
		IReadOnlyList<DungeonZone> zones,
		int startZoneId)
	{
		Dictionary<int, int> distances = new()
		{
			[startZoneId] = 0
		};
		Queue<int> remaining = new();
		remaining.Enqueue(startZoneId);

		while (remaining.Count > 0)
		{
			int currentId = remaining.Dequeue();

			foreach (int nextId in zones[currentId].ConnectedZoneIds)
			{
				if (distances.ContainsKey(nextId))
					continue;

				distances[nextId] = distances[currentId] + 1;
				remaining.Enqueue(nextId);
			}
		}

		return distances;
	}

	private static void RenderTemplatesIntoMap(
		DungeonMap map,
		IReadOnlyList<DungeonZone> zones,
		Random random)
	{
		foreach (DungeonZone zone in zones)
		{
			IReadOnlyList<ZoneTemplate> templates =
				ZoneTemplateCatalog.GetFor(zone.Type);
			ZoneTemplate template = templates[random.Next(templates.Count)];
			int rotation = random.Next(4);
			bool mirrored = random.Next(2) == 1;

			zone.TemplateName = template.Name;
			zone.TemplateRotation = rotation;
			zone.TemplateMirrored = mirrored;

			for (int templateY = 0; templateY < template.Size; templateY++)
			{
				for (int templateX = 0; templateX < template.Size; templateX++)
				{
					int mapX = zone.TemplateOrigin.X + templateX;
					int mapY = zone.TemplateOrigin.Y + templateY;
					bool isBoundary = templateX == 0 || templateY == 0 ||
						templateX == template.Size - 1 ||
						templateY == template.Size - 1;

					if (isBoundary)
					{
						map.SetTerrain(mapX, mapY, TerrainKind.SolidWall);
						continue;
					}

					char symbol = template.GetSymbol(
						templateX,
						templateY,
						rotation,
						mirrored
					);
					TerrainKind terrain = symbol switch
					{
						ZoneTemplate.BreakableWallSymbol =>
							TerrainKind.BreakableWall,
						ZoneTemplate.TreeWallSymbol =>
							TerrainKind.TreeWall,
						ZoneTemplate.GrowingWallSymbol =>
							TerrainKind.GrowingWall,
						_ => TerrainKind.Floor
					};

					map.SetTerrain(mapX, mapY, terrain);
					map.SetZone(mapX, mapY, zone.Id);
				}
			}
		}
	}

	private static void CreateSharedWallsAndDoors(
		DungeonMap map,
		IReadOnlyList<DungeonZone> zones,
		int templateSize)
	{
		for (int first = 0; first < zones.Count; first++)
		{
			DungeonZone firstZone = zones[first];

			foreach (int secondId in firstZone.ConnectedZoneIds)
			{
				if (secondId <= firstZone.Id)
					continue;

				DungeonZone secondZone = zones[secondId];
				CreateSharedWallAndDoor(
					map,
					firstZone,
					secondZone,
					templateSize
				);
			}
		}
	}

	private static void CreateSharedWallAndDoor(
		DungeonMap map,
		DungeonZone first,
		DungeonZone second,
		int templateSize)
	{
		int center = templateSize / 2;
		bool horizontalNeighbors =
			first.LayoutPosition.Y == second.LayoutPosition.Y;

		if (horizontalNeighbors)
		{
			DungeonZone left = first.LayoutPosition.X < second.LayoutPosition.X
				? first
				: second;
			DungeonZone right = ReferenceEquals(left, first) ? second : first;
			int wallX = left.TemplateOrigin.X + templateSize - 1;

			for (int offset = 1; offset < templateSize - 1; offset++)
			{
				map.SetTerrain(
					wallX,
					left.TemplateOrigin.Y + offset,
					TerrainKind.BreakableWall
				);
			}

			map.SetDoor(
				wallX,
				left.TemplateOrigin.Y + center,
				left.Id,
				right.Id
			);
			return;
		}

		DungeonZone top = first.LayoutPosition.Y < second.LayoutPosition.Y
			? first
			: second;
		DungeonZone bottom = ReferenceEquals(top, first) ? second : first;
		int wallY = top.TemplateOrigin.Y + templateSize - 1;

		for (int offset = 1; offset < templateSize - 1; offset++)
		{
			map.SetTerrain(
				top.TemplateOrigin.X + offset,
				wallY,
				TerrainKind.BreakableWall
			);
		}

		map.SetDoor(
			top.TemplateOrigin.X + center,
			wallY,
			top.Id,
			bottom.Id
		);
	}
}
