using Godot;
using NUnit.Framework;

[TestFixture]
public sealed class ScenarioSandboxTests
{
	[Test]
	public void Constructor_StartsInEditState()
	{
		ScenarioSandbox sandbox = new(11, 7, new GridPosition(5, 3));

		Assert.That(sandbox.State, Is.EqualTo(SandboxState.Edit));
	}

	[Test]
	public void Constructor_ClampsAnOutOfBoundsInitialCursor()
	{
		ScenarioSandbox sandbox = new(11, 7, new GridPosition(50, -5));

		Assert.That(sandbox.CursorPosition, Is.EqualTo(new GridPosition(10, 0)));
	}

	[Test]
	public void MoveCursor_MovesByTheGivenDelta()
	{
		ScenarioSandbox sandbox = new(11, 7, new GridPosition(5, 3));

		sandbox.MoveCursor(1, 0);

		Assert.That(sandbox.CursorPosition, Is.EqualTo(new GridPosition(6, 3)));
	}

	[Test]
	public void MoveCursor_ClampsAtTheMapEdges()
	{
		ScenarioSandbox sandbox = new(11, 7, new GridPosition(0, 0));

		sandbox.MoveCursor(-5, -5);
		Assert.That(sandbox.CursorPosition, Is.EqualTo(new GridPosition(0, 0)));

		sandbox.MoveCursor(50, 50);
		Assert.That(sandbox.CursorPosition, Is.EqualTo(new GridPosition(10, 6)));
	}

	[Test]
	public void SetCursor_ClampsToMapBounds()
	{
		ScenarioSandbox sandbox = new(11, 7, new GridPosition(0, 0));

		sandbox.SetCursor(new GridPosition(-3, 100));

		Assert.That(sandbox.CursorPosition, Is.EqualTo(new GridPosition(0, 6)));
	}

	[Test]
	public void EnterEditAndEnterPlay_ToggleState()
	{
		ScenarioSandbox sandbox = new(11, 7, new GridPosition(0, 0));

		sandbox.EnterPlay();
		Assert.That(sandbox.State, Is.EqualTo(SandboxState.Play));

		sandbox.EnterEdit();
		Assert.That(sandbox.State, Is.EqualTo(SandboxState.Edit));
	}

	[TestCase(-16f, -16f, 0, 0)]
	[TestCase(0f, 0f, 0, 0)]
	[TestCase(15f, 15f, 0, 0)]
	[TestCase(16f, 0f, 1, 0)]
	[TestCase(0f, 16f, 0, 1)]
	[TestCase(-17f, 0f, -1, 0)]
	[TestCase(-48f, -48f, -1, -1)]
	[TestCase(-49f, 0f, -2, 0)]
	public void PixelToCell_UsesCenteredTileBoundsAroundTheOrigin(
		float pixelX,
		float pixelY,
		int expectedX,
		int expectedY)
	{
		GridPosition cell = ScenarioSandbox.PixelToCell(
			new Vector2(pixelX, pixelY),
			origin: Vector2.Zero,
			tileSize: 32f
		);

		Assert.That(cell, Is.EqualTo(new GridPosition(expectedX, expectedY)));
	}

	[Test]
	public void PixelToCell_AccountsForANonZeroOrigin()
	{
		GridPosition cell = ScenarioSandbox.PixelToCell(
			pixel: new Vector2(164f, 96f),
			origin: new Vector2(100f, 0f),
			tileSize: 32f
		);

		Assert.That(cell, Is.EqualTo(new GridPosition(2, 3)));
	}
}
