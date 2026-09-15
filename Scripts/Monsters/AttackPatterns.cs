using System.Collections.Generic;

// Named attack shapes a monster (or weapon, in principle) can reference by
// id instead of hand-writing AttackOffset arrays. This is what lets a mod's
// JSON say "pattern": "adjacent" and get the same offsets combat already
// understands.
public static class AttackPatterns
{
	private static readonly AttackOffset[] Adjacent =
	{
		new(1, 0)
	};

	private static readonly AttackOffset[] Line2 =
	{
		new(1, 0),
		new(2, 0)
	};

	private static readonly Dictionary<string, AttackOffset[]> Patterns = new()
	{
		["adjacent"] = Adjacent,
		["line2"] = Line2
	};

	public static IReadOnlyCollection<string> KnownIds => Patterns.Keys;

	public static bool IsKnown(string id) =>
		id != null && Patterns.ContainsKey(id);

	public static AttackOffset[] Get(string id) => Patterns[id];
}
