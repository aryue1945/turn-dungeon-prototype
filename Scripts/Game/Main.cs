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
	private Button _exportHistoryButton;
	private Label _statusLabel;
	private Button _restartButton;
	private Control _endGameOverlay;
	private Control _weaponSelectionPanel;
	private Control _exportMenuOverlay;
	private Button _exportLast5Button;
	private Button _basicSwordButton;
	private Control _startupMenuOverlay;
	private Button _continueButton;
	private Label _startupErrorLabel;
	private Control _confirmOverwriteOverlay;
	private Button _confirmOverwriteCancelButton;
	private Control _pauseMenuOverlay;
	private Button _pauseRestartButton;
	private Button _quitButton;
	private bool _gameStarted;
	private bool _gameEnded;
	private bool _wasPlayerInputEnabledBeforeExportMenu;
	private bool _wasPlayerInputEnabledBeforePauseMenu;
	private SaveFileLoadOutcome _pendingLoadOutcome;
	private bool _hasUnfinishedResumableRun;
	private OverwriteConfirmationReason _overwriteConfirmationReason;
	private static bool _skipStartupMenuForNewRun;

	// Which flow opened the "this will overwrite..." dialog, so its
	// Confirm/Cancel buttons know whether to build a fresh run in place
	// (already at the startup screen, nothing to tear down) or reload the
	// whole scene (abandoning a run in progress via the pause menu).
	private enum OverwriteConfirmationReason
	{
		NewRunFromStartup,
		RestartFromPauseMenu
	}

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
	private GameState _gameState;
	private DebugHistory _debugHistory;
	private Guid _runId;
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

		_runId = Guid.NewGuid();
		_random.Randomize();
		_spawnPool = BuildSpawnPool();

		CreateGameUi();
		CreateWeaponSelection();
		CreateStartupMenu();
		CreateOverwriteConfirmation();
		CreatePauseMenu();

		_player.MoveRequested += OnPlayerMoveRequested;
		_player.HealthChanged += OnPlayerHealthChanged;
		_player.Died += OnPlayerDied;

		_player.SetProcessUnhandledInput(false);

		_pendingLoadOutcome = RunSaveFileService.Load(OS.GetUserDataDir());

		// Set by the pause menu's Restart (which reloads this scene to tear
		// down a live run) right before abandoning a run that was still in
		// progress - its own on-disk save is therefore still "resumable" by
		// ShowStartupMenu's definition, which would otherwise re-offer
		// Continue for the very run the player just chose to discard.
		if (_skipStartupMenuForNewRun)
		{
			_skipStartupMenuForNewRun = false;
			StartNewRun(confirmed: true);
		}
		else
		{
			ShowStartupMenu();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_weaponSelectionPanel.Visible &&
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
			case Key.X:
				OpenExportMenu();
				break;
			case Key.R:
				if (!_gameEnded)
					return;

				OnRestartPressed();
				break;
			case Key.Escape:
				OnEscapePressed();
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

		BuildAndRenderDungeon();

		GD.Print(
			$"Dungeon seed: {_dungeonSeed}; " +
			$"zones: {_dungeonMap.Zones.Count}."
		);
	}

	// Shared between fresh generation (CreateDungeon) and milestone-4
	// Continue (RestoreRun) - both already have a populated _dungeonMap by
	// the time this runs, from generation or GameSnapshotRestore.RestoreMap
	// respectively.
	private void BuildAndRenderDungeon()
	{
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
	}

	private void PlacePlayerInStartRoom()
	{
		DungeonZone startZone = _dungeonMap.Zones.Single(
			zone => zone.Type == DungeonZoneType.Start
		);
		GridPosition startCell = startZone.Room.Center;
		_player.PlaceAt(startCell, CellToPosition(startCell));
		UpdatePlayerZone();
	}

	private void UpdatePlayerZone()
	{
		GridPosition cell = _player.GridPosition;
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
		// GameSnapshot/CellSnapshot only ever captured each cell's ZoneId,
		// not the DungeonZone list itself (room bounds, type, template) -
		// GameSnapshotRestore.RestoreMap has no zone metadata to rebuild
		// Zones from, so GetZone returns null for a Continue'd run even
		// though the zoneId itself is still correct. This narration is
		// cosmetic only (nothing gameplay-relevant reads Zones after
		// setup), so fall back to the bare id instead of crashing.
		DungeonZone zone = _dungeonMap.GetZone(zoneId);

		if (zone == null)
		{
			GD.Print($"Player entered zone {zoneId}.");
			return;
		}

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
		if (_camera == null)
			return;

		SetCameraZoom(_camera.Zoom.X + amount);
	}

	private void SetCameraZoom(float zoom)
	{
		if (_camera == null)
			return;

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
	// monsters are built-in versus modded. MonsterModLoader already rejects
	// a duplicate id between two mod files; this is the one remaining case
	// it cannot see for itself - a mod redefining a built-in id - rejected
	// the same way (disabled with a readable error, built-in wins) rather
	// than silently overwriting or duplicating the built-in definition
	// (NEXT_STEPS milestone 5).
	private List<MonsterDefinition> BuildSpawnPool()
	{
		List<MonsterDefinition> pool = new(MonsterDefinitions.All);
		HashSet<string> builtInIds = new(MonsterDefinitions.All.Select(definition => definition.Id));

		string modsPath = ProjectSettings.GlobalizePath("res://mods");
		MonsterModLoadResult modResult = MonsterModLoader.LoadFromDirectory(modsPath);

		foreach (string error in modResult.Errors)
			GD.PushError(error);

		int addedCount = 0;

		foreach (MonsterDefinition modded in modResult.Monsters)
		{
			if (builtInIds.Contains(modded.Id))
			{
				GD.PushError(
					$"Modded monster \"{modded.Id}\" disabled: duplicate id is already " +
						"defined by a built-in monster."
				);
				continue;
			}

			pool.Add(modded);
			addedCount++;
		}

		if (addedCount > 0)
			GD.Print($"Loaded {addedCount} modded monster(s) from {modsPath}.");

		return pool;
	}

	private void SpawnEnemies()
	{
		HashSet<GridPosition> occupiedCells = new()
		{
			_player.GridPosition
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
				occupiedCells
			);

			MonsterDefinition definition = _spawnPool[i];
			Enemy enemy = _enemyScene.Instantiate<Enemy>();
			enemy.Name = $"{definition.Id.Replace('.', '_')}{i + 1}";
			enemy.Configure(
				definition,
				enemyCell,
				CellToPosition(enemyCell),
				IsWallAt
			);

			occupiedCells.Add(enemyCell);
			_enemies.Add(enemy);
			_gameState.AddEnemy(enemy.State);
			AddChild(enemy);
		}
	}

	private GridPosition GetRandomSpawnCell(
		DungeonRoom room,
		HashSet<GridPosition> occupiedCells)
	{
		for (int attempt = 0; attempt < 100; attempt++)
		{
			GridPosition cell = new(
				_random.RandiRange(room.X, room.Right - 1),
				_random.RandiRange(room.Y, room.Bottom - 1)
			);

			if (_dungeonMap.IsWalkable(cell.X, cell.Y) &&
				!occupiedCells.Contains(cell))
			{
				return cell;
			}
		}

		for (int y = room.Y; y < room.Bottom; y++)
		{
			for (int x = room.X; x < room.Right; x++)
			{
				GridPosition cell = new(x, y);

				if (_dungeonMap.IsWalkable(x, y) &&
					!occupiedCells.Contains(cell))
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
		hudPanel.OffsetBottom = 148;

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

		// Available during play and on the end screen (this HUD panel is
		// never hidden) per docs/SAVE_AND_DEBUG_HISTORY.md.
		_exportHistoryButton = new Button
		{
			Text = "Export Debug History"
		};
		_exportHistoryButton.Pressed += OnExportDebugHistoryPressed;
		hud.AddChild(_exportHistoryButton);

		CenterContainer endGameCenter = new()
		{
			// This spans the full screen (to center its panel when the
			// game ends) and sits above the HUD in the same CanvasLayer.
			// Without this, it silently blocks clicks on anything under it
			// - harmless while the HUD only had labels, but it ate clicks
			// on the export button below once one existed.
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
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

		_quitButton = new Button
		{
			CustomMinimumSize = new Vector2(160, 40),
			Text = "Quit"
		};
		_quitButton.Pressed += OnQuitPressed;
		endGameBox.AddChild(_quitButton);

		_restartButton.FocusNeighborTop = _restartButton.GetPathTo(_quitButton);
		_restartButton.FocusNeighborBottom = _restartButton.GetPathTo(_quitButton);
		_quitButton.FocusNeighborTop = _quitButton.GetPathTo(_restartButton);
		_quitButton.FocusNeighborBottom = _quitButton.GetPathTo(_restartButton);

		CreateExportMenu();
	}

	// The "X" key and the Export button both open this same modal menu.
	// Hidden by default; a full-rect backdrop above the HUD blocks clicks to
	// the game underneath while it is open (opening/using it must not
	// consume a turn, change game state, or use RNG - it only reads the
	// already-captured DebugHistory).
	private void CreateExportMenu()
	{
		CanvasLayer exportMenuLayer = new()
		{
			Layer = 10
		};
		AddChild(exportMenuLayer);

		Control menuRoot = CreateFullRectRoot(exportMenuLayer);
		menuRoot.Visible = false;
		_exportMenuOverlay = menuRoot;

		ColorRect backdrop = new()
		{
			Color = new Color(0, 0, 0, 0.55f)
		};
		menuRoot.AddChild(backdrop);
		backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		CenterContainer menuCenter = new();
		menuRoot.AddChild(menuCenter);
		menuCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		PanelContainer menuPanel = new()
		{
			CustomMinimumSize = new Vector2(260, 220)
		};
		menuCenter.AddChild(menuPanel);

		MarginContainer menuMargin = new();
		menuMargin.AddThemeConstantOverride("margin_left", 20);
		menuMargin.AddThemeConstantOverride("margin_top", 16);
		menuMargin.AddThemeConstantOverride("margin_right", 20);
		menuMargin.AddThemeConstantOverride("margin_bottom", 16);
		menuPanel.AddChild(menuMargin);

		VBoxContainer menuBox = new();
		menuBox.AddThemeConstantOverride("separation", 10);
		menuMargin.AddChild(menuBox);

		Label titleLabel = new()
		{
			CustomMinimumSize = new Vector2(208, 28),
			Text = "Export Debug History",
			HorizontalAlignment = Godot.HorizontalAlignment.Center
		};
		menuBox.AddChild(titleLabel);

		Button last3Button = CreateExportMenuButton("Last 3 turns");
		last3Button.Pressed += () => OnExportOptionSelected(3);
		menuBox.AddChild(last3Button);

		Button last5Button = CreateExportMenuButton("Last 5 turns (default)");
		last5Button.Pressed += () => OnExportOptionSelected(5);
		menuBox.AddChild(last5Button);
		_exportLast5Button = last5Button;

		Button last10Button = CreateExportMenuButton("Last 10 turns");
		last10Button.Pressed += () => OnExportOptionSelected(10);
		menuBox.AddChild(last10Button);

		Button cancelButton = CreateExportMenuButton("Cancel");
		cancelButton.Pressed += CloseExportMenu;
		menuBox.AddChild(cancelButton);
	}

	private static Button CreateExportMenuButton(string text)
	{
		return new Button
		{
			CustomMinimumSize = new Vector2(208, 36),
			Text = text
		};
	}

	private void CreateWeaponSelection()
	{
		CanvasLayer selectionLayer = new()
		{
			Layer = 10
		};
		AddChild(selectionLayer);

		Control selectionRoot = CreateFullRectRoot(selectionLayer);
		selectionRoot.Visible = false;

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
		_basicSwordButton = basicSwordButton;

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
	}

	private void ShowWeaponSelection()
	{
		_weaponSelectionPanel.Visible = true;
		_basicSwordButton.GrabFocus();
	}

	// Shown at boot instead of jumping straight into weapon selection, per
	// the milestone-4 decision table: "Show Continue and New Run when a
	// resumable save exists." Player input stays disabled (set in _Ready)
	// until whichever of Continue/New Run actually starts the game.
	private void CreateStartupMenu()
	{
		CanvasLayer startupLayer = new()
		{
			Layer = 10
		};
		AddChild(startupLayer);

		Control startupRoot = CreateFullRectRoot(startupLayer);
		startupRoot.Visible = false;
		_startupMenuOverlay = startupRoot;

		ColorRect backdrop = new()
		{
			Color = new Color(0, 0, 0, 0.55f)
		};
		startupRoot.AddChild(backdrop);
		backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		CenterContainer startupCenter = new();
		startupRoot.AddChild(startupCenter);
		startupCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		PanelContainer startupPanel = new()
		{
			CustomMinimumSize = new Vector2(280, 220)
		};
		startupCenter.AddChild(startupPanel);

		MarginContainer startupMargin = new();
		startupMargin.AddThemeConstantOverride("margin_left", 24);
		startupMargin.AddThemeConstantOverride("margin_top", 20);
		startupMargin.AddThemeConstantOverride("margin_right", 24);
		startupMargin.AddThemeConstantOverride("margin_bottom", 20);
		startupPanel.AddChild(startupMargin);

		VBoxContainer startupBox = new();
		startupBox.AddThemeConstantOverride("separation", 12);
		startupMargin.AddChild(startupBox);

		Label titleLabel = new()
		{
			CustomMinimumSize = new Vector2(232, 32),
			Text = "Turn Dungeon",
			HorizontalAlignment = Godot.HorizontalAlignment.Center
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 22);
		startupBox.AddChild(titleLabel);

		_startupErrorLabel = new Label
		{
			CustomMinimumSize = new Vector2(232, 40),
			Text = "",
			Visible = false,
			HorizontalAlignment = Godot.HorizontalAlignment.Center
		};
		_startupErrorLabel.AddThemeFontSizeOverride("font_size", 13);
		_startupErrorLabel.AddThemeColorOverride("font_color", Colors.IndianRed);
		startupBox.AddChild(_startupErrorLabel);

		_continueButton = new Button
		{
			CustomMinimumSize = new Vector2(208, 44),
			Text = "Continue"
		};
		_continueButton.Pressed += OnContinuePressed;
		startupBox.AddChild(_continueButton);

		Button newRunButton = new()
		{
			CustomMinimumSize = new Vector2(208, 44),
			Text = "New Run"
		};
		newRunButton.Pressed += OnNewRunPressed;
		startupBox.AddChild(newRunButton);
	}

	// The "confirm before replacing an existing unfinished run" step from
	// the decision table - only ever shown when New Run is chosen while a
	// valid, not-yet-complete save exists (_hasUnfinishedResumableRun).
	private void CreateOverwriteConfirmation()
	{
		CanvasLayer confirmLayer = new()
		{
			Layer = 11
		};
		AddChild(confirmLayer);

		Control confirmRoot = CreateFullRectRoot(confirmLayer);
		confirmRoot.Visible = false;
		_confirmOverwriteOverlay = confirmRoot;

		ColorRect backdrop = new()
		{
			Color = new Color(0, 0, 0, 0.55f)
		};
		confirmRoot.AddChild(backdrop);
		backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		CenterContainer confirmCenter = new();
		confirmRoot.AddChild(confirmCenter);
		confirmCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		PanelContainer confirmPanel = new()
		{
			CustomMinimumSize = new Vector2(280, 170)
		};
		confirmCenter.AddChild(confirmPanel);

		MarginContainer confirmMargin = new();
		confirmMargin.AddThemeConstantOverride("margin_left", 20);
		confirmMargin.AddThemeConstantOverride("margin_top", 16);
		confirmMargin.AddThemeConstantOverride("margin_right", 20);
		confirmMargin.AddThemeConstantOverride("margin_bottom", 16);
		confirmPanel.AddChild(confirmMargin);

		VBoxContainer confirmBox = new();
		confirmBox.AddThemeConstantOverride("separation", 12);
		confirmMargin.AddChild(confirmBox);

		Label messageLabel = new()
		{
			CustomMinimumSize = new Vector2(232, 48),
			Text = "This will overwrite your unfinished run. Continue?",
			HorizontalAlignment = Godot.HorizontalAlignment.Center
		};
		confirmBox.AddChild(messageLabel);

		Button confirmButton = new()
		{
			CustomMinimumSize = new Vector2(208, 40),
			Text = "Start New Run"
		};
		confirmButton.Pressed += OnConfirmOverwritePressed;
		confirmBox.AddChild(confirmButton);

		Button cancelButton = new()
		{
			CustomMinimumSize = new Vector2(208, 40),
			Text = "Cancel"
		};
		cancelButton.Pressed += OnCancelOverwritePressed;
		confirmBox.AddChild(cancelButton);
		_confirmOverwriteCancelButton = cancelButton;

		confirmButton.FocusNeighborTop = confirmButton.GetPathTo(cancelButton);
		confirmButton.FocusNeighborBottom = confirmButton.GetPathTo(cancelButton);
		cancelButton.FocusNeighborTop = cancelButton.GetPathTo(confirmButton);
		cancelButton.FocusNeighborBottom = cancelButton.GetPathTo(confirmButton);
	}

	// A minimal ESC-triggered pause menu, only reachable during active play
	// (OpenPauseMenu gates on _gameStarted/_gameEnded) - Restart routes
	// through the same overwrite-confirmation dialog New Run uses, tagged
	// with which flow opened it (OverwriteConfirmationReason) so Confirm/
	// Cancel do the right thing either way.
	private void CreatePauseMenu()
	{
		CanvasLayer pauseLayer = new()
		{
			Layer = 10
		};
		AddChild(pauseLayer);

		Control pauseRoot = CreateFullRectRoot(pauseLayer);
		pauseRoot.Visible = false;
		_pauseMenuOverlay = pauseRoot;

		ColorRect backdrop = new()
		{
			Color = new Color(0, 0, 0, 0.55f)
		};
		pauseRoot.AddChild(backdrop);
		backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		CenterContainer pauseCenter = new();
		pauseRoot.AddChild(pauseCenter);
		pauseCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		PanelContainer pausePanel = new()
		{
			CustomMinimumSize = new Vector2(260, 190)
		};
		pauseCenter.AddChild(pausePanel);

		MarginContainer pauseMargin = new();
		pauseMargin.AddThemeConstantOverride("margin_left", 20);
		pauseMargin.AddThemeConstantOverride("margin_top", 16);
		pauseMargin.AddThemeConstantOverride("margin_right", 20);
		pauseMargin.AddThemeConstantOverride("margin_bottom", 16);
		pausePanel.AddChild(pauseMargin);

		VBoxContainer pauseBox = new();
		pauseBox.AddThemeConstantOverride("separation", 10);
		pauseMargin.AddChild(pauseBox);

		Label titleLabel = new()
		{
			CustomMinimumSize = new Vector2(208, 28),
			Text = "Paused",
			HorizontalAlignment = Godot.HorizontalAlignment.Center
		};
		pauseBox.AddChild(titleLabel);

		_pauseRestartButton = new Button
		{
			CustomMinimumSize = new Vector2(208, 40),
			Text = "Restart"
		};
		_pauseRestartButton.Pressed += OnPauseRestartPressed;
		pauseBox.AddChild(_pauseRestartButton);

		Button pauseQuitButton = new()
		{
			CustomMinimumSize = new Vector2(208, 40),
			Text = "Quit"
		};
		pauseQuitButton.Pressed += OnQuitPressed;
		pauseBox.AddChild(pauseQuitButton);

		Button resumeButton = new()
		{
			CustomMinimumSize = new Vector2(208, 40),
			Text = "Resume"
		};
		resumeButton.Pressed += ClosePauseMenu;
		pauseBox.AddChild(resumeButton);

		_pauseRestartButton.FocusNeighborTop = _pauseRestartButton.GetPathTo(resumeButton);
		_pauseRestartButton.FocusNeighborBottom = _pauseRestartButton.GetPathTo(pauseQuitButton);
		pauseQuitButton.FocusNeighborTop = pauseQuitButton.GetPathTo(_pauseRestartButton);
		pauseQuitButton.FocusNeighborBottom = pauseQuitButton.GetPathTo(resumeButton);
		resumeButton.FocusNeighborTop = resumeButton.GetPathTo(pauseQuitButton);
		resumeButton.FocusNeighborBottom = resumeButton.GetPathTo(_pauseRestartButton);
	}

	// ESC closes whichever modal is already open (export menu first, then
	// pause menu) rather than stacking a second one on top, and otherwise
	// opens the pause menu.
	private void OnEscapePressed()
	{
		if (_exportMenuOverlay.Visible)
		{
			CloseExportMenu();
			return;
		}

		if (_pauseMenuOverlay.Visible)
		{
			ClosePauseMenu();
			return;
		}

		if (_confirmOverwriteOverlay.Visible)
			return;

		OpenPauseMenu();
	}

	private void OpenPauseMenu()
	{
		if (!_gameStarted || _gameEnded || _pauseMenuOverlay.Visible)
			return;

		_wasPlayerInputEnabledBeforePauseMenu = true;
		_player.SetProcessUnhandledInput(false);

		_pauseMenuOverlay.Visible = true;
		_pauseRestartButton.GrabFocus();
	}

	private void ClosePauseMenu()
	{
		_pauseMenuOverlay.Visible = false;

		if (_wasPlayerInputEnabledBeforePauseMenu)
			_player.SetProcessUnhandledInput(true);
	}

	// Abandoning a run in progress always confirms first (unlike New Run
	// from the startup screen, which only confirms when the existing save
	// is itself unfinished) - the pause menu is only reachable while a run
	// is already in progress, so there is always something to lose here.
	private void OnPauseRestartPressed()
	{
		_pauseMenuOverlay.Visible = false;
		_overwriteConfirmationReason = OverwriteConfirmationReason.RestartFromPauseMenu;
		_confirmOverwriteOverlay.Visible = true;
		_confirmOverwriteCancelButton.GrabFocus();
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}

	// Decides whether a resumable save exists at all (per the decision
	// table, the startup menu only appears when one does - otherwise go
	// straight to New Run). A save whose run already ended (Won/Lost) does
	// not count as resumable - there is nothing left to continue playing,
	// so both a fresh launch and a post-Restart reload go straight to a
	// new run instead of offering to "continue" a finished one.
	private void ShowStartupMenu()
	{
		bool hasResumableSave =
			(_pendingLoadOutcome.Result == SaveFileLoadResult.Loaded ||
				_pendingLoadOutcome.Result == SaveFileLoadResult.LoadedFromBackup) &&
			!_pendingLoadOutcome.Envelope.IsComplete;

		_hasUnfinishedResumableRun = hasResumableSave;

		if (!hasResumableSave)
		{
			if (_pendingLoadOutcome.Result == SaveFileLoadResult.Invalid)
				GD.PushError($"Save file could not be loaded: {_pendingLoadOutcome.Error}");

			StartNewRun(confirmed: true);
			return;
		}

		_continueButton.Disabled = false;
		_startupErrorLabel.Visible = _pendingLoadOutcome.Result == SaveFileLoadResult.LoadedFromBackup;

		if (_startupErrorLabel.Visible)
			_startupErrorLabel.Text = "Recovered from backup - the current save was invalid.";

		_startupMenuOverlay.Visible = true;
		_continueButton.GrabFocus();
	}

	private void OnContinuePressed()
	{
		RunSaveEnvelope envelope = _pendingLoadOutcome.Envelope;

		if (!TryResolveSaveDefinitions(
			envelope,
			out WeaponDefinition weapon,
			out DiggingToolDefinition tool,
			out List<MonsterDefinition> enemyDefinitions,
			out string error))
		{
			_startupErrorLabel.Text = $"Cannot continue: {error}";
			_startupErrorLabel.Visible = true;
			_continueButton.Disabled = true;
			return;
		}

		_startupMenuOverlay.Visible = false;
		RestoreRun(envelope, weapon, tool, enemyDefinitions);
	}

	private void OnNewRunPressed()
	{
		_overwriteConfirmationReason = OverwriteConfirmationReason.NewRunFromStartup;
		StartNewRun(confirmed: false);
	}

	private void OnConfirmOverwritePressed()
	{
		if (_overwriteConfirmationReason == OverwriteConfirmationReason.RestartFromPauseMenu)
		{
			// A live run is being torn down via a full scene reload, unlike
			// StartNewRun (already at the startup screen, nothing to tear
			// down) - the reload's own _Ready would otherwise re-detect this
			// still-in-progress run's own save as resumable and show
			// Continue/New Run again for the very run just abandoned.
			_skipStartupMenuForNewRun = true;
			GetTree().ReloadCurrentScene();
			return;
		}

		StartNewRun(confirmed: true);
	}

	private void OnCancelOverwritePressed()
	{
		_confirmOverwriteOverlay.Visible = false;

		if (_overwriteConfirmationReason == OverwriteConfirmationReason.RestartFromPauseMenu)
		{
			_pauseMenuOverlay.Visible = true;
			_pauseRestartButton.GrabFocus();
			return;
		}

		_startupMenuOverlay.Visible = true;
		_continueButton.GrabFocus();
	}

	// Resolves every saved id (WeaponId, ToolId, each enemy's DefinitionId)
	// back into a real definition before touching any game state - Continue
	// must not partially tear down the startup screen only to discover a
	// missing weapon/monster mod halfway through restoring.
	private bool TryResolveSaveDefinitions(
		RunSaveEnvelope envelope,
		out WeaponDefinition weapon,
		out DiggingToolDefinition tool,
		out List<MonsterDefinition> enemyDefinitions,
		out string error)
	{
		enemyDefinitions = new List<MonsterDefinition>();

		weapon = WeaponDefinitions.FindById(envelope.Snapshot.Player.WeaponId);

		if (weapon == null)
		{
			tool = null;
			error = $"Unknown weapon \"{envelope.Snapshot.Player.WeaponId}\".";
			return false;
		}

		tool = DiggingToolDefinitions.FindById(envelope.Snapshot.Player.ToolId);

		if (tool == null)
		{
			error = $"Unknown digging tool \"{envelope.Snapshot.Player.ToolId}\".";
			return false;
		}

		foreach (ActorSnapshot enemySnapshot in envelope.Snapshot.Enemies)
		{
			MonsterDefinition definition = _spawnPool
				.FirstOrDefault(candidate => candidate.Id == enemySnapshot.DefinitionId);

			if (definition == null)
			{
				error = $"Unknown monster \"{enemySnapshot.DefinitionId}\" (mod not loaded?).";
				return false;
			}

			enemyDefinitions.Add(definition);
		}

		error = null;
		return true;
	}

	// New Run: confirms first if it would replace an unfinished run, then
	// builds a fresh dungeon/player/enemies exactly like the original
	// single-flow boot did, ending at weapon selection.
	private void StartNewRun(bool confirmed)
	{
		if (!confirmed && _hasUnfinishedResumableRun)
		{
			_startupMenuOverlay.Visible = false;
			_confirmOverwriteOverlay.Visible = true;
			return;
		}

		_startupMenuOverlay.Visible = false;
		_confirmOverwriteOverlay.Visible = false;

		CreateDungeon();
		PlacePlayerInStartRoom();
		_gameState = new GameState(_dungeonMap, _player.State);
		CreateFollowingCamera();
		SpawnEnemies();

		GD.Print($"Player health: {_player.Health}");
		GD.Print($"Spawned {_enemies.Count} enemies.");

		ShowWeaponSelection();
	}

	// Continue: rebuilds map/actor/enemy views from a validated save
	// instead of fresh generation, then resumes (or, for an already-
	// finished run, shows the same end screen a live run would have
	// reached). Weapon selection is skipped entirely - the saved loadout is
	// restored, not re-chosen.
	private void RestoreRun(
		RunSaveEnvelope envelope,
		WeaponDefinition weapon,
		DiggingToolDefinition tool,
		List<MonsterDefinition> enemyDefinitions)
	{
		GameSnapshot snapshot = envelope.Snapshot;

		_dungeonSeed = envelope.Seed;
		_dungeonMap = GameSnapshotRestore.RestoreMap(snapshot.Grid, envelope.Seed);
		BuildAndRenderDungeon();

		_player.Attack.Equip(weapon.PrimaryAttack);
		ActorState playerState = GameSnapshotRestore.RestoreActor(snapshot.Player, _player.Attack);
		_player.RestoreFrom(playerState, weapon, tool, CellToPosition(snapshot.Player.Position));

		_gameState = new GameState(_dungeonMap, playerState);
		_gameState.RestoreTurnNumber(snapshot.TurnNumber);

		CreateFollowingCamera();

		_enemies.Clear();
		for (int i = 0; i < snapshot.Enemies.Count; i++)
		{
			ActorSnapshot enemySnapshot = snapshot.Enemies[i];
			MonsterDefinition definition = enemyDefinitions[i];
			Vector2 pixelPosition = CellToPosition(enemySnapshot.Position);

			Enemy enemy = _enemyScene.Instantiate<Enemy>();
			enemy.Name = $"{definition.Id.Replace('.', '_')}{i + 1}";
			enemy.Configure(definition, enemySnapshot.Position, pixelPosition, IsWallAt);
			AddChild(enemy);
			enemy.RestoreFrom(enemySnapshot, pixelPosition);

			_enemies.Add(enemy);
			_gameState.AddEnemy(enemy.State);
		}

		_currentPlayerZoneId = -1;
		UpdatePlayerZone();

		_weaponLabel.Text = $"Weapon: {weapon.Name}";
		_toolLabel.Text = $"Tool: {tool.Name}";
		_healthLabel.Text = $"HP: {_player.Health}";

		_gameStarted = true;
		_debugHistory = new DebugHistory(GameSnapshot.Capture(_gameState));

		if (snapshot.Status != RunStatus.InProgress)
			EndGame(snapshot.Status == RunStatus.Won);
		else
			_player.SetProcessUnhandledInput(true);

		GD.Print($"Continued run at turn {_gameState.TurnNumber}.");
	}

	private bool IsWallAt(GridPosition position)
	{
		return !_dungeonMap.IsWalkable(position.X, position.Y);
	}

	// Narrates a resolved player action and applies the presentation-only
	// side effects the rule itself doesn't own (printing, refreshing
	// changed cells, zone tracking). TurnResolver already made every
	// gameplay mutation (damage, terrain, movement, door state).
	private void ApplyPlayerActionOutcome(PlayerActionOutcome outcome)
	{
		switch (outcome.Kind)
		{
			case PlayerActionKind.Attacked:
				GD.Print($"Player used {outcome.AttackName}.");
				break;

			case PlayerActionKind.Preparing:
				GD.Print($"Player prepares {outcome.AttackName}.");
				break;

			case PlayerActionKind.TerrainDestroyed:
				GD.Print($"Player destroyed terrain at {outcome.TargetCell}.");
				_dungeonRenderer.RefreshCell(
					_dungeonMap,
					outcome.TargetCell.Value.X,
					outcome.TargetCell.Value.Y
				);
				break;

			case PlayerActionKind.TerrainDug:
				GD.Print(
					$"Player dug terrain at {outcome.TargetCell}; " +
						$"durability {outcome.RemainingDurability}."
				);
				break;

			case PlayerActionKind.Moved:
				UpdatePlayerZone();

				if (outcome.DoorOpened)
				{
					_dungeonRenderer.RefreshCell(
						_dungeonMap,
						outcome.TargetCell.Value.X,
						outcome.TargetCell.Value.Y
					);
				}
				break;

			case PlayerActionKind.Blocked:
				GD.Print($"Player hit wall at {outcome.TargetCell}");
				break;
		}
	}

	// Enemy counterpart to ApplyPlayerActionOutcome. Idle/Prepared/Moved/
	// Blocked need no narration (Enemy already prints health changes via
	// TakeDamage); only the attack cases and door refresh need Main's
	// involvement here.
	private void ApplyEnemyActionOutcome(Enemy enemy, EnemyActionOutcome outcome)
	{
		switch (outcome.Kind)
		{
			case EnemyActionKind.Attacked:
				GD.Print($"{enemy.Name} used {outcome.AttackName}.");
				break;

			case EnemyActionKind.Preparing:
				GD.Print($"{enemy.Name} prepares {outcome.AttackName}.");
				break;
		}

		if (outcome.DoorOpened)
		{
			_dungeonRenderer.RefreshCell(
				_dungeonMap,
				outcome.ResultingPosition.X,
				outcome.ResultingPosition.Y
			);
		}
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

		_gameState.RemoveDefeatedEnemies();
	}

	private HashSet<GridPosition> GetOccupiedEnemyPositions(Enemy movingEnemy)
	{
		HashSet<GridPosition> occupiedPositions = new();

		foreach (Enemy enemy in _enemies)
		{
			if (enemy != movingEnemy && IsEnemyActive(enemy))
				occupiedPositions.Add(enemy.GridPosition);
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

	private List<EnemyActionOutcome> TakeEnemyTurns()
	{
		List<EnemyActionOutcome> outcomes = new();

		foreach (Enemy enemy in _enemies)
		{
			if (!IsEnemyActive(enemy))
				continue;

			HashSet<GridPosition> occupiedPositions =
				GetOccupiedEnemyPositions(enemy);

			EnemyActionOutcome outcome = TurnResolver.ResolveEnemyAction(
				enemy,
				_player,
				occupiedPositions,
				GetCombatants(),
				_dungeonMap
			);

			ApplyEnemyActionOutcome(enemy, outcome);
			outcomes.Add(outcome);

			if (_gameState.IsPlayerDefeated)
				break;
		}

		return outcomes;
	}

	private void OnPlayerMoveRequested(Vector2 direction)
	{
		if (_gameEnded || !_gameStarted)
			return;

		PlayerActionOutcome outcome = TurnResolver.ResolvePlayerAction(
			_player,
			direction,
			GetCombatants(),
			_dungeonMap,
			IsWallAt
		);

		ApplyPlayerActionOutcome(outcome);

		RemoveDefeatedEnemies();
		CheckForVictory();

		if (_gameEnded)
		{
			FinishTurn(direction, outcome, Array.Empty<EnemyActionOutcome>());
			return;
		}

		List<EnemyActionOutcome> enemyOutcomes = TakeEnemyTurns();

		RemoveDefeatedEnemies();
		CheckForVictory();
		FinishTurn(direction, outcome, enemyOutcomes);
	}

	// Completes bookkeeping for one fully-resolved turn: advances GameState,
	// records the debug-history transition, then autosaves exactly once -
	// both call sites above reach this exactly once per turn, so a turn
	// that ends in victory/death still only produces a single save
	// (capturing the terminal status), never a redundant second one.
	private void FinishTurn(
		Vector2 direction,
		PlayerActionOutcome outcome,
		IReadOnlyList<EnemyActionOutcome> enemyOutcomes)
	{
		_gameState.CompleteTurn();
		RecordTransition(direction, outcome, enemyOutcomes);
		AutosaveCurrentRun();
	}

	// Appends one consumed turn to the debug ring, using the state after
	// CompleteTurn so the transition's TurnNumber matches its own resulting
	// snapshot (see docs/SAVE_AND_DEBUG_HISTORY.md's turn-25-retains-15..25
	// example). _debugHistory only exists once a weapon is chosen, matching
	// the guard at the top of this method.
	private void RecordTransition(
		Vector2 direction,
		PlayerActionOutcome playerOutcome,
		IReadOnlyList<EnemyActionOutcome> enemyOutcomes)
	{
		_debugHistory?.AppendTransition(new TurnTransition(
			_gameState.TurnNumber,
			direction,
			playerOutcome,
			enemyOutcomes,
			GameSnapshot.Capture(_gameState)
		));
	}

	private void CheckForVictory()
	{
		if (!_gameEnded && _gameState.AreAllEnemiesDefeated)
			EndGame(true);
	}

	private void EndGame(bool playerWon)
	{
		if (_gameEnded)
			return;

		_gameEnded = true;
		_gameState.SetStatus(playerWon ? RunStatus.Won : RunStatus.Lost);
		_player.SetProcessUnhandledInput(false);

		_statusLabel.Text = playerWon ? "YOU WIN!" : "GAME OVER";
		_statusLabel.AddThemeColorOverride(
			"font_color",
			playerWon ? Colors.LimeGreen : Colors.IndianRed
		);
		_endGameOverlay.Visible = true;
		_restartButton.GrabFocus();

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

		// The debug ring's initial state, captured after setup/weapon
		// choice and before the first command - see
		// docs/SAVE_AND_DEBUG_HISTORY.md.
		_debugHistory = new DebugHistory(GameSnapshot.Capture(_gameState));

		GD.Print($"Equipped {_player.Weapon.Name}.");

		AutosaveCurrentRun();
	}

	// Autosave triggers per the milestone-4 decision table: after initial
	// weapon selection/setup (here) and after every completed gameplay
	// turn (FinishTurn) - including the turn that ends in victory/death,
	// captured by the same single save rather than a second one. Failures
	// are logged, not thrown - a save is a side effect of playing, not
	// something that should crash a turn.
	private void AutosaveCurrentRun()
	{
		try
		{
			RunSaveFileService.Save(OS.GetUserDataDir(), RunSaveEnvelope.Capture(_gameState));
		}
		catch (Exception exception)
		{
			GD.PushError($"Autosave failed: {exception.Message}");
		}
	}

	private void OnRestartPressed()
	{
		GetTree().ReloadCurrentScene();
	}

	// The Export button and the "X" key both open this same menu - neither
	// consumes a turn, changes game state, or uses RNG (see
	// docs/SAVE_AND_DEBUG_HISTORY.md); opening it only toggles UI visibility.
	private void OnExportDebugHistoryPressed()
	{
		OpenExportMenu();
	}

	private void OpenExportMenu()
	{
		if (_debugHistory == null)
		{
			GD.Print("No debug history to export yet.");
			return;
		}

		if (_exportMenuOverlay.Visible || _pauseMenuOverlay.Visible)
			return;

		_wasPlayerInputEnabledBeforeExportMenu = _gameStarted && !_gameEnded;

		if (_wasPlayerInputEnabledBeforeExportMenu)
			_player.SetProcessUnhandledInput(false);

		_exportMenuOverlay.Visible = true;
		_exportLast5Button.GrabFocus();
	}

	private void CloseExportMenu()
	{
		_exportMenuOverlay.Visible = false;

		if (_wasPlayerInputEnabledBeforeExportMenu)
			_player.SetProcessUnhandledInput(true);
	}

	private void OnExportOptionSelected(int transitionCount)
	{
		CloseExportMenu();
		ExportDebugHistory(transitionCount);
	}

	// Reads the already-captured DebugHistory and writes it out - no turn,
	// state or RNG involved, matching the export rule in
	// docs/SAVE_AND_DEBUG_HISTORY.md.
	private void ExportDebugHistory(int transitionCount)
	{
		if (_debugHistory == null)
		{
			GD.Print("No debug history to export yet.");
			return;
		}

		DebugHistoryExportContext context = new(
			_runId,
			_dungeonSeed,
			floorId: null,
			buildVersion: System.Reflection.Assembly
				.GetExecutingAssembly()
				.GetName()
				.Version?
				.ToString(),
			weapons: BuildWeaponSummaries(),
			monsters: BuildMonsterSummaries()
		);

		string json = DebugHistoryExporter.ToJson(_debugHistory, transitionCount, context);
		string path = System.IO.Path.Combine(
			OS.GetUserDataDir(),
			"debug_history_export.json"
		);
		System.IO.File.WriteAllText(path, json);

		GD.Print($"Exported last {transitionCount} turn(s) of debug history to {path}");
	}

	// The known weapon roster (no weapon-mod loader exists yet, unlike
	// monsters) and the actual spawn pool for this run (built-in plus any
	// loaded monster mods), so custom content shows up in the export exactly
	// as it behaves in this run.
	private static List<WeaponSummary> BuildWeaponSummaries()
	{
		return WeaponDefinitions.All.Select(WeaponSummary.From).ToList();
	}

	private List<MonsterSummary> BuildMonsterSummaries()
	{
		return _spawnPool.Select(MonsterSummary.From).ToList();
	}
}
