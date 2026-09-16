using Godot;
using System;
using System.Collections.Generic;

public partial class Enemy : CharacterBody2D, ICombatant, IEnemyMovementHost
{
	private const float TileSize = 32.0f;

	private ActorState _state;
	private Polygon2D _facingIndicator;
	private Label _healthLabel;
	private IEnemyMovementBehavior _movementBehavior;
	private Func<GridPosition, bool> _isWallAt;

	public MonsterDefinition Definition { get; private set; }
	public bool IsAlive =>
		_state.IsAlive && !IsQueuedForDeletion();
	public CombatFaction Faction => CombatFaction.Enemy;
	public AttackState Attack { get; private set; }

	// GameState holds this reference directly rather than copying fields, so
	// it always sees this enemy's current grid position/health/etc.
	public ActorState State => _state;
	public Guid InstanceId => _state.InstanceId;
	public string DefinitionId => _state.DefinitionId;
	public GridPosition GridPosition => _state.GridPosition;
	public int Health => _state.Health;
	Vector2 IEnemyMovementHost.FacingDirection => _state.Facing;
	bool IEnemyMovementHost.HasPreparedMove => _state.HasPreparedMove;

	// Must be called before this node enters the tree (Main configures the
	// enemy immediately after instantiating it, before AddChild triggers
	// _Ready). Everything the definition drives - health, sprite, movement
	// behavior, attack - is resolved here instead of being hard-coded, so a
	// modded MonsterDefinition works exactly like a built-in one. gridPosition
	// and pixelPosition set the authoritative ActorState and the synced
	// pixel Position together, the same way Player.PlaceAt does.
	public void Configure(
		MonsterDefinition definition,
		GridPosition gridPosition,
		Vector2 pixelPosition,
		Func<GridPosition, bool> isWallAt)
	{
		Definition = definition;
		_movementBehavior = EnemyMovementBehaviors.Create(definition.MovementBehaviorId);
		_state = new ActorState(gridPosition, definition.Health, definition.Id);
		Position = pixelPosition;
		Attack = new AttackState(definition.PrimaryAttack);
		_state.SetFacing(_movementBehavior.InitialFacingDirection);
		_state.SetAttack(Attack);
		_isWallAt = isWallAt;
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
			Visible = _movementBehavior.ShowsFacingIndicatorInitially
		};

		AddChild(_facingIndicator);
		UpdateFacingIndicatorRotation();
		ApplyTypeDisplay();
		CreateHealthDisplay();
	}

	public void TakeDamage(int damage)
	{
		_state.TakeDamage(damage);

		UpdateHealthDisplay();
		GD.Print($"{Name} health: {_state.Health}/{_state.MaxHealth}");

		if (_state.Health <= 0)
		{
			GD.Print($"{Name} defeated!");
			QueueFree();
		}
	}

	// Adopts a state rebuilt by GameSnapshotRestore.RestoreActor for
	// milestone-4 Continue, in place of the fresh full-health ActorState
	// Configure sets up. Called right after Configure/AddChild, reusing the
	// same Attack instance Configure already created from the definition
	// (an enemy's attack is fixed by its MonsterDefinition, so - unlike
	// Player - there is no separate Equip step).
	public void RestoreFrom(ActorSnapshot snapshot, Vector2 pixelPosition)
	{
		_state = GameSnapshotRestore.RestoreActor(snapshot, Attack);
		Position = pixelPosition;
		UpdateFacingIndicatorRotation();
		_facingIndicator.Visible =
			_movementBehavior.ShowsFacingIndicatorInitially || _state.HasPreparedMove;
		UpdateHealthDisplay();
	}

	public EnemyActionResult TakeTurn(
		Player player,
		HashSet<GridPosition> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		return _movementBehavior.TakeTurn(
			this,
			player,
			occupiedEnemyPositions,
			combatants
		);
	}

	void IEnemyMovementHost.SetFacingDirection(Vector2 direction)
	{
		_state.SetFacing(direction);
		UpdateFacingIndicatorRotation();
	}

	void IEnemyMovementHost.SetFacingIndicatorVisible(bool visible)
	{
		_facingIndicator.Visible = visible;
	}

	void IEnemyMovementHost.SetHasPreparedMove(bool hasPreparedMove)
	{
		_state.SetHasPreparedMove(hasPreparedMove);
	}

	void IEnemyMovementHost.TurnLeft() => TurnLeft();
	void IEnemyMovementHost.TurnRight() => TurnRight();

	EnemyActionResult IEnemyMovementHost.TryMoveForward(
		HashSet<GridPosition> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		return TryMoveForward(occupiedEnemyPositions, combatants);
	}

	private void TurnLeft()
	{
		Vector2 facing = _state.Facing;
		_state.SetFacing(new Vector2(facing.Y, -facing.X));
		UpdateFacingIndicatorRotation();
	}

	private void TurnRight()
	{
		Vector2 facing = _state.Facing;
		_state.SetFacing(new Vector2(-facing.Y, facing.X));
		UpdateFacingIndicatorRotation();
	}

	private EnemyActionResult TryMoveForward(
		HashSet<GridPosition> occupiedEnemyPositions,
		IReadOnlyList<ICombatant> combatants)
	{
		Vector2 facing = _state.Facing;

		AttackTurnResult attackResult =
			AttackResolver.TryAttack(
				this,
				facing,
				Attack,
				combatants,
				_isWallAt,
				out AttackExecutionDetail attackDetail
			);

		if (attackResult == AttackTurnResult.Attacked)
			return EnemyActionResult.Attacked(Attack.Definition.Name, attackDetail);

		if (attackResult == AttackTurnResult.Preparing)
			return EnemyActionResult.Preparing(Attack.Definition.Name);

		GridPosition nextGridPosition = new(
			_state.GridPosition.X + (int)facing.X,
			_state.GridPosition.Y + (int)facing.Y
		);

		if (_isWallAt(nextGridPosition) ||
			occupiedEnemyPositions.Contains(nextGridPosition))
		{
			return EnemyActionResult.Blocked;
		}

		Position += facing * TileSize;
		_state.MoveTo(nextGridPosition);
		return EnemyActionResult.Moved;
	}

	private void ApplyTypeDisplay()
	{
		Sprite2D sprite = GetNode<Sprite2D>("Sprite2D");
		sprite.Texture = MonsterSpriteLoader.Load(Definition.SpritePath);
		sprite.Modulate = Colors.White;

		Label label = new()
		{
			Position = new Vector2(-28, 17),
			Size = new Vector2(56, 18),
			Text = Definition.Name,
			HorizontalAlignment = Godot.HorizontalAlignment.Center,
			ZIndex = 2
		};
		label.AddThemeFontSizeOverride("font_size", 10);
		label.AddThemeColorOverride("font_color", Colors.White);
		AddChild(label);
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
		_healthLabel.Text = $"{_state.Health}/{_state.MaxHealth}";
	}

	private void UpdateFacingIndicatorRotation()
	{
		_facingIndicator.Rotation = _state.Facing.Angle();
	}
}
