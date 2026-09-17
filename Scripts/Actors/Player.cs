using Godot;
using System;

public partial class Player : CharacterBody2D, ICombatant, IPlayerTurnActor
{
	private const float TileSize = 32.0f;
	private const int StartingHealth = 3;
	private const string PlayerDefinitionId = "core.player";

	private Polygon2D _facingIndicator;
	private Label _healthLabel;

	// Authoritative grid position and health. Godot's own pixel Position
	// (inherited from CharacterBody2D) is kept in sync from this and used
	// only for rendering/camera - see PlaceAt, Move and RestoreFrom. Not
	// readonly: RestoreFrom (milestone-4 Continue) swaps in a state rebuilt
	// from a save instead of this fresh one.
	private ActorState _state =
		new(new GridPosition(0, 0), StartingHealth, PlayerDefinitionId);

	[Signal]
	public delegate void MoveRequestedEventHandler(Vector2 direction);

	[Signal]
	public delegate void WaitRequestedEventHandler();

	[Signal]
	public delegate void DiedEventHandler();

	// GameState holds this reference directly rather than copying fields, so
	// it always sees the player's current grid position/health/etc.
	public ActorState State => _state;
	public Guid InstanceId => _state.InstanceId;
	public string DefinitionId => _state.DefinitionId;
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
		CreateHealthDisplay();

		_state.SetAttack(Attack);
		_state.SetEquipment(Weapon.Id, DiggingTool.Id);
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent.IsActionPressed("wait"))
		{
			EmitSignal(SignalName.WaitRequested);
			GetViewport().SetInputAsHandled();
			return;
		}

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
		_state.SetEquipment(Weapon.Id, DiggingTool.Id);
	}

	public void EquipDiggingTool(DiggingToolDefinition diggingTool)
	{
		DiggingTool = diggingTool;
		_state.SetEquipment(Weapon.Id, DiggingTool.Id);
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

	// ICombatant.Knockback: identical mechanics to Move, kept as a separate
	// method since it is driven by AttackResolver rather than the player's
	// own input and carries no facing change.
	public void Knockback(Vector2 direction)
	{
		Position += direction * TileSize;
		_state.MoveBy((int)direction.X, (int)direction.Y);
	}

	// Adopts a state rebuilt by GameSnapshotRestore.RestoreActor for
	// milestone-4 Continue, in place of the fresh ActorState/weapon/tool
	// PlaceAt/EquipWeapon would otherwise set up. The caller must already
	// have called Attack.Equip(weapon.PrimaryAttack) before restoring state
	// through GameSnapshotRestore.RestoreActor, so state.Attack (this same
	// Attack instance) reflects the saved preparation, not a cancelled one.
	public void RestoreFrom(
		ActorState state,
		WeaponDefinition weapon,
		DiggingToolDefinition diggingTool,
		Vector2 pixelPosition)
	{
		_state = state;
		Weapon = weapon;
		DiggingTool = diggingTool;
		Position = pixelPosition;
		SetFacingDirection(state.Facing);
		UpdateHealthDisplay();
	}

	private void SetFacingDirection(Vector2 direction)
	{
		_state.SetFacing(direction);
		_facingIndicator.Rotation = direction.Angle();
	}

	// Public entry point for Sandbox placement (docs/DEBUG_SCENARIO_EDITOR.md),
	// which sets a facing without an accompanying move/input event.
	public void SetFacing(Vector2 direction)
	{
		SetFacingDirection(direction);
	}

	public void TakeDamage(int damage)
	{
		if (Health <= 0)
			return;

		_state.TakeDamage(damage);

		GD.Print($"Player health: {Health}");
		UpdateHealthDisplay();

		if (Health == 0)
		{
			GD.Print("Player defeated!");
			EmitSignal(SignalName.Died);
			SetProcessUnhandledInput(false);
		}
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
}
