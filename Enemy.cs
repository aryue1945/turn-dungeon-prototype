using Godot;
using System.Collections.Generic;

public enum EnemyMovementType
{
	Chaser,
	SlowChaser,
	Patroller,
	Stationary
}

public partial class Enemy : CharacterBody2D
{
	private const float TileSize = 32.0f;

	private int _health = 2;
	private Polygon2D _facingIndicator;
	private Vector2 _facingDirection = Vector2.Down;
	private bool _slowChaserHasPreparedMove;

	public EnemyMovementType MovementType { get; private set; }

	public void Configure(EnemyMovementType movementType)
	{
		MovementType = movementType;

		if (MovementType == EnemyMovementType.Patroller)
			_facingDirection = Vector2.Right;
	}

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
			ZIndex = 1,
			Visible = MovementType != EnemyMovementType.Stationary &&
				MovementType != EnemyMovementType.SlowChaser
		};

		AddChild(_facingIndicator);
		SetFacingDirection(_facingDirection);
		ApplyTypeDisplay();
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
		if (MovementType != EnemyMovementType.Chaser &&
			MovementType != EnemyMovementType.SlowChaser)
		{
			return;
		}

		Vector2 difference = playerPosition - Position;

		if (difference.IsZeroApprox())
			return;

		if (Mathf.Abs(difference.X) > Mathf.Abs(difference.Y))
			_facingDirection = new Vector2(Mathf.Sign(difference.X), 0);
		else
			_facingDirection = new Vector2(0, Mathf.Sign(difference.Y));

		SetFacingDirection(_facingDirection);

		if (MovementType == EnemyMovementType.SlowChaser)
			_facingIndicator.Visible = true;
	}

	public void TakeTurn(
		Player player,
		HashSet<Vector2> occupiedEnemyPositions)
	{
		switch (MovementType)
		{
			case EnemyMovementType.Chaser:
				TryMoveForward(player, occupiedEnemyPositions);
				PrepareNextMove(player.Position);
				break;

			case EnemyMovementType.SlowChaser:
				TakeSlowChaserTurn(player, occupiedEnemyPositions);
				break;

			case EnemyMovementType.Patroller:
				TakePatrollerTurn(player, occupiedEnemyPositions);
				break;

			case EnemyMovementType.Stationary:
				break;
		}
	}

	private void TakeSlowChaserTurn(
		Player player,
		HashSet<Vector2> occupiedEnemyPositions)
	{
		if (!_slowChaserHasPreparedMove)
		{
			// Preparing is the entire action for this turn.
			PrepareNextMove(player.Position);
			_slowChaserHasPreparedMove = true;
			return;
		}

		// Moving uses the direction locked during the previous turn.
		TryMoveForward(player, occupiedEnemyPositions);
		_slowChaserHasPreparedMove = false;
		_facingIndicator.Visible = false;
	}

	private void TakePatrollerTurn(
		Player player,
		HashSet<Vector2> occupiedEnemyPositions)
	{
		bool completedAction =
			TryMoveForward(player, occupiedEnemyPositions);

		if (!completedAction)
		{
			_facingDirection = new Vector2(
				-_facingDirection.Y,
				_facingDirection.X
			);
			SetFacingDirection(_facingDirection);
		}
	}

	private bool TryMoveForward(
		Player player,
		HashSet<Vector2> occupiedEnemyPositions)
	{
		Vector2 nextPosition =
			Position + _facingDirection * TileSize;

		if (nextPosition.IsEqualApprox(player.Position))
		{
			GD.Print($"{Name} attacks player!");
			player.TakeDamage(1);
			return true;
		}

		if (IsWallAt(nextPosition) ||
			occupiedEnemyPositions.Contains(nextPosition))
		{
			return false;
		}

		Position = nextPosition;
		return true;
	}

	private void ApplyTypeDisplay()
	{
		Sprite2D sprite = GetNode<Sprite2D>("Sprite2D");
		string typeLabel;

		switch (MovementType)
		{
			case EnemyMovementType.Chaser:
				sprite.Modulate = new Color(1.0f, 0.15f, 0.1f);
				typeLabel = "CHASE";
				break;

			case EnemyMovementType.SlowChaser:
				sprite.Modulate = new Color(0.2f, 0.55f, 1.0f);
				typeLabel = "SLOW";
				break;

			case EnemyMovementType.Patroller:
				sprite.Modulate = new Color(1.0f, 0.55f, 0.1f);
				typeLabel = "PATROL";
				break;

			default:
				sprite.Modulate = new Color(0.65f, 0.35f, 0.9f);
				typeLabel = "STILL";
				break;
		}

		Label label = new()
		{
			Position = new Vector2(-28, 17),
			Size = new Vector2(56, 18),
			Text = typeLabel,
			HorizontalAlignment = Godot.HorizontalAlignment.Center,
			ZIndex = 2
		};
		label.AddThemeFontSizeOverride("font_size", 10);
		label.AddThemeColorOverride("font_color", Colors.White);
		AddChild(label);
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
