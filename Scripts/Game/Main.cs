using Godot;
using System.Collections.Generic;

public partial class Main : Node2D
{
	private const float TileSize = 32.0f;
	private const int RoomWidth = 15;
	private const int RoomHeight = 11;

	private static readonly Vector2 RoomOrigin = new(64, 64);
	private static readonly EnemyMovementType[] EnemyTypes =
	{
		EnemyMovementType.SlowChaser,
		EnemyMovementType.Patroller,
		EnemyMovementType.LeftTurner,
		EnemyMovementType.RightTurner,
		EnemyMovementType.Stationary
	};

	private readonly RandomNumberGenerator _random = new();
	private readonly List<Enemy> _enemies = new();

	private Player _player;
	private Label _healthLabel;
	private Label _weaponLabel;
	private Label _statusLabel;
	private Button _restartButton;
	private Control _endGameOverlay;
	private Control _weaponSelectionPanel;
	private bool _gameStarted;
	private bool _gameEnded;

	private PackedScene _enemyScene;
	private PackedScene _wallScene;

	public override void _Ready()
	{
		_player = GetNode<Player>("Player");

		_enemyScene = GD.Load<PackedScene>("res://enemy.tscn");
		_wallScene = GD.Load<PackedScene>("res://wall.tscn");

		_random.Randomize();

		CreateRoom();
		CreateRoomCamera();
		PlacePlayerInCenter();
		SpawnEnemies();
		CreateGameUi();
		CreateWeaponSelection();

		_player.MoveRequested += OnPlayerMoveRequested;
		_player.HealthChanged += OnPlayerHealthChanged;
		_player.Died += OnPlayerDied;

		GD.Print($"Player health: {_player.Health}");
		GD.Print($"Spawned {_enemies.Count} enemies.");
	}

	private Vector2 CellToPosition(int x, int y)
	{
		return RoomOrigin + new Vector2(x * TileSize, y * TileSize);
	}

	private void CreateRoomCamera()
	{
		Camera2D camera = new()
		{
			Position = RoomOrigin + new Vector2(
				(RoomWidth - 1) * TileSize / 2.0f,
				(RoomHeight - 1) * TileSize / 2.0f
			)
		};

		AddChild(camera);
		camera.MakeCurrent();
	}

	private void CreateRoom()
	{
		for (int x = 0; x < RoomWidth; x++)
		{
			CreateWall(x, 0);
			CreateWall(x, RoomHeight - 1);
		}

		for (int y = 1; y < RoomHeight - 1; y++)
		{
			CreateWall(0, y);
			CreateWall(RoomWidth - 1, y);
		}
	}

	private void CreateWall(int x, int y)
	{
		Node2D wall = _wallScene.Instantiate<Node2D>();
		wall.Position = CellToPosition(x, y);
		wall.AddToGroup("walls");
		AddChild(wall);
	}

	private void PlacePlayerInCenter()
	{
		int centerX = RoomWidth / 2;
		int centerY = RoomHeight / 2;
		_player.Position = CellToPosition(centerX, centerY);
	}

	private void SpawnEnemies()
	{
		HashSet<Vector2> occupiedPositions = new()
		{
			_player.Position
		};

		for (int i = 0; i < EnemyTypes.Length; i++)
		{
			Vector2 enemyPosition;

			do
			{
				int enemyX = _random.RandiRange(1, RoomWidth - 2);
				int enemyY = _random.RandiRange(1, RoomHeight - 2);
				enemyPosition = CellToPosition(enemyX, enemyY);
			}
			while (occupiedPositions.Contains(enemyPosition));

			EnemyMovementType movementType = EnemyTypes[i];
			Enemy enemy = _enemyScene.Instantiate<Enemy>();
			enemy.Name = $"{movementType}{i + 1}";
			enemy.Position = enemyPosition;
			enemy.Configure(movementType);

			occupiedPositions.Add(enemyPosition);
			_enemies.Add(enemy);
			AddChild(enemy);
		}
	}

	private static Control CreateFullRectRoot(CanvasLayer layer)
	{
		Control root = new();
		layer.AddChild(root);
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		return root;
	}

