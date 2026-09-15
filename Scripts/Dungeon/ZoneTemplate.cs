using System;
using System.Collections.Generic;

public sealed class ZoneTemplate
{
	public const char FloorSymbol = '.';
	public const char BreakableWallSymbol = 'B';
	public const char TreeWallSymbol = 'T';
	public const char GrowingWallSymbol = 'G';
	public const char BoundarySymbol = '#';
	public const char DoorSocketSymbol = 'D';

	private readonly string[] _rows;

	public string Name { get; }
	public int Size => _rows.Length;

	public ZoneTemplate(string name, params string[] rows)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("A template requires a name.");

		if (rows == null || rows.Length < 3 || rows.Length % 2 == 0)
		{
			throw new ArgumentException(
				"A template requires an odd square of at least 3x3."
			);
		}

		for (int y = 0; y < rows.Length; y++)
		{
			string row = rows[y];

			if (row == null || row.Length != rows.Length)
				throw new ArgumentException("Zone templates must be square.");

			for (int x = 0; x < row.Length; x++)
			{
				char symbol = row[x];
				bool isBoundary = x == 0 || y == 0 ||
					x == rows.Length - 1 || y == rows.Length - 1;

				if (symbol != FloorSymbol &&
					symbol != BreakableWallSymbol &&
					symbol != TreeWallSymbol &&
					symbol != GrowingWallSymbol &&
					symbol != BoundarySymbol &&
					symbol != DoorSocketSymbol)
				{
					throw new ArgumentException(
						$"Unsupported template symbol: {symbol}."
					);
				}

				if (isBoundary &&
					symbol != BoundarySymbol &&
					symbol != DoorSocketSymbol)
				{
					throw new ArgumentException(
						"Template boundaries must contain walls or doors."
					);
				}

				if (!isBoundary &&
					symbol != FloorSymbol &&
					symbol != BreakableWallSymbol &&
					symbol != TreeWallSymbol &&
					symbol != GrowingWallSymbol)
				{
					throw new ArgumentException(
						"Template interiors must contain floor or walls."
					);
				}
			}
		}

		int center = rows.Length / 2;
		if (rows[0][center] != DoorSocketSymbol ||
			rows[rows.Length - 1][center] != DoorSocketSymbol ||
			rows[center][0] != DoorSocketSymbol ||
			rows[center][rows.Length - 1] != DoorSocketSymbol)
		{
			throw new ArgumentException(
				"Each side requires one centered door socket."
			);
		}

		Name = name;
		_rows = rows;
	}

	public char GetSymbol(int x, int y, int quarterTurns, bool mirrored)
	{
		if (x < 0 || x >= Size || y < 0 || y >= Size)
			throw new ArgumentOutOfRangeException();

		int normalizedTurns = ((quarterTurns % 4) + 4) % 4;
		GridPosition source = ReverseTransform(
			x,
			y,
			normalizedTurns,
			mirrored
		);
		return _rows[source.Y][source.X];
	}

	private GridPosition ReverseTransform(
		int transformedX,
		int transformedY,
		int quarterTurns,
		bool mirrored)
	{
		int x;
		int y;

		switch (quarterTurns)
		{
			case 0:
				x = transformedX;
				y = transformedY;
				break;
			case 1:
				x = transformedY;
				y = Size - 1 - transformedX;
				break;
			case 2:
				x = Size - 1 - transformedX;
				y = Size - 1 - transformedY;
				break;
			case 3:
				x = Size - 1 - transformedY;
				y = transformedX;
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(quarterTurns));
		}

		if (mirrored)
			x = Size - 1 - x;

		return new GridPosition(x, y);
	}
}

public static class ZoneTemplateCatalog
{
	public const int TemplateSize = 7;

	private static readonly ZoneTemplate Open = new(
		"Open",
		"###D###",
		"#.....#",
		"#.....#",
		"D.....D",
		"#.....#",
		"#.....#",
		"###D###"
	);

	private static readonly ZoneTemplate Pillars = new(
		"Pillars",
		"###D###",
		"#.B.B.#",
		"#.....#",
		"D.....D",
		"#.....#",
		"#.B.B.#",
		"###D###"
	);

	private static readonly ZoneTemplate Corner = new(
		"Corner",
		"###D###",
		"#BB...#",
		"#B....#",
		"D.....D",
		"#.....#",
		"#.....#",
		"###D###"
	);

	private static readonly ZoneTemplate Bars = new(
		"Bars",
		"###D###",
		"#.B.B.#",
		"#.B.B.#",
		"D.....D",
		"#.....#",
		"#.....#",
		"###D###"
	);

	private static readonly ZoneTemplate Grove = new(
		"Grove",
		"###D###",
		"#.T.T.#",
		"#.....#",
		"D..T..D",
		"#.....#",
		"#.T.T.#",
		"###D###"
	);

	private static readonly ZoneTemplate Overgrowth = new(
		"Overgrowth",
		"###D###",
		"#.....#",
		"#..G..#",
		"D.....D",
		"#.....#",
		"#.....#",
		"###D###"
	);

	private static readonly ZoneTemplate ShopCounter = new(
		"Shop Counter",
		"###D###",
		"#.....#",
		"#.BBB.#",
		"D.....D",
		"#.....#",
		"#.....#",
		"###D###"
	);

	private static readonly IReadOnlyList<ZoneTemplate> StartTemplates =
		new[] { Open };
	private static readonly IReadOnlyList<ZoneTemplate> CombatTemplates =
		new[] { Pillars, Corner, Bars, Grove, Overgrowth };
	private static readonly IReadOnlyList<ZoneTemplate> ShopTemplates =
		new[] { ShopCounter };
	private static readonly IReadOnlyList<ZoneTemplate> ExitTemplates =
		new[] { Open, Pillars };

	public static IReadOnlyList<ZoneTemplate> GetFor(
		DungeonZoneType zoneType)
	{
		return zoneType switch
		{
			DungeonZoneType.Start => StartTemplates,
			DungeonZoneType.Combat => CombatTemplates,
			DungeonZoneType.Shop => ShopTemplates,
			DungeonZoneType.Exit => ExitTemplates,
			_ => throw new ArgumentOutOfRangeException(nameof(zoneType))
		};
	}
}
