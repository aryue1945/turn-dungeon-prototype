using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main : Node2D
{
	private const float TileSize = 32.0f;
	private const int MapWidth = 24;
	private const int MapHeight = 16;
	private const int TargetZoneCount = 5;
	private const float MinimumCameraZoom = 0.5f;
	private const float MaximumCameraZoom = 2.0f;
	private const float CameraZoomStep = 0.25f;

	private static readonly Vector2 MapOrigin = Vector2.Zero;

	private readonly RandomNumberGenerator _random = new();
	private readonly List<Enemy> _enemies = new();
	private List<MonsterDefinition> _spawnPool = new();

	private Player _player;
	private Label _healthLabel;
	private Label _weaponLabel;
	private Label _toolLabel;
	private Label _statusLabel;
	private Button _restartButton;
	private Control _endGameOverlay;
	private Control _weaponSelectionPanel;
	private bool _gameStarted;
	private bool _gameEnded;

	private PackedScene _enemyScene;
	private PackedScene _wallScene;
	private Texture2D _floorTexture;
	private Texture2D _floorCrackedTexture;
	private Texture2D _wallHorizontalTexture;
	private Texture2D _wallVerticalTexture;
	private Texture2D _wallCornerLeftTexture;
	private Texture2D _wallCornerRightTexture;
	private Texture2D _wallBarsTexture;
	private Texture2D _breakableWallTexture;
	private Texture2D _doorTexture;
	private DungeonMap _dungeonMap;
	private DungeonRenderer _dungeonRenderer;
	private int _dungeonSeed;
	private int _currentPlayerZoneId = -1;
	private Camera2D _camera;

	public override void _Ready()
	{
		_player = GetNode<Player>("Player");

		_enemyScene = GD.Load<PackedScene>("res://enemy.tscn");
		_wallScene = GD.Load<PackedScene>("res://wall.tscn");
		_floorTexture = GD.Load<Texture2D>(
			"res://Art/Tiles/prison_floor.png"
		);
		_floorCrackedTexture = GD.Load<Texture2D>(
			"res://Art/Tiles/prison_floor_cracked.png"
		);
		_wallHorizontalTexture = GD.Load<Texture2D>(
			"res://Art/Tiles/prison_wall.png"
		);
		_wallVerticalTexture = GD.Load<Texture2D>(
			"res://Art/Tiles/prison_wall_vertical.png"
		);
		_wallCornerLeftTexture = GD.Load<Texture2D>(
			"res://Art/Tiles/prison_wall_corner_left.png"
		);
		_wallCornerRightTexture = GD.Load<Texture2D>(
			"res://Art/Tiles/prison_wall_corner_right.png"
		);
		_wallBarsTexture = GD.Load<Texture2D>(
			"res://Art/Tiles/prison_wall_bars.png"
		);
		_breakableWallTexture = GD.Load<Texture2D>(
			"res://Art/Tiles/prison_breakable_wall.svg"
		);
		_doorTexture = GD.Load<Texture2D>(
			"res://Art/Tiles/prison_cell_door.png"
		);

		_random.Randomize();
		_spawnPool = BuildSpawnPool();

		CreateDungeon();
		PlacePlayerInStartRoom();
		CreateFollowingCamera();
		SpawnEnemies();
		CreateGameUi();
		CreateWeaponSelection();

		_player.MoveRequested += OnPlayerMoveRequested;
		_player.HealthChanged += OnPlayerHealthChanged;
		_player.Died += OnPlayerDied;

		GD.Print($"Player health: {_player.Health}");
		GD.Print($"Spawned {_enemies.Count} enemies.");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_gameStarted &&
			@event is InputEventKey weaponKey &&
			weaponKey.Pressed &&
			!weaponKey.Echo)
		{
			if (weaponKey.Keycode == Key.Key1)
				OnBasicSwordSelected();
			else if (weaponKey.Keycode == Key.Key2)
				OnLongSwordSelected();
			else
				return;

			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.Pressed)
		{
			if (mouseButton.ButtonIndex == MouseButton.WheelDown)
				AdjustCameraZoom(-CameraZoomStep);
			else if (mouseButton.ButtonIndex == MouseButton.WheelUp)
				AdjustCameraZoom(CameraZoomStep);
			else
				return;

			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is not InputEventKey keyEvent ||
			!keyEvent.Pressed ||
			keyEvent.Echo)
		{
			return;
		}

		switch (keyEvent.Keycode)
		{
			case Key.Minus:
			case Key.KpSubtract:
				AdjustCameraZoom(-CameraZoomStep);
				break;
			case Key.Equal:
			case Key.KpAdd:
				AdjustCameraZoom(CameraZoomStep);
				break;
			case Key.Key0:
				SetCameraZoom(1.0f);
				break;
			default:
				return;
		}

		GetViewport().SetInputAsHandled();
	}

	private Vector2 CellToPosition(GridPosition cell)
	{
		return MapOrigin + new Vector2(
			cell.X * TileSize,
			cell.Y * TileSize
		);
	}

	private GridPosition PositionToCell(Vector2 position)
	{
		Vector2 localPosition = (position - MapOrigin) / TileSize;
		return new GridPosition(
			Mathf.RoundToInt(localPosition.X),
			Mathf.RoundToInt(localPosition.Y)
		);
	}

	private void CreateDungeon()
	{
		_dungeonSeed = unchecked((int)_random.Randi());
		DungeonGenerationRequest request = new(
			MapWidth,
			MapHeight,
			TargetZoneCount,
			seed: _dungeonSeed
		);
		_dungeonMap = new DungeonGenerator().Generate(request);
		GD.Print(_dungeonMap.ToDebugString());

		_dungeonRenderer = new(
			this,
			_wallScene,
			_floorTexture,
			_floorCrackedTexture,
			_wallHorizontalTexture,
			_wallVerticalTexture,
			_wallCornerLeftTexture,
			_wallCornerRightTexture,
			_wallBarsTexture,
			_breakableWallTexture,
			_doorTexture,
			MapOrigin,
			TileSize
		);
		_dungeonRenderer.Render(_dungeonMap);

		GD.Print(
			$"Dungeon seed: {_dungeonSeed}; " +
			$"zones: {_dungeonMap.Zones.Count}."
		);
	}

	private void PlacePlayerInStartRoom()
	{
		DungeonZone startZone = _dungeonMap.Zones.Single(
			zone => zone.Type == DungeonZoneType.Start
		);
		GridPosition startCell = startZone.Room.Center;
		_player.Position = CellToPosition(startCell);
		UpdatePlayerZone();
	}

	private void UpdatePlayerZone()
	{
		GridPosition cell = PositionToCell(_player.Position);
		int zoneId = _dungeonMap.GetZoneId(cell.X, cell.Y);

		// A door belongs to both neighboring zones, so retain the
		// current zone until the player steps onto the next room floor.
		if (zoneId < 0 || zoneId == _currentPlayerZoneId)
			return;

		if (_currentPlayerZoneId >= 0)
			OnPlayerExitedZone(_currentPlayerZoneId);

		_currentPlayerZoneId = zoneId;
		OnPlayerEnteredZone(zoneId);
	}

	private void OnPlayerEnteredZone(int zoneId)
	{
		DungeonZone zone = _dungeonMap.GetZone(zoneId);
		GD.Print(
			$"Player entered zone {zoneId}: {zone.Type}, " +
			$"template {zone.TemplateName}."
		);
	}

	private static void OnPlayerExitedZone(int zoneId)
	{
		GD.Print($"Player exited zone {zoneId}.");
	}

	private void CreateFollowingCamera()
	{
		_camera = new Camera2D
		{
			Position = Vector2.Zero,
			PositionSmoothingEnabled = true,
			PositionSmoothingSpeed = 8.0f,
			LimitLeft = Mathf.RoundToInt(MapOrigin.X - TileSize / 2),
			LimitTop = Mathf.RoundToInt(MapOrigin.Y - TileSize / 2),
			LimitRight = Mathf.RoundToInt(
				MapOrigin.X + (MapWidth - 1) * TileSize + TileSize / 2
			),
			LimitBottom = Mathf.RoundToInt(
				MapOrigin.Y + (MapHeight - 1) * TileSize + TileSize / 2
			)
		};

		_player.AddChild(_camera);
		_camera.MakeCurrent();
	}

	private void AdjustCameraZoom(float amount)
	{
		SetCameraZoom(_camera.Zoom.X + amount);
	}

	private void SetCameraZoom(float zoom)
	{
		float clampedZoom = Mathf.Clamp(
			zoom,
			MinimumCameraZoom,
			MaximumCameraZoom
		);
		_camera.Zoom = Vector2.One * clampedZoom;
		GD.Print($"Camera zoom: {clampedZoom:0.00}x");
	}

	// Built-in monsters plus any monster mods found on disk. Kept as data
	// (MonsterDefinition) so SpawnEnemies doesn't need to know which
	// monsters are built-in versus modded.
	private List<MonsterDefinition> BuildSpawnPool()
	{
		List<MonsterDefinition> pool = new(MonsterDefinitions.All);

		string modsPath = ProjectSettings.GlobalizePath("res://mods");
		MonsterModLoadResult modResult = MonsterModLoader.LoadFromDirectory(modsPath);

		foreach (string error in modResult.Errors)
			GD.PushError(error);

		if (modResult.Monsters.Count > 0)
		{
			GD.Print($"Loaded {modResult.Monsters.Count} modded monster(s) from {modsPath}.");
			pool.AddRange(modResult.Monsters);
		}

		return pool;
	}

	private void SpawnEnemies()
	{
		HashSet<Vector2> occupiedPositions = new()
		{
			_player.Position
		};

		List<DungeonZone> combatZones = _dungeonMap.Zones
			.Where(zone => zone.Type == DungeonZoneType.Combat)
			.ToList();

		if (combatZones.Count == 0 || _spawnPool.Count == 0)
			return;

		for (int i = 0; i < _spawnPool.Count; i++)
		{
			DungeonRoom room = combatZones[i % combatZones.Count].Room;
			GridPosition enemyCell = GetRandomSpawnCell(
				room,
				occupiedPositions
			);
			Vector2 enemyPosition = CellToPosition(enemyCell);

			MonsterDefinition definition = _spawnPool[i];
			Enemy enemy = _enemyScene.Instantiate<Enemy>();
			enemy.Name = $"{definition.Id.Replace('.', '_')}{i + 1}";
			enemy.Position = enemyPosition;
			enemy.Configure(definition, IsWallAt);

			occupiedPositions.Add(enemyPosition);
			_enemies.Add(enemy);
			AddChild(enemy);
		}
	}

	private GridPosition GetRandomSpawnCell(
		DungeonRoom room,
		HashSet<Vector2> occupiedPositions)
	{
		for (int attempt = 0; attempt < 100; attempt++)
		{
			GridPosition cell = new(
				_random.RandiRange(room.X, room.Right - 1),
				_random.RandiRange(room.Y, room.Bottom - 1)
			);
			Vector2 position = CellToPosition(cell);

			if (_dungeonMap.IsWalkable(cell.X, cell.Y) &&
				!occupiedPositions.Contains(position))
			{
				return cell;
			}
		}

		for (int y = room.Y; y < room.Bottom; y++)
		{
			for (int x = room.X; x < room.Right; x++)
			{
				GridPosition cell = new(x, y);
				Vector2 position = CellToPosition(cell);

				if (_dungeonMap.IsWalkable(x, y) &&
					!occupiedPositions.Contains(position))
				{
					return cell;
				}
			}
		}

		throw new InvalidOperationException(
			"No free spawn cell exists in the selected room."
		);
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
		hudPanel.OffsetBottom = 112;

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

		_toolLabel = new Label
		{
			Text = $"Tool: {_player.DiggingTool.Name}"
		};
		_toolLabel.AddThemeFontSizeOverride("font_size", 16);
		_toolLabel.AddThemeColorOverride("font_color", Colors.White);
		hud.AddChild(_toolLabel);

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
			CustomMinimumSize = new Vector2(280, 250)
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
			Text = "[1] Basic Sword"
		};
		basicSwordButton.Pressed += OnBasicSwordSelected;
		selectionBox.AddChild(basicSwordButton);

		Button longSwordButton = new()
		{
			CustomMinimumSize = new Vector2(208, 44),
			Text = "[2] Long Sword"
		};
		longSwordButton.Pressed += OnLongSwordSelected;
		selectionBox.AddChild(longSwordButton);

		Label keyboardHint = new()
		{
			Text = "Up/Down, then Enter or Space",
			HorizontalAlignment = Godot.HorizontalAlignment.Center
		};
		keyboardHint.AddThemeFontSizeOverride("font_size", 14);
		selectionBox.AddChild(keyboardHint);

		basicSwordButton.FocusNeighborTop =
			basicSwordButton.GetPathTo(longSwordButton);
		basicSwordButton.FocusNeighborBottom =
			basicSwordButton.GetPathTo(longSwordButton);
		longSwordButton.FocusNeighborTop =
			longSwordButton.GetPathTo(basicSwordButton);
		longSwordButton.FocusNeighborBottom =
			longSwordButton.GetPathTo(basicSwordButton);

		_player.SetProcessUnhandledInput(false);
		basicSwordButton.GrabFocus();
	}

	private bool IsWallAt(Vector2 position)
	{
		GridPosition cell = PositionToCell(position);
		return !_dungeonMap.IsWalkable(cell.X, cell.Y);
	}

	private void OpenDoorAt(Vector2 position)
	{
		GridPosition cell = PositionToCell(position);

		if (_dungeonMap.OpenDoor(cell.X, cell.Y))
			_dungeonRenderer.RefreshCell(_dungeonMap, cell.X, cell.Y);
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

			OpenDoorAt(enemy.Position);

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
			GridPosition targetCell = PositionToCell(targetPosition);
			DigResult digResult = DigResolver.TryDig(
				_dungeonMap,
				targetCell,
				_player.DiggingTool
			);

			if (digResult != DigResult.NoTarget)
			{
				DungeonCell cell = _dungeonMap.GetCell(
					targetCell.X,
					targetCell.Y
				);
				GD.Print(
					digResult == DigResult.Destroyed
						? $"Player destroyed terrain at {targetCell}."
						: $"Player dug terrain at {targetCell}; " +
							$"durability {cell.Durability}."
				);

				if (digResult == DigResult.Destroyed)
				{
					_dungeonRenderer.RefreshCell(
						_dungeonMap,
						targetCell.X,
						targetCell.Y
					);
				}
			}
			else if (!IsWallAt(targetPosition))
			{
				_player.Move(direction);
				UpdatePlayerZone();
				OpenDoorAt(_player.Position);
			}
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
