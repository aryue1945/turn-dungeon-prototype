using Godot;
using System.Collections.Generic;

public partial class Enemy : CharacterBody2D
{
	private const float TileSize = 32.0f;
	private int _health = 2;
	private Polygon2D _facingIndicator;

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

	public void TakeTurn(
		Player player,
		Vector2 chaseTargetPosition,
		HashSet<Vector2> occupiedEnemyPositions)
	{
		Vector2 difference = chaseTargetPosition - Position;

		if (difference.IsZeroApprox())
			return;

		Vector2 direction;

		if (Mathf.Abs(difference.X) > Mathf.Abs(difference.Y))
			direction = new Vector2(Mathf.Sign(difference.X), 0);
		else
			direction = new Vector2(0, Mathf.Sign(difference.Y));

		SetFacingDirection(direction);

		Vector2 nextPosition = Position + direction * TileSize;

		if (nextPosition.IsEqualApprox(player.Position))
		{
			GD.Print($"{Name} attacks player!");
			player.TakeDamage(1);
			return;
		}

		if (IsWallAt(nextPosition) ||
			occupiedEnemyPositions.Contains(nextPosition))
		{
			return;
		}

		Position = nextPosition;
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
