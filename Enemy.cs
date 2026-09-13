using Godot;
using System.Collections.Generic;

public partial class Enemy : CharacterBody2D
{
	private const float TileSize = 32.0f;
	private int _health = 2;
	private Polygon2D _facingIndicator;
	private Vector2 _facingDirection = Vector2.Down;

	public override void _Ready()
	{
		_facingIndicator = new Polygon2D
		{
			Polygon = new Vector2[]
			{
				new(16, 0),
				new(7, -5),
				new(7, 5)
			},
			Color = Colors.Yellow,
			ZIndex = 1
		};

		AddChild(_facingIndicator);
		SetFacingDirection(Vector2.Down);
	}

	public void TakeDamage(int damage)
	{
		_health -= damage;
		GD.Print($"{Name} health: {_health}");

		if (_health <= 0)
		{
			GD.Print($"{Name} defeated!");
			QueueFree();
		}
	}

	public void PrepareNextMove(Vector2 playerPosition)
	{
		Vector2 difference = playerPosition - Position;

		if (difference.IsZeroApprox())
			return;

		if (Mathf.Abs(difference.X) > Mathf.Abs(difference.Y))
			_facingDirection = new Vector2(Mathf.Sign(difference.X), 0);
		else
			_facingDirection = new Vector2(0, Mathf.Sign(difference.Y));

		SetFacingDirection(_facingDirection);
	}

	public void TakeTurn(
		Player player,
		HashSet<Vector2> occupiedEnemyPositions)
	{
		Vector2 nextPosition =
			Position + _facingDirection * TileSize;

		if (nextPosition.IsEqualApprox(player.Position))
		{
			GD.Print($"{Name} attacks player!");
			player.TakeDamage(1);
		}
		else if (!IsWallAt(nextPosition) &&
			!occupiedEnemyPositions.Contains(nextPosition))
		{
			Position = nextPosition;
		}

		// The completed move used the old facing direction.
		// Now telegraph the direction prepared for the next turn.
		PrepareNextMove(player.Position);
	}

	private void SetFacingDirection(Vector2 direction)
	{
		_facingIndicator.Rotation = direction.Angle();
	}

	private bool IsWallAt(Vector2 position)
	{
		foreach (Node node in GetTree().GetNodesInGroup("walls"))
		{
			if (node is Node2D wall &&
				wall.Position.IsEqualApprox(position))
			{
				return true;
			}
		}

		return false;
	}
}
