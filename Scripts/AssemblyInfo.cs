using System.Runtime.CompilerServices;

// Lets Tests exercise dormant terrain mechanics (see DungeonMap.AdvanceTurn)
// directly, without needing a live spawn path in generation.
[assembly: InternalsVisibleTo("TurnDungeon.Tests")]
