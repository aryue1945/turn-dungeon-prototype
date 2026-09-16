using Godot;

public partial class Player : CharacterBody2D, ICombatant
{
	private const float TileSize = 32.0f;
	private const int StartingHealth = 3;

	private Polygon2D _facingIndicator;

	// Authoritative grid position and health. Godot's own pixel Position
	// (inherited from CharacterBody2D) is kept in sync from this and used
	// only for rendering/camera - see PlaceAt and Move.
	private readonly ActorState _state =
		new(new GridPosition(0, 0), StartingHealth);

	[Signal]
	public delegate void MoveRequestedEventHandler(Vector2 direction);

	[Signal]
	public delegate void HealthChangedEventHandler(int health);

	[Signal]
	public delegate void DiedEventHandler();

	public GridPosition GridPosition => _state.GridPosition;
	public int Health => _state.Health;
	public Vector2 Facing => _state.Facing;
	public bool IsAlive => Health > 0;
	public CombatFaction Faction => CombatFaction.Player;
	public WeaponDefinition Weapon { get; private set; } =
		WeaponDefinitions.BasicSword;
	public DiggingToolDefinition DiggingTool { get; private set; } =
		DiggingToolDefinitions.BasicShovel;
	public AttackState Attack { get; } = new(
		WeaponDefinitions.BasicSword.PrimaryAttack
	);

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

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		Vector2 direction = Vector2.Zero;

		if (inputEvent.IsActionPressed("move_left"))
			direction = Vector2.Left;
		else if (inputEvent.IsActionPressed("move_right"))
			direction = Vector2.Right;
		else if (inputEvent.IsActionPressed("move_up"))
			direction = Vector2.Up;
		else if (inputEvent.IsActionPressed("move_down"))
			direction = Vector2.Down;

		if (direction != Vector2.Zero)
		{
			SetFacingDirection(direction);
			EmitSignal(SignalName.MoveRequested, direction);
			GetViewport().SetInputAsHandled();
		}
	}

	public void EquipWeapon(WeaponDefinition weapon)
	{
		Weapon = weapon;
		Attack.Equip(weapon.PrimaryAttack);
	}

	public void EquipDiggingTool(DiggingToolDefinition diggingTool)
	{
		DiggingTool = diggingTool;
	}

	// Called once by Main after generation to set the player's starting
	// cell, instead of Main writing the pixel Position directly.
	public void PlaceAt(GridPosition gridPosition, Vector2 pixelPosition)
	{
		_state.MoveTo(gridPosition);
		Position = pixelPosition;
	}

	public void Move(Vector2 direction)
	{
		Position += direction * TileSize;
		_state.MoveBy((int)direction.X, (int)direction.Y);
	}

	private void SetFacingDirection(Vector2 direction)
	{
		_state.SetFacing(direction);
		_facingIndicator.Rotation = direction.Angle();
	}

	public void TakeDamage(int damage)
	{
		if (Health <= 0)
			return;

		_state.TakeDamage(damage);

		GD.Print($"Player health: {Health}");
		EmitSignal(SignalName.HealthChanged, Health);

		if (Health == 0)
		{
			GD.Print("Player defeated!");
			EmitSignal(SignalName.Died);
			SetProcessUnhandledInput(false);
		}
	}
}