	private void CreateGameUi()
	{
		CanvasLayer canvasLayer = new();
		AddChild(canvasLayer);

		Control uiRoot = CreateFullRectRoot(canvasLayer);

		PanelContainer hudPanel = new();
		uiRoot.AddChild(hudPanel);
		hudPanel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		hudPanel.OffsetLeft = 12;
		hudPanel.OffsetTop = 12;
		hudPanel.OffsetRight = 224;
		hudPanel.OffsetBottom = 88;

		MarginContainer hudMargin = new();
		hudMargin.AddThemeConstantOverride("margin_left", 10);
		hudMargin.AddThemeConstantOverride("margin_top", 6);
		hudMargin.AddThemeConstantOverride("margin_right", 10);
		hudMargin.AddThemeConstantOverride("margin_bottom", 6);
		hudPanel.AddChild(hudMargin);

		VBoxContainer hud = new();
		hud.AddThemeConstantOverride("separation", 2);
		hudMargin.AddChild(hud);

		_healthLabel = new Label
		{
			Text = $"HP: {_player.Health}"
		};
		_healthLabel.AddThemeFontSizeOverride("font_size", 24);
		_healthLabel.AddThemeColorOverride("font_color", Colors.White);
		hud.AddChild(_healthLabel);

		_weaponLabel = new Label
		{
			Text = "Weapon: not selected"
		};
		_weaponLabel.AddThemeFontSizeOverride("font_size", 16);
		_weaponLabel.AddThemeColorOverride("font_color", Colors.White);
		hud.AddChild(_weaponLabel);

		CenterContainer endGameCenter = new();
		uiRoot.AddChild(endGameCenter);
		endGameCenter.SetAnchorsAndOffsetsPreset(
			Control.LayoutPreset.FullRect
		);

		PanelContainer endGamePanel = new()
		{
			CustomMinimumSize = new Vector2(260, 120),
			Visible = false
		};
		endGameCenter.AddChild(endGamePanel);
		_endGameOverlay = endGamePanel;

		MarginContainer endGameMargin = new();
		endGameMargin.AddThemeConstantOverride("margin_left", 20);
		endGameMargin.AddThemeConstantOverride("margin_top", 16);
		endGameMargin.AddThemeConstantOverride("margin_right", 20);
		endGameMargin.AddThemeConstantOverride("margin_bottom", 16);
		endGamePanel.AddChild(endGameMargin);

		VBoxContainer endGameBox = new();
		endGameBox.AddThemeConstantOverride("separation", 12);
		endGameMargin.AddChild(endGameBox);

		_statusLabel = new Label
		{
			CustomMinimumSize = new Vector2(220, 40),
			HorizontalAlignment = Godot.HorizontalAlignment.Center
		};
		_statusLabel.AddThemeFontSizeOverride("font_size", 28);
		endGameBox.AddChild(_statusLabel);

		_restartButton = new Button
		{
			CustomMinimumSize = new Vector2(160, 40),
			Text = "Restart"
		};
		_restartButton.Pressed += OnRestartPressed;
		endGameBox.AddChild(_restartButton);
	}

