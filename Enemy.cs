using Godot;
using System.Collections.Generic;

public enum EnemyMovementType
{
	SlowChaser,
	Patroller,
	LeftTurner,
	RightTurner,
	Stationary
}

public partial class Enemy : CharacterBody2D
{
	private const float TileSize = 32.0f;
	private const int MaxHealth = 2;

	private int _health = MaxHealth;
	private Polygon2D _facingIndicator;
	private ProgressBar _healthBar;
	private Vector2 _facingDirection = Vector2.Down;
	private bool _slowChaserHasPreparedMove;

	public EnemyMovementType MovementType { get; private set; }
	public int AttackRange { get; private set; } = 1;

	public void Configure(EnemyMovementType movementType)
	{
		MovementType = movementType;

		AttackRange = movementType switch
		{
			EnemyMovementType.SlowChaser => 1,
			EnemyMovementType.Patroller => 1,
			EnemyMovementType.LeftTurner => 1,
			EnemyMovementType.RightTurner => 1,
			EnemyMovementType.Stationary => 1,
			_ => 1
		};

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
		CreateHealthBar();
	}

	public void TakeDamage(int damage)
	{
		_health -= damage;

		if (_health < 0)
			_health = 0;

		_healthBar.Value = _health;
		GD.Print($"{Name} health: {_health}/{MaxHealth}");

		if (_health <= 0)
		{
			GD.Print($"{Name} defeated!");
			QueueFree();
		}
	}

	public void PrepareNextMove(Vector2 playerPosition)
	{
		if (MovementType != EnemyMovementType.SlowChaser)
			return;

		Vector2 difference = playerPosition - Position;

		if (difference.IsZeroApprox())
			return;

		if (Mathf.Abs(difference.X) > Mathf.Abs(difference.Y))
			_facingDirection = new Vector2(Mathf.Sign(difference.X), 0);
		else
			_facingDirection = new Vector2(0, Mathf.Sign(difference.Y));

		SetFacingDirection(_facingDirection);

		_facingIndicator.Visible = true;
	}

	public void TakeTurn(
		Player player,
		HashSet<Vector2> occupiedEnemyPositions)
	{
		if (CanAttack(player.Position))
		{
			Attack(player);
			return;
		}

		switch (MovementType)
		{
			case EnemyMovementType.SlowChaser:
				TakeSlowChaserTurn(player, occupiedEnemyPositions);
				break;

			case EnemyMovementType.Patroller:
				TakePatrollerTurn(player, occupiedEnemyPositions);
				break;

			case EnemyMovementType.LeftTurner:
				TakeTurningWalkerTurn(
					player,
					occupiedEnemyPositions,
					turnRight: false
				);
				break;

			case EnemyMovementType.RightTurner:
				TakeTurningWalkerTurn(
					player,
					occupiedEnemyPositions,
					turnRight: true
				);
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
		TryMoveForward(occupiedEnemyPositions);
		_slowChaserHasPreparedMove = false;
		_facingIndicator.Visible = false;
	}

	private void TakePatrollerTurn(
		Player player,
		HashSet<Vector2> occupiedEnemyPositions)
	{
		bool completedAction =
			TryMoveForward(occupiedEnemyPositions);

		if (!completedAction)
			TurnRight();
	}

	private void TakeTurningWalkerTurn(
		Player player,
		HashSet<Vector2> occupiedEnemyPositions,
		bool turnRight)
	{
		// Moving or attacking is the first action in the beat.
		TryMoveForward(occupiedEnemyPositions);

		// Turning always happens as the second action, even if blocked.
		if (turnRight)
			TurnRight();
		else
			TurnLeft();
	}

	private void TurnLeft()
	{
		_facingDirection = new Vector2(
			_facingDirection.Y,
			-_facingDirection.X
		);
		SetFacingDirection(_facingDirection);
	}

	private void TurnRight()
	{
		_facingDirection = new Vector2(
			-_facingDirection.Y,
			_facingDirection.X
		);
		SetFacingDirection(_facingDirection);
	}

	private bool CanAttack(Vector2 targetPosition)
	{
		Vector2 difference = targetPosition - Position;
		int horizontalTiles =
			Mathf.RoundToInt(Mathf.Abs(difference.X) / TileSize);
		int verticalTiles =
			Mathf.RoundToInt(Mathf.Abs(difference.Y) / TileSize);

		bool isInStraightLine =
			horizontalTiles == 0 || verticalTiles == 0;
		int distanceInTiles = horizontalTiles + verticalTiles;

		return isInStraightLine &&
			distanceInTiles >= 1 &&
			distanceInTiles <= AttackRange;
	}

	private void Attack(Player player)
	{
		GD.Print($"{Name} attacks player from range {AttackRange}!");
		player.TakeDamage(1);
	}

	private bool TryMoveForward(
		HashSet<Vector2> occupiedEnemyPositions)
	{
		Vector2 nextPosition =
			Position + _facingDirection * TileSize;

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
			case EnemyMovementType.SlowChaser:
				sprite.Modulate = new Color(0.2f, 0.55f, 1.0f);
				typeLabel = "SLOW";
				break;

			case EnemyMovementType.Patroller:
				sprite.Modulate = new Color(1.0f, 0.55f, 0.1f);
				typeLabel = "PATROL";
				break;

			case EnemyMovementType.LeftTurner:
				sprite.Modulate = new Color(0.2f, 0.85f, 0.35f);
				typeLabel = "LEFT";
				break;

			case EnemyMovementType.RightTurner:
				sprite.Modulate = new Color(1.0f, 0.3f, 0.65f);
				typeLabel = "RIGHT";
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

	private void CreateHealthBar()
	{
		_healthBar = new ProgressBar
		{
			Position = new Vector2(-16, -26),
			Size = new Vector2(32, 7),
			MinValue = 0,
			MaxValue = MaxHealth,
			Value = _health,
			ShowPercentage = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ZIndex = 3
		};

		StyleBoxFlat backgroundStyle = new()
		{
			BgColor = new Color(0.12f, 0.12f, 0.12f)
		};

		StyleBoxFlat fillStyle = new()
		{
			BgColor = new Color(0.2f, 0.9f, 0.25f)
		};

		_healthBar.AddThemeStyleboxOverride("background", backgroundStyle);
		_healthBar.AddThemeStyleboxOverride("fill", fillStyle);
		AddChild(_healthBar);
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
