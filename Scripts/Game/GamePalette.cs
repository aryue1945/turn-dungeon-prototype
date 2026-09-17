using Godot;

// Semantic color constants drawn from the master palette
// (assets/palettes/lospec500.hex) - see docs/GAME_DESIGN.md's palette
// section. Pure data, no behavior. Values are grouped by how fixed their
// meaning is:
//
//   - Environment tones (Floor*/Wall*) are the ones a future dungeon theme
//     is expected to swap out. Kept muted so gameplay entities stand out.
//   - Gameplay-semantic tones (EliteAccent, Hazard*, Interactable, Reward,
//     Healing) are meant to stay fixed across every theme, so their meaning
//     never has to be relearned floor to floor.
//   - UI tones are chrome, not world content.
//
// This direction is now live in production art (Art/Actors, Art/Tiles) -
// the values here are baked into those PNGs at authoring time, not read at
// runtime, so nothing in Scripts/ references these constants directly.
// Kept as the canonical record of which lospec500 hex each role uses, so
// new art (a new enemy, a new hazard) pulls from the same values instead
// of guessing.
public static class GamePalette
{
	// Environment - theme-flexible. A simple three-tone material rule
	// (shadow/base/highlight) with one consistent light direction
	// (upper-left), hue-shifted rather than only lightened/darkened.
	public static readonly Color FloorBase = new("a26d3f");
	public static readonly Color FloorShadow = new("1e4044");
	public static readonly Color FloorHighlight = new("ce9248");
	public static readonly Color WallBase = new("4d3533");
	public static readonly Color WallHighlight = new("ce9248");
	public static readonly Color WallShadow = new("2c1e31");
	public static readonly Color BackgroundVoid = new("10121c");

	// Gameplay semantics - fixed across themes. Reserved for states that
	// carry real information (elite, hazard telegraphs, etc.), not applied
	// as a permanent identity treatment on every actor of a kind.
	public static readonly Color EliteAccent = new("ac2847");
	public static readonly Color HazardWarning = new("f3a833");
	public static readonly Color HazardActive = new("ec273f");
	public static readonly Color SpikeSafe = new("b0a7b8");
	public static readonly Color Interactable = new("cc99ff");
	public static readonly Color Reward = new("f7f3b7");
	public static readonly Color Healing = new("6dead6");

	// UI chrome.
	public static readonly Color UITextPrimary = new("ffffff");
	public static readonly Color UITextSecondary = new("b0a7b8");
}
