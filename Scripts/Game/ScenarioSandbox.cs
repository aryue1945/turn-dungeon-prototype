using Godot;
using System;

// Sandbox has two states (docs/DEBUG_SCENARIO_EDITOR.md): Edit (cursor,
// palette, place/delete - turns never advance) and Play (normal turn
// resolution, no editing).
public enum SandboxState
{
	Edit,
	Play
}

// Sandbox mode's state machine and cursor, kept separate from Main so Main
// does not grow by another few hundred lines as the palette/placement/
// save-load slices are added on top of this one. Holds no Godot nodes
// itself - Main owns and positions the cursor visual and calls back into
// this class for state transitions and cursor movement.
public sealed class ScenarioSandbox
{
	private readonly int _width;
	private readonly int _height;

	public SandboxState State { get; private set; } = SandboxState.Edit;
	public GridPosition CursorPosition { get; private set; }

	public ScenarioSandbox(int width, int height, GridPosition initialCursor)
	{
		if (width <= 0)
			throw new ArgumentOutOfRangeException(nameof(width));

		if (height <= 0)
			throw new ArgumentOutOfRangeException(nameof(height));

		_width = width;
		_height = height;
		CursorPosition = Clamp(initialCursor);
	}

	public void MoveCursor(int deltaX, int deltaY)
	{
		CursorPosition = Clamp(new GridPosition(CursorPosition.X + deltaX, CursorPosition.Y + deltaY));
	}

	public void SetCursor(GridPosition position)
	{
		CursorPosition = Clamp(position);
	}

	public void EnterEdit()
	{
		State = SandboxState.Edit;
	}

	public void EnterPlay()
	{
		State = SandboxState.Play;
	}

	private GridPosition Clamp(GridPosition position)
	{
		return new GridPosition(
			Math.Clamp(position.X, 0, _width - 1),
			Math.Clamp(position.Y, 0, _height - 1)
		);
	}

	// The inverse of Main.CellToPosition, for mouse picking. Floors rather
	// than truncates so a pixel position slightly left/above the origin
	// resolves to cell -1, not 0 - truncation would fold two different
	// cells' worth of negative-side pixels onto cell 0.
	public static GridPosition PixelToCell(Vector2 pixel, Vector2 origin, float tileSize)
	{
		Vector2 local = pixel - origin;

		return new GridPosition(
			(int)MathF.Floor(local.X / tileSize),
			(int)MathF.Floor(local.Y / tileSize)
		);
	}
}