	private void CreateWeaponSelection()
	{
		CanvasLayer selectionLayer = new()
		{
			Layer = 10
		};
		AddChild(selectionLayer);

		Control selectionRoot = CreateFullRectRoot(selectionLayer);

		ColorRect backdrop = new()
		{
			Color = new Color(0, 0, 0, 0.55f)
		};
		selectionRoot.AddChild(backdrop);
		backdrop.SetAnchorsAndOffsetsPreset(
			Control.LayoutPreset.FullRect
		);

		CenterContainer selectionCenter = new();
		selectionRoot.AddChild(selectionCenter);
		selectionCenter.SetAnchorsAndOffsetsPreset(
			Control.LayoutPreset.FullRect
		);

		PanelContainer selectionPanel = new()
		{
			CustomMinimumSize = new Vector2(280, 220)
		};
		selectionCenter.AddChild(selectionPanel);
		_weaponSelectionPanel = selectionRoot;

		MarginContainer selectionMargin = new();
		selectionMargin.AddThemeConstantOverride("margin_left", 24);
		selectionMargin.AddThemeConstantOverride("margin_top", 20);
		selectionMargin.AddThemeConstantOverride("margin_right", 24);
		selectionMargin.AddThemeConstantOverride("margin_bottom", 20);
		selectionPanel.AddChild(selectionMargin);

		VBoxContainer selectionBox = new();
		selectionBox.AddThemeConstantOverride("separation", 12);
		selectionMargin.AddChild(selectionBox);

		Label titleLabel = new()
		{
			CustomMinimumSize = new Vector2(208, 36),
			Text = "Choose a weapon",
			HorizontalAlignment = Godot.HorizontalAlignment.Center
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 22);
		selectionBox.AddChild(titleLabel);

		Button basicSwordButton = new()
		{
			CustomMinimumSize = new Vector2(208, 44),
			Text = "Basic Sword"
		};
		basicSwordButton.Pressed += OnBasicSwordSelected;
		selectionBox.AddChild(basicSwordButton);

		Button longSwordButton = new()
		{
			CustomMinimumSize = new Vector2(208, 44),
			Text = "Long Sword"
		};
		longSwordButton.Pressed += OnLongSwordSelected;
		selectionBox.AddChild(longSwordButton);

		_player.SetProcessUnhandledInput(false);
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

	private bool IsEnemyActive(Enemy enemy)
	{
		return IsInstanceValid(enemy) &&
			!enemy.IsQueuedForDeletion();
	}

	private void RemoveDefeatedEnemies()
	{
		for (int i = _enemies.Count - 1; i >= 0; i--)
		{
			if (!IsEnemyActive(_enemies[i]))
				_enemies.RemoveAt(i);
		}
	}

	private HashSet<Vector2> GetOccupiedEnemyPositions(Enemy movingEnemy)
	{
		HashSet<Vector2> occupiedPositions = new();

		foreach (Enemy enemy in _enemies)
		{
			if (enemy != movingEnemy && IsEnemyActive(enemy))
				occupiedPositions.Add(enemy.Position);
		}

		return occupiedPositions;
	}

	private IReadOnlyList<ICombatant> GetCombatants()
	{
		List<ICombatant> combatants = new()
		{
			_player
		};

		foreach (Enemy enemy in _enemies)
		{
			if (IsEnemyActive(enemy))
				combatants.Add(enemy);
		}

		return combatants;
	}

	private void TakeEnemyTurns()
	{
		foreach (Enemy enemy in _enemies)
		{
			if (!IsEnemyActive(enemy))
				continue;

			HashSet<Vector2> occupiedPositions =
				GetOccupiedEnemyPositions(enemy);

			enemy.TakeTurn(
				_player,
				occupiedPositions,
				GetCombatants()
			);

			if (_player.Health <= 0)
				break;
		}
	}

	private void OnPlayerMoveRequested(Vector2 direction)
	{
		if (_gameEnded || !_gameStarted)
			return;

		Vector2 targetPosition =
			_player.Position + direction * TileSize;

		AttackTurnResult attackResult =
			AttackResolver.TryAttack(
				_player,
				direction,
				_player.Attack,
				GetCombatants(),
				IsWallAt
			);

		if (attackResult == AttackTurnResult.NoAttack)
		{
			if (!IsWallAt(targetPosition))
				_player.Move(direction);
			else
				GD.Print($"Player hit wall at {targetPosition}");
		}
		else
		{
			string actionText = attackResult == AttackTurnResult.Preparing
				? "prepares"
				: "used";

			GD.Print(
				$"Player {actionText} {_player.Attack.Definition.Name}."
			);
		}

		RemoveDefeatedEnemies();
		CheckForVictory();

		if (_gameEnded)
			return;

		TakeEnemyTurns();

		RemoveDefeatedEnemies();
		CheckForVictory();
	}

	private void CheckForVictory()
	{
		if (!_gameEnded && _enemies.Count == 0)
			EndGame(true);
	}

	private void EndGame(bool playerWon)
	{
		if (_gameEnded)
			return;

		_gameEnded = true;
		_player.SetProcessUnhandledInput(false);

		_statusLabel.Text = playerWon ? "YOU WIN!" : "GAME OVER";
		_statusLabel.AddThemeColorOverride(
			"font_color",
			playerWon ? Colors.LimeGreen : Colors.IndianRed
		);
		_endGameOverlay.Visible = true;

		GD.Print(playerWon ? "Room cleared!" : "Game over!");
	}

	private void OnPlayerHealthChanged(int health)
	{
		_healthLabel.Text = $"HP: {health}";
	}

	private void OnPlayerDied()
	{
		_healthLabel.Text = "HP: 0";
		EndGame(false);
	}

	private void OnBasicSwordSelected()
	{
		SelectWeapon(WeaponDefinitions.BasicSword);
	}

	private void OnLongSwordSelected()
	{
		SelectWeapon(WeaponDefinitions.LongSword);
	}

	private void SelectWeapon(WeaponDefinition weapon)
	{
		_player.EquipWeapon(weapon);
		_weaponLabel.Text = $"Weapon: {_player.Weapon.Name}";
		_weaponSelectionPanel.Visible = false;
		_gameStarted = true;
		_player.SetProcessUnhandledInput(true);

		GD.Print($"Equipped {_player.Weapon.Name}.");
	}

	private void OnRestartPressed()
	{
		GetTree().ReloadCurrentScene();
	}
}
