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

public partial class Enemy : CharacterBody2D, ICombatant
{
	private enum ForwardActionResult
	{
		Moved,
		AttackAction,
		Blocked
	}

	private const float TileSize = 32.0f;
	private const int MaxHealth = 2;

	private int _health = MaxHealth;
	private Polygon2D _facingIndicator;
	private Label _healthLabel;
	private Vector2 _facingDirection = Vector2.Down;
	private bool _slowChaserHasPreparedMove;

	public EnemyMovementType MovementType { get; private set; }
	public bool IsAlive =>
		_health > 0 && !IsQueuedForDeletion();
	public CombatFaction Faction => CombatFaction.Enemy;
	public AttackState Attack { get; } = new(
		AttackDefinitions.BasicEnemyStrike
	);

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
		CreateHealthDisplay();
	}

	public void TakeDamage(int damage)
	{
		_health -= damage;

		if (_health < 0)
			_health = 0;

		UpdateHealthDisplay();
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
		HashSet<Vector2> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		switch (MovementType)
		{
			case EnemyMovementType.SlowChaser:
				TakeSlowChaserTurn(
					player,
					occupiedEnemyPositions,
					combatants
				);
				break;

			case EnemyMovementType.Patroller:
				TakePatrollerTurn(
					occupiedEnemyPositions,
					combatants
				);
				break;

			case EnemyMovementType.LeftTurner:
				TakeTurningWalkerTurn(
					occupiedEnemyPositions,
					combatants,
					turnRight: false
				);
				break;

			case EnemyMovementType.RightTurner:
				TakeTurningWalkerTurn(
					occupiedEnemyPositions,
					combatants,
					turnRight: true
				);
				break;

			case EnemyMovementType.Stationary:
				break;
		}
	}

	private void TakeSlowChaserTurn(
		Player player,
		HashSet<Vector2> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		if (!_slowChaserHasPreparedMove)
		{
			// Preparing is the entire action for this turn.
			PrepareNextMove(player.Position);
			_slowChaserHasPreparedMove = true;
			return;
		}

		// Moving uses the direction locked during the previous turn.
		TryMoveForward(
			occupiedEnemyPositions,
			combatants
		);
		_slowChaserHasPreparedMove = false;
		_facingIndicator.Visible = false;
	}

	private void TakePatrollerTurn(
		HashSet<Vector2> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		ForwardActionResult result =
			TryMoveForward(
				occupiedEnemyPositions,
				combatants
			);

		if (result == ForwardActionResult.Blocked)
			TurnRight();
	}

	private void TakeTurningWalkerTurn(
		HashSet<Vector2> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants,
		bool turnRight)
	{
		ForwardActionResult result =
			TryMoveForward(
				occupiedEnemyPositions,
				combatants
			);

		// An attack consumes the entire beat.
		if (result == ForwardActionResult.AttackAction)
			return;

		// Otherwise turning is the second action, even if movement was blocked.
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

	private ForwardActionResult TryMoveForward(
		HashSet<Vector2> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		AttackTurnResult attackResult =
			AttackResolver.TryAttack(
				this,
				_facingDirection,
				Attack,
				combatants,
				IsWallAt
			);

		if (attackResult != AttackTurnResult.NoAttack)
		{
			string actionText = attackResult == AttackTurnResult.Preparing
				? "prepares"
				: "used";

			GD.Print(
				$"{Name} {actionText} {Attack.Definition.Name}."
			);
			return ForwardActionResult.AttackAction;
		}

		Vector2 nextPosition =
			Position + _facingDirection * TileSize;

		if (IsWallAt(nextPosition) ||
			occupiedEnemyPositions.Contains(nextPosition))
		{
			return ForwardActionResult.Blocked;
		}

		Position = nextPosition;
		return ForwardActionResult.Moved;
	}

	private void ApplyTypeDisplay()
	{
		Sprite2D sprite = GetNode<Sprite2D>("Sprite2D");
		sprite.Texture = GD.Load<Texture2D>(GetTypeTexturePath());
		sprite.Modulate = Colors.White;

		string typeLabel = MovementType switch
		{
			EnemyMovementType.SlowChaser => "SLOW",
			EnemyMovementType.Patroller => "PATROL",
			EnemyMovementType.LeftTurner => "LEFT",
			EnemyMovementType.RightTurner => "RIGHT",
			_ => "STILL"
		};

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

	private string GetTypeTexturePath()
	{
		return MovementType switch
		{
			EnemyMovementType.SlowChaser =>
				"res://Art/Actors/enemy_slow_chaser.png",
			EnemyMovementType.Patroller =>
				"res://Art/Actors/enemy_patroller.png",
			EnemyMovementType.LeftTurner =>
				"res://Art/Actors/enemy_left_turner.png",
			EnemyMovementType.RightTurner =>
				"res://Art/Actors/enemy_right_turner.png",
			_ => "res://Art/Actors/enemy_stationary.png"
		};
	}

	private void CreateHealthDisplay()
	{
		_healthLabel = new Label
		{
			Position = new Vector2(-20, -46),
			Size = new Vector2(40, 16),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			ZIndex = 4
		};

		_healthLabel.AddThemeFontSizeOverride("font_size", 10);
		_healthLabel.AddThemeColorOverride("font_color", Colors.White);
		_healthLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_healthLabel.AddThemeConstantOverride("outline_size", 2);

		AddChild(_healthLabel);
		UpdateHealthDisplay();
	}

	private void UpdateHealthDisplay()
	{
		_healthLabel.Text = $"{_health}/{MaxHealth}";
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
