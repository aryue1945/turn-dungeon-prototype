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
	private readonly Dictionary<string, Texture2D> _weaponTextures = new();
	private List<MonsterDefinition> _spawnPool = new();

	private Player _player;
	private Label _weaponLabel;
	private TextureRect _weaponIcon;
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
	private Button _newRunButton;
	private Button _settingsButton;
	private Label _startupErrorLabel;
	private Control _settingsOverlay;
	private OptionButton _displayModeOption;
	private OptionButton _windowSizeOption;
	private CheckButton _vsyncCheckButton;
	private VideoSettings _videoSettings;
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
	private bool _suppressAutosave;
	private OverwriteConfirmationReason _overwriteConfirmationReason;
	private static bool _skipStartupMenuForNewRun;

	private const int SandboxWidth = 18;
	private const int SandboxHeight = 12;

	private bool _inSandbox;
	private ScenarioSandbox _sandbox;
	private Node2D _sandboxCursorVisual;
	private VBoxContainer _sandboxToolbar;
	private Label _sandboxModeLabel;
	private Button _sandboxRunButton;
	private Button _sandboxRestartButton;
	private Button _sandboxResetButton;
	private Button _sandboxExitButton;
	private GameSnapshot _sandboxInitialSnapshot;
	private GameSnapshot _sandboxPlaySnapshot;
	private List<MonsterDefinition> _sandboxPlayEnemyDefinitions;

	// The click-to-place menu (docs/DEBUG_SCENARIO_EDITOR.md): pick an object;
	// enemies then choose a facing, while other objects apply immediately.
	private Control _sandboxPlacementOverlay;
	private Control _sandboxObjectPage;
	private VBoxContainer _sandboxObjectListBox;
	private readonly Dictionary<Button, Action> _sandboxCategoryActions = new();
	private VBoxContainer _sandboxDirectionPage;
	private Button _sandboxFirstObjectButton;
	private Button _sandboxActiveCategoryButton;
	private Button _sandboxFirstSubgroupButton;
	private Button _sandboxFirstDirectionButton;
	private bool _sandboxPlacementMenuOpen;
	private MonsterDefinition _pendingMonsterDefinition;
	private GridPosition _pendingPlacementCell;

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
	private Texture2D _spikeSafeTexture;
	private Texture2D _spikeWarningTexture;
	private Texture2D _spikeActiveTexture;
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
		// Stylized Tactical direction (Into the Breach-referenced), approved
		// and propagated from the isolated Palette Test area - see
		// Art/Prototype/ (which now holds only the pieces without a
		// production slot yet: elite accent, reward, interactable) and
		// GamePalette.cs for the semantic colors baked into this art.
		// Floor has one tile now, not a base+cracked pair - the crack
		// texture was part of the old noisy direction being replaced, so
		// the same floor texture fills both slots.
		Texture2D floorTexture = GD.Load<Texture2D>("res://Art/Tiles/floor.png");
		_floorTexture = floorTexture;
		_floorCrackedTexture = floorTexture;

		// One wall texture for every boundary/divider orientation - the
		// old per-orientation brick art does not carry over, since the new
		// wall reads as a raised block from any side already.
		Texture2D wallTexture = GD.Load<Texture2D>("res://Art/Tiles/wall.png");
		_wallHorizontalTexture = wallTexture;
		_wallVerticalTexture = wallTexture;
		_wallCornerLeftTexture = wallTexture;
		_wallCornerRightTexture = wallTexture;
		_wallBarsTexture = wallTexture;

		_breakableWallTexture = GD.Load<Texture2D>("res://Art/Tiles/wall_breakable.png");
		_doorTexture = GD.Load<Texture2D>("res://Art/Tiles/door.png");
		_spikeSafeTexture = GD.Load<Texture2D>("res://Art/Tiles/spike_safe.png");
		_spikeWarningTexture = GD.Load<Texture2D>("res://Art/Tiles/spike_warning.png");
		_spikeActiveTexture = GD.Load<Texture2D>("res://Art/Tiles/spike_active.png");

		foreach (WeaponDefinition weapon in WeaponDefinitions.All)
		{
			if (!string.IsNullOrWhiteSpace(weapon.SpritePath))
				_weaponTextures[weapon.Id] = GD.Load<Texture2D>(weapon.SpritePath);
		}

		_runId = Guid.NewGuid();
		_random.Randomize();
		_spawnPool = BuildSpawnPool();
		_videoSettings = VideoSettings.Load();
		_videoSettings.Apply();

		CreateGameUi();
		CreateWeaponSelection();
		CreateStartupMenu();
		CreateSettingsMenu();
		CreateOverwriteConfirmation();
		CreatePauseMenu();
		CreateSandboxCursorVisual();
		CreateSandboxPlacementMenu();

		_player.MoveRequested += OnPlayerMoveRequested;
		_player.WaitRequested += OnPlayerWaitRequested;
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

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey tabKey &&
			tabKey.Pressed &&
			!tabKey.Echo &&
			tabKey.Keycode == Key.Tab)
		{
			HandleGameplayTab();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventKey placementKey &&
			placementKey.Pressed &&
			!placementKey.Echo &&
			_sandboxPlacementMenuOpen &&
			_sandboxObjectPage.Visible)
		{
			if (placementKey.Keycode == Key.Left &&
				_sandboxActiveCategoryButton != null &&
				GetViewport().GuiGetFocusOwner() is Control placementFocus &&
				_sandboxObjectListBox.IsAncestorOf(placementFocus))
			{
				_sandboxActiveCategoryButton.GrabFocus();
				GetViewport().SetInputAsHandled();
			}
			else if (placementKey.Keycode == Key.Right &&
				GetViewport().GuiGetFocusOwner() is Button categoryButton &&
				_sandboxCategoryActions.TryGetValue(categoryButton, out Action openCategory))
			{
				openCategory();
				GetViewport().SetInputAsHandled();
			}
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
			else if (weaponKey.Keycode == Key.Key3)
				OnWarHammerSelected();
			else
				return;

			GetViewport().SetInputAsHandled();
			return;
		}

		// Debug-only launcher for the hand-built War Hammer/Charging
		// Beetle/Spike Trap encounter (NEXT_STEPS roadmap item 3), only
		// reachable from the startup screen so it can never fire mid-play.
		// To be retired once Sandbox mode can reproduce this scenario as a
		// saved scenario (docs/DEBUG_SCENARIO_EDITOR.md's acceptance note).
		if (_startupMenuOverlay.Visible &&
			@event is InputEventKey debugKey &&
			debugKey.Pressed &&
			!debugKey.Echo &&
			debugKey.Keycode == Key.F)
		{
			StartFixedEncounter();
			GetViewport().SetInputAsHandled();
			return;
		}

		// Sandbox mode is entered from the startup screen's "Debug" button
		// (OnDebugPressed), not a keybinding - docs/DEBUG_SCENARIO_EDITOR.md
		// and this session's follow-up keep controls visible and discoverable;
		// WASD/arrows move the cursor, Enter/Space opens placement, and Esc
		// backs out of the current Sandbox state or menu.

		// Sandbox's Edit-state cursor rides the same move_* actions as
		// player movement - safe to reuse since Player's own unhandled
		// input is disabled for the whole time Edit is active, so these
		// actions would otherwise go nowhere. Echo is deliberately allowed
		// through (unlike player movement) so holding a direction repeats,
		// which is what scanning across cells with a cursor should do.
		// Suppressed while the placement menu is open so arrow keys don't
		// move the cursor out from under an in-progress placement.
		if (_inSandbox &&
			_sandbox.State == SandboxState.Edit &&
			!_sandboxPlacementMenuOpen &&
			GetViewport().GuiGetFocusOwner() == null &&
			@event is InputEventKey sandboxMoveKey &&
			sandboxMoveKey.Pressed)
		{
			Vector2 direction = Vector2.Zero;

			if (@event.IsActionPressed("move_left"))
				direction = Vector2.Left;
			else if (@event.IsActionPressed("move_right"))
				direction = Vector2.Right;
			else if (@event.IsActionPressed("move_up"))
				direction = Vector2.Up;
			else if (@event.IsActionPressed("move_down"))
				direction = Vector2.Down;

			if (direction != Vector2.Zero)
			{
				_sandbox.MoveCursor((int)direction.X, (int)direction.Y);
				UpdateSandboxCursorVisual();
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (_inSandbox &&
			_sandbox.State == SandboxState.Edit &&
			!_sandboxPlacementMenuOpen &&
			GetViewport().GuiGetFocusOwner() == null &&
			@event is InputEventKey sandboxSelectKey &&
			sandboxSelectKey.Pressed &&
			!sandboxSelectKey.Echo &&
			(sandboxSelectKey.Keycode == Key.Enter ||
				sandboxSelectKey.Keycode == Key.KpEnter ||
				sandboxSelectKey.Keycode == Key.Space))
		{
			OpenSandboxPlacementMenu(_sandbox.CursorPosition);
			GetViewport().SetInputAsHandled();
			return;
		}

		// Click-to-place: left-click a cell in Edit to open the object +
		// direction menu (OpenSandboxPlacementMenu). Checked before the
		// zoom-wheel handling below, which otherwise swallows every mouse
		// button press.
		if (_inSandbox &&
			_sandbox.State == SandboxState.Edit &&
			!_sandboxPlacementMenuOpen &&
			@event is InputEventMouseButton sandboxClick &&
			sandboxClick.Pressed &&
			sandboxClick.ButtonIndex == MouseButton.Left)
		{
			GridPosition cell = ScenarioSandbox.PixelToCell(GetGlobalMousePosition(), MapOrigin, TileSize);

			if (cell.X >= 0 && cell.X < SandboxWidth && cell.Y >= 0 && cell.Y < SandboxHeight)
				OpenSandboxPlacementMenu(cell);

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
		_dungeonRenderer?.Clear();
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
			_spikeSafeTexture,
			_spikeWarningTexture,
			_spikeActiveTexture,
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
				MapOrigin.X + (_dungeonMap.Width - 1) * TileSize + TileSize / 2
			),
			LimitBottom = Mathf.RoundToInt(
				MapOrigin.Y + (_dungeonMap.Height - 1) * TileSize + TileSize / 2
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

	// Roster and placement come from EncounterPlanner, seeded from this
	// run's own dungeon seed rather than Main's general-purpose _random -
	// the same seed/config/content must always produce the same roster,
	// order and positions (NEXT_STEPS milestone 5), which an unseeded RNG
	// (what this used before) cannot guarantee.
	private void SpawnEnemies()
	{
		List<DungeonRoom> combatRooms = _dungeonMap.Zones
			.Where(zone => zone.Type == DungeonZoneType.Combat)
			.Select(zone => zone.Room)
			.ToList();

		HashSet<GridPosition> occupiedCells = new()
		{
			_player.GridPosition
		};

		IReadOnlyList<PlannedSpawn> plannedSpawns = EncounterPlanner.PlanSpawns(
			_spawnPool,
			combatRooms,
			cell => _dungeonMap.IsWalkable(cell.X, cell.Y) && !occupiedCells.Contains(cell),
			_dungeonSeed
		);

		for (int i = 0; i < plannedSpawns.Count; i++)
		{
			PlannedSpawn spawn = plannedSpawns[i];
			Enemy enemy = _enemyScene.Instantiate<Enemy>();
			enemy.Name = $"{spawn.Definition.Id.Replace('.', '_')}{i + 1}";
			enemy.Configure(
				spawn.Definition,
				spawn.Position,
				CellToPosition(spawn.Position),
				IsWallAt
			);

			_enemies.Add(enemy);
			_gameState.AddEnemy(enemy.State);
			AddChild(enemy);
		}
	}

	// Tab is reserved for switching between the playfield and its contextual
	// HUD/toolbar. It never advances to another menu item; arrows do that.
	// Modal menus consume Tab without changing focus.
	private bool HandleGameplayTab()
	{
		if (_startupMenuOverlay.Visible ||
			_settingsOverlay.Visible ||
			_confirmOverwriteOverlay.Visible ||
			_pauseMenuOverlay.Visible ||
			_exportMenuOverlay.Visible ||
			_weaponSelectionPanel.Visible ||
			_sandboxPlacementOverlay.Visible ||
			_endGameOverlay.Visible)
		{
			return true;
		}

		Control focusOwner = GetViewport().GuiGetFocusOwner();

		if (_inSandbox)
		{
			Button modeButton = _sandbox.State == SandboxState.Edit
				? _sandboxRunButton
				: _sandboxRestartButton;
			Button[] controls = { modeButton, _sandboxResetButton, _sandboxExitButton };
			int focusedIndex = Array.IndexOf(controls, focusOwner);

			if (focusedIndex < 0)
			{
				controls[0].GrabFocus();
				_player.SetProcessUnhandledInput(false);
				return true;
			}

			focusOwner.ReleaseFocus();
			if (_sandbox.State == SandboxState.Play)
				_player.SetProcessUnhandledInput(true);

			return true;
		}

		if (!_gameStarted || _gameEnded)
			return true;

		if (focusOwner == _exportHistoryButton)
		{
			focusOwner.ReleaseFocus();
			_player.SetProcessUnhandledInput(true);
		}
		else
		{
			_exportHistoryButton.GrabFocus();
			_player.SetProcessUnhandledInput(false);
		}

		return true;
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

		// A bare full-rect Control defaults to MouseFilter.Stop, which
		// silently swallows every mouse click across the whole screen -
		// including blank areas with no visible HUD element - before it
		// ever reaches _UnhandledInput. Ignore here lets clicks fall
		// through to the game world (Sandbox placement, camera zoom); the
		// HUD panel and buttons below keep their own default Stop filter,
		// so clicks on them are still captured normally.
		uiRoot.MouseFilter = Control.MouseFilterEnum.Ignore;

		PanelContainer hudPanel = new();
		uiRoot.AddChild(hudPanel);
		hudPanel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		hudPanel.OffsetLeft = 12;
		hudPanel.OffsetTop = 12;
		hudPanel.OffsetRight = 118;
		hudPanel.OffsetBottom = 64;

		MarginContainer hudMargin = new();
		hudMargin.AddThemeConstantOverride("margin_left", 5);
		hudMargin.AddThemeConstantOverride("margin_top", 3);
		hudMargin.AddThemeConstantOverride("margin_right", 5);
		hudMargin.AddThemeConstantOverride("margin_bottom", 3);
		hudPanel.AddChild(hudMargin);

		VBoxContainer hud = new();
		hud.AddThemeConstantOverride("separation", 2);
		hudMargin.AddChild(hud);

		HBoxContainer weaponRow = new();
		weaponRow.AddThemeConstantOverride("separation", 3);
		hud.AddChild(weaponRow);

		_weaponIcon = new TextureRect
		{
			CustomMinimumSize = new Vector2(12, 12),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Visible = false
		};
		weaponRow.AddChild(_weaponIcon);

		_weaponLabel = new Label
		{
			Text = "Weapon: not selected"
		};
		_weaponLabel.AddThemeFontSizeOverride("font_size", 10);
		_weaponLabel.AddThemeColorOverride("font_color", Colors.White);
		weaponRow.AddChild(_weaponLabel);

		_toolLabel = new Label
		{
			Text = $"Tool: {_player.DiggingTool.Name}"
		};
		_toolLabel.AddThemeFontSizeOverride("font_size", 10);
		_toolLabel.AddThemeColorOverride("font_color", Colors.White);
		hud.AddChild(_toolLabel);

		// Available during play and on the end screen (this HUD panel is
		// never hidden) per docs/SAVE_AND_DEBUG_HISTORY.md.
		_exportHistoryButton = new Button
		{
			Text = "Export Debug History"
		};
		_exportHistoryButton.Pressed += OnExportDebugHistoryPressed;
		_exportHistoryButton.AddThemeFontSizeOverride("font_size", 10);
		_exportHistoryButton.CustomMinimumSize = new Vector2(96, 18);
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

		// Sandbox's on-screen controls (docs/DEBUG_SCENARIO_EDITOR.md): a
		// mode readout plus Run/Restart/Reset/Exit buttons, replacing the earlier
		// F2/F3/Esc keybindings with visible UI. Keyboard control remains
		// available for grid movement, placement selection, and backing out.
		_sandboxToolbar = new VBoxContainer
		{
			Visible = false
		};
		_sandboxToolbar.AddThemeConstantOverride("separation", 6);
		uiRoot.AddChild(_sandboxToolbar);
		_sandboxToolbar.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		_sandboxToolbar.OffsetLeft = -180;
		_sandboxToolbar.OffsetTop = 12;
		_sandboxToolbar.OffsetRight = -12;
		_sandboxToolbar.OffsetBottom = 210;

		_sandboxModeLabel = new Label
		{
			HorizontalAlignment = Godot.HorizontalAlignment.Right
		};
		_sandboxModeLabel.AddThemeFontSizeOverride("font_size", 14);
		_sandboxModeLabel.AddThemeColorOverride("font_color", Colors.Yellow);
		_sandboxToolbar.AddChild(_sandboxModeLabel);

		_sandboxRunButton = new Button
		{
			CustomMinimumSize = new Vector2(160, 32),
			Text = "Run"
		};
		_sandboxRunButton.Pressed += OnSandboxRunPressed;
		_sandboxToolbar.AddChild(_sandboxRunButton);

		_sandboxRestartButton = new Button
		{
			CustomMinimumSize = new Vector2(160, 32),
			Text = "Restart"
		};
		_sandboxRestartButton.Pressed += OnSandboxRestartPressed;
		_sandboxToolbar.AddChild(_sandboxRestartButton);

		_sandboxResetButton = new Button
		{
			CustomMinimumSize = new Vector2(160, 32),
			Text = "Reset"
		};
		_sandboxResetButton.Pressed += OnSandboxResetPressed;
		_sandboxToolbar.AddChild(_sandboxResetButton);

		Button sandboxExitButton = new()
		{
			CustomMinimumSize = new Vector2(160, 32),
			Text = "Exit"
		};
		sandboxExitButton.Pressed += OnSandboxExitPressed;
		_sandboxToolbar.AddChild(sandboxExitButton);
		_sandboxExitButton = sandboxExitButton;

		Button[] sandboxToolbarButtons =
		{
			_sandboxRunButton,
			_sandboxRestartButton,
			_sandboxResetButton,
			_sandboxExitButton
		};
		foreach (Button button in sandboxToolbarButtons)
		{
			NodePath ownPath = button.GetPathTo(button);
			button.FocusNeighborLeft = ownPath;
			button.FocusNeighborRight = ownPath;
		}

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
			Text = "[1] Basic Sword",
			Icon = GetWeaponIcon(WeaponDefinitions.BasicSword),
			ExpandIcon = false
		};
		basicSwordButton.Pressed += OnBasicSwordSelected;
		selectionBox.AddChild(basicSwordButton);
		_basicSwordButton = basicSwordButton;

		Button longSwordButton = new()
		{
			CustomMinimumSize = new Vector2(208, 44),
			Text = "[2] Long Sword",
			Icon = GetWeaponIcon(WeaponDefinitions.LongSword),
			ExpandIcon = false
		};
		longSwordButton.Pressed += OnLongSwordSelected;
		selectionBox.AddChild(longSwordButton);

		Button warHammerButton = new()
		{
			CustomMinimumSize = new Vector2(208, 44),
			Text = "[3] War Hammer",
			Icon = GetWeaponIcon(WeaponDefinitions.WarHammer),
			ExpandIcon = false
		};
		warHammerButton.Pressed += OnWarHammerSelected;
		selectionBox.AddChild(warHammerButton);

		Label keyboardHint = new()
		{
			Text = "Up/Down, then Enter or Space",
			HorizontalAlignment = Godot.HorizontalAlignment.Center
		};
		keyboardHint.AddThemeFontSizeOverride("font_size", 14);
		selectionBox.AddChild(keyboardHint);

		basicSwordButton.FocusNeighborTop =
			basicSwordButton.GetPathTo(warHammerButton);
		basicSwordButton.FocusNeighborBottom =
			basicSwordButton.GetPathTo(longSwordButton);
		longSwordButton.FocusNeighborTop =
			longSwordButton.GetPathTo(basicSwordButton);
		longSwordButton.FocusNeighborBottom =
			longSwordButton.GetPathTo(warHammerButton);
		warHammerButton.FocusNeighborTop =
			warHammerButton.GetPathTo(longSwordButton);
		warHammerButton.FocusNeighborBottom =
			warHammerButton.GetPathTo(basicSwordButton);
	}

	private void ShowWeaponSelection()
	{
		_weaponSelectionPanel.Visible = true;
		_basicSwordButton.GrabFocus();
	}

	private Texture2D GetWeaponIcon(WeaponDefinition weapon)
	{
		return _weaponTextures.TryGetValue(weapon.Id, out Texture2D texture) ? texture : null;
	}

	// Shared by every path that equips/restores a weapon (SelectWeapon,
	// RestoreRun, StartFixedEncounter) so the HUD label and icon can never
	// drift apart or be updated in only one of them.
	private void UpdateWeaponDisplay(WeaponDefinition weapon)
	{
		_weaponLabel.Text = $"Weapon: {weapon.Name}";

		Texture2D icon = GetWeaponIcon(weapon);
		_weaponIcon.Texture = icon;
		_weaponIcon.Visible = icon != null;
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
			CustomMinimumSize = new Vector2(280, 360)
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

		_newRunButton = new Button
		{
			CustomMinimumSize = new Vector2(208, 44),
			Text = "New Run"
		};
		_newRunButton.Pressed += OnNewRunPressed;
		startupBox.AddChild(_newRunButton);

		_settingsButton = new Button
		{
			CustomMinimumSize = new Vector2(208, 44),
			Text = "Settings"
		};
		_settingsButton.Pressed += OpenSettingsMenu;
		startupBox.AddChild(_settingsButton);

		startupBox.AddChild(new HSeparator());

		// Debug mode selector (docs/DEBUG_SCENARIO_EDITOR.md): Continue/New
		// Run above are the normal-gameplay entry points, this is the
		// separate Sandbox entry point - a visible button rather than a
		// hidden keybinding, replacing this feature's earlier F1 shortcut.
		Button debugButton = new()
		{
			CustomMinimumSize = new Vector2(208, 44),
			Text = "Debug"
		};
		debugButton.Pressed += OnDebugPressed;
		startupBox.AddChild(debugButton);
	}

	private void CreateSettingsMenu()
	{
		CanvasLayer settingsLayer = new()
		{
			Layer = 12
		};
		AddChild(settingsLayer);

		Control settingsRoot = CreateFullRectRoot(settingsLayer);
		settingsRoot.Visible = false;
		_settingsOverlay = settingsRoot;

		ColorRect backdrop = new()
		{
			Color = new Color(0, 0, 0, 0.7f)
		};
		settingsRoot.AddChild(backdrop);
		backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		CenterContainer settingsCenter = new();
		settingsRoot.AddChild(settingsCenter);
		settingsCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		PanelContainer settingsPanel = new()
		{
			CustomMinimumSize = new Vector2(360, 330)
		};
		settingsCenter.AddChild(settingsPanel);

		MarginContainer settingsMargin = new();
		settingsMargin.AddThemeConstantOverride("margin_left", 24);
		settingsMargin.AddThemeConstantOverride("margin_top", 20);
		settingsMargin.AddThemeConstantOverride("margin_right", 24);
		settingsMargin.AddThemeConstantOverride("margin_bottom", 20);
		settingsPanel.AddChild(settingsMargin);

		VBoxContainer settingsBox = new();
		settingsBox.AddThemeConstantOverride("separation", 12);
		settingsMargin.AddChild(settingsBox);

		Label titleLabel = new()
		{
			Text = "Settings",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 22);
		settingsBox.AddChild(titleLabel);

		Label videoLabel = new()
		{
			Text = "Video"
		};
		videoLabel.AddThemeFontSizeOverride("font_size", 18);
		videoLabel.AddThemeColorOverride("font_color", Colors.Yellow);
		settingsBox.AddChild(videoLabel);

		settingsBox.AddChild(new Label { Text = "Display mode" });
		_displayModeOption = new OptionButton();
		_displayModeOption.AddItem("Windowed", 0);
		_displayModeOption.AddItem("Fullscreen", 1);
		_displayModeOption.ItemSelected += OnDisplayModeSelected;
		settingsBox.AddChild(_displayModeOption);

		settingsBox.AddChild(new Label { Text = "Window size" });
		_windowSizeOption = new OptionButton();
		foreach (Vector2I size in VideoSettings.WindowSizes)
			_windowSizeOption.AddItem($"{size.X} x {size.Y}");
		_windowSizeOption.ItemSelected += OnWindowSizeSelected;
		settingsBox.AddChild(_windowSizeOption);

		_vsyncCheckButton = new CheckButton
		{
			Text = "Vertical sync"
		};
		_vsyncCheckButton.Toggled += OnVsyncToggled;
		settingsBox.AddChild(_vsyncCheckButton);

		HBoxContainer actionRow = new();
		actionRow.AddThemeConstantOverride("separation", 10);
		settingsBox.AddChild(actionRow);

		Button backButton = new()
		{
			CustomMinimumSize = new Vector2(302, 40),
			Text = "Back"
		};
		backButton.Pressed += CloseSettingsMenu;
		actionRow.AddChild(backButton);
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

	// A single semi-transparent tile highlighting Sandbox's edit cursor.
	// Created once and reused - Visible toggles with Edit/Play, Position
	// follows ScenarioSandbox.CursorPosition (see UpdateSandboxCursorVisual).
	private void CreateSandboxCursorVisual()
	{
		_sandboxCursorVisual = new Polygon2D
		{
			Polygon = new Vector2[]
			{
				new(-TileSize / 2, -TileSize / 2),
				new(TileSize / 2, -TileSize / 2),
				new(TileSize / 2, TileSize / 2),
				new(-TileSize / 2, TileSize / 2)
			},
			Color = new Color(1f, 1f, 0.2f, 0.35f),
			ZIndex = 5,
			Visible = false
		};
		AddChild(_sandboxCursorVisual);
	}

	// The placement menu (docs/DEBUG_SCENARIO_EDITOR.md, extended per this
	// session's follow-up): left-clicking a cell or pressing Enter/Space in Edit
	// state opens this two-column browser. Broad categories stay on the left;
	// Right/Enter opens their objects on the right and Left returns to the
	// category. Player Start, Delete, and Cancel remain top-level actions.
	private void CreateSandboxPlacementMenu()
	{
		CanvasLayer placementLayer = new()
		{
			Layer = 10
		};
		AddChild(placementLayer);

		Control menuRoot = CreateFullRectRoot(placementLayer);
		menuRoot.Visible = false;
		_sandboxPlacementOverlay = menuRoot;

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
			CustomMinimumSize = new Vector2(500, 420)
		};
		menuCenter.AddChild(menuPanel);

		MarginContainer menuMargin = new();
		menuMargin.AddThemeConstantOverride("margin_left", 20);
		menuMargin.AddThemeConstantOverride("margin_top", 16);
		menuMargin.AddThemeConstantOverride("margin_right", 20);
		menuMargin.AddThemeConstantOverride("margin_bottom", 16);
		menuPanel.AddChild(menuMargin);

		VBoxContainer outerBox = new();
		outerBox.AddThemeConstantOverride("separation", 10);
		menuMargin.AddChild(outerBox);

		Label titleLabel = new()
		{
			CustomMinimumSize = new Vector2(452, 28),
			Text = "Place object",
			HorizontalAlignment = Godot.HorizontalAlignment.Center
		};
		outerBox.AddChild(titleLabel);

		_sandboxObjectPage = new HBoxContainer();
		_sandboxObjectPage.AddThemeConstantOverride("separation", 12);
		outerBox.AddChild(_sandboxObjectPage);

		VBoxContainer categoryBox = new();
		categoryBox.AddThemeConstantOverride("separation", 6);

		_sandboxObjectListBox = new VBoxContainer();
		_sandboxObjectListBox.AddThemeConstantOverride("separation", 6);

		ScrollContainer categoryScroll = new()
		{
			CustomMinimumSize = new Vector2(180, 300)
		};
		categoryScroll.AddChild(categoryBox);
		_sandboxObjectPage.AddChild(categoryScroll);

		ScrollContainer objectScroll = new()
		{
			CustomMinimumSize = new Vector2(260, 300)
		};
		objectScroll.AddChild(_sandboxObjectListBox);
		_sandboxObjectPage.AddChild(objectScroll);

		Button enemyCategory = CreateSandboxCategoryButton("Enemy  →");
		RegisterSandboxCategory(enemyCategory, () => ShowSandboxEnemyGroup(enemyCategory));
		categoryBox.AddChild(enemyCategory);
		_sandboxFirstObjectButton = enemyCategory;

		Button wallCategory = CreateSandboxCategoryButton("Wall  →");
		RegisterSandboxCategory(wallCategory, () => ShowSandboxTerrainGroup(
			wallCategory,
			TerrainKind.SolidWall,
			TerrainKind.BreakableWall,
			TerrainKind.TreeWall,
			TerrainKind.GrowingWall
		));
		categoryBox.AddChild(wallCategory);

		Button groundCategory = CreateSandboxCategoryButton("Ground  →");
		RegisterSandboxCategory(groundCategory, () => ShowSandboxTerrainGroup(
			groundCategory,
			TerrainKind.Floor,
			TerrainKind.Fire,
			TerrainKind.Ice,
			TerrainKind.SpikeTrap
		));
		categoryBox.AddChild(groundCategory);

		Button structureCategory = CreateSandboxCategoryButton("Structure  →");
		RegisterSandboxCategory(structureCategory, () => ShowSandboxTerrainGroup(
			structureCategory,
			TerrainKind.Door
		));
		categoryBox.AddChild(structureCategory);

		Button playerStartButton = CreateSandboxCategoryButton("Player Start");
		playerStartButton.Pressed += SelectSandboxPlayerStart;
		categoryBox.AddChild(playerStartButton);

		Button deleteButton = CreateSandboxCategoryButton("Delete");
		deleteButton.Pressed += ConfirmSandboxDelete;
		categoryBox.AddChild(deleteButton);

		Button objectCancelButton = CreateSandboxCategoryButton("Cancel");
		objectCancelButton.Pressed += CloseSandboxPlacementMenu;
		categoryBox.AddChild(objectCancelButton);

		_sandboxDirectionPage = new VBoxContainer
		{
			Visible = false
		};
		_sandboxDirectionPage.AddThemeConstantOverride("separation", 6);
		outerBox.AddChild(_sandboxDirectionPage);

		Button upButton = CreateExportMenuButton("Facing: Up");
		upButton.Pressed += () => ConfirmSandboxPlacement(Vector2.Up);
		_sandboxDirectionPage.AddChild(upButton);
		_sandboxFirstDirectionButton = upButton;

		Button downButton = CreateExportMenuButton("Facing: Down");
		downButton.Pressed += () => ConfirmSandboxPlacement(Vector2.Down);
		_sandboxDirectionPage.AddChild(downButton);

		Button leftButton = CreateExportMenuButton("Facing: Left");
		leftButton.Pressed += () => ConfirmSandboxPlacement(Vector2.Left);
		_sandboxDirectionPage.AddChild(leftButton);

		Button rightButton = CreateExportMenuButton("Facing: Right");
		rightButton.Pressed += () => ConfirmSandboxPlacement(Vector2.Right);
		_sandboxDirectionPage.AddChild(rightButton);

		Button directionCancelButton = CreateExportMenuButton("Cancel");
		directionCancelButton.Pressed += CloseSandboxPlacementMenu;
		_sandboxDirectionPage.AddChild(directionCancelButton);
	}

	private static Button CreateSandboxCategoryButton(string text)
	{
		return new Button
		{
			CustomMinimumSize = new Vector2(168, 36),
			Text = text
		};
	}

	private void RegisterSandboxCategory(Button button, Action action)
	{
		_sandboxCategoryActions[button] = action;
		button.Pressed += action;
	}

	private void ShowSandboxEnemyGroup(Button categoryButton)
	{
		ClearSandboxSubgroup(categoryButton);

		foreach (MonsterDefinition monster in _spawnPool)
		{
			Button enemyButton = CreateExportMenuButton(monster.Name);
			enemyButton.CustomMinimumSize = new Vector2(248, 36);
			enemyButton.Pressed += () => SelectSandboxEnemy(monster);
			_sandboxObjectListBox.AddChild(enemyButton);
			_sandboxFirstSubgroupButton ??= enemyButton;
		}

		_sandboxFirstSubgroupButton?.GrabFocus();
	}

	private void ShowSandboxTerrainGroup(Button categoryButton, params TerrainKind[] kinds)
	{
		ClearSandboxSubgroup(categoryButton);

		foreach (TerrainKind kind in kinds)
		{
			Button terrainButton = CreateExportMenuButton(TerrainCatalog.Get(kind).Name);
			terrainButton.CustomMinimumSize = new Vector2(248, 36);
			terrainButton.Pressed += () => SelectSandboxTerrain(kind);
			_sandboxObjectListBox.AddChild(terrainButton);
			_sandboxFirstSubgroupButton ??= terrainButton;
		}

		_sandboxFirstSubgroupButton?.GrabFocus();
	}

	private void ClearSandboxSubgroup(Button categoryButton)
	{
		foreach (Node child in _sandboxObjectListBox.GetChildren())
		{
			_sandboxObjectListBox.RemoveChild(child);
			child.QueueFree();
		}

		_sandboxActiveCategoryButton = categoryButton;
		_sandboxFirstSubgroupButton = null;
	}

	private void OpenSandboxPlacementMenu(GridPosition cell)
	{
		_pendingPlacementCell = cell;
		_sandboxObjectPage.Visible = true;
		_sandboxDirectionPage.Visible = false;
		ClearSandboxSubgroup(null);
		_sandboxPlacementOverlay.Visible = true;
		_sandboxPlacementMenuOpen = true;
		_sandboxFirstObjectButton.GrabFocus();
	}

	private void CloseSandboxPlacementMenu()
	{
		_sandboxPlacementOverlay.Visible = false;
		_sandboxPlacementMenuOpen = false;
		GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
	}

	private void SelectSandboxTerrain(TerrainKind kind)
	{
		RemoveSandboxEnemyAt(_pendingPlacementCell);
		_dungeonMap.SetTerrain(_pendingPlacementCell.X, _pendingPlacementCell.Y, kind);
		_dungeonRenderer.RefreshCell(
			_dungeonMap,
			_pendingPlacementCell.X,
			_pendingPlacementCell.Y
		);
		CloseSandboxPlacementMenu();
	}

	private void SelectSandboxPlayerStart()
	{
		_player.PlaceAt(_pendingPlacementCell, CellToPosition(_pendingPlacementCell));
		CloseSandboxPlacementMenu();
	}

	private void SelectSandboxEnemy(MonsterDefinition monster)
	{
		_pendingMonsterDefinition = monster;
		ShowSandboxDirectionPage();
	}

	private void ShowSandboxDirectionPage()
	{
		_sandboxObjectPage.Visible = false;
		_sandboxDirectionPage.Visible = true;
		_sandboxFirstDirectionButton.GrabFocus();
	}

	// Only enemies choose a facing. Terrain orientation is a renderer concern,
	// and Player Start does not change the player's existing facing.
	private void ConfirmSandboxPlacement(Vector2 direction)
	{
		RemoveSandboxEnemyAt(_pendingPlacementCell);
		SpawnSandboxEnemy(_pendingPlacementCell, _pendingMonsterDefinition, direction);

		CloseSandboxPlacementMenu();
	}

	private void ConfirmSandboxDelete()
	{
		RemoveSandboxEnemyAt(_pendingPlacementCell);
		_dungeonMap.SetTerrain(_pendingPlacementCell.X, _pendingPlacementCell.Y, TerrainKind.Floor);
		_dungeonRenderer.RefreshCell(_dungeonMap, _pendingPlacementCell.X, _pendingPlacementCell.Y);
		CloseSandboxPlacementMenu();
	}

	private void SpawnSandboxEnemy(GridPosition cell, MonsterDefinition definition, Vector2 facing)
	{
		Enemy enemy = _enemyScene.Instantiate<Enemy>();
		enemy.Name = $"{definition.Id.Replace('.', '_')}{_enemies.Count + 1}";
		enemy.Configure(definition, cell, CellToPosition(cell), IsWallAt);
		_enemies.Add(enemy);
		_gameState.AddEnemy(enemy.State);
		AddChild(enemy);

		// Configure sets the movement behavior's default initial facing;
		// this must run after AddChild (so _Ready has already created the
		// facing indicator SetFacingDirection rotates) to apply the facing
		// chosen in the menu instead.
		((IEnemyMovementHost)enemy).SetFacingDirection(facing);
	}

	// RemoveDefeatedEnemies only drops dead actors (docs/DEBUG_SCENARIO_EDITOR.md's
	// "two things that will bite"), so deleting or overwriting a living
	// enemy - or placing a new one on an occupied cell - has to drop it from
	// both _enemies and GameState.Enemies explicitly.
	private void RemoveSandboxEnemyAt(GridPosition cell)
	{
		Enemy occupying = _enemies.Find(enemy => enemy.GridPosition == cell);

		if (occupying == null)
			return;

		_gameState.RemoveEnemy(occupying.State);
		_enemies.Remove(occupying);
		occupying.QueueFree();
	}

	// ESC closes whichever modal is already open (export menu first, then
	// pause menu) rather than stacking a second one on top, and otherwise
	// opens the pause menu.
	//
	// Inside Sandbox, ESC is deliberately not an exit: it closes the
	// placement menu if one is open, otherwise drops Play back to Edit.
	// Leaving Sandbox entirely is only ever the Exit button
	// (OnSandboxExitPressed) - see this session's follow-up to
	// docs/DEBUG_SCENARIO_EDITOR.md.
	private void OnEscapePressed()
	{
		if (_settingsOverlay.Visible)
		{
			CloseSettingsMenu();
			return;
		}

		if (_sandboxPlacementMenuOpen)
		{
			CloseSandboxPlacementMenu();
			return;
		}

		if (_inSandbox)
		{
			if (_sandbox.State == SandboxState.Play)
				EnterSandboxEditState();

			return;
		}

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

	// Shows the main menu on every launch so Settings is always reachable.
	// Continue is enabled only when a valid unfinished save exists; a save
	// whose run already ended (Won/Lost) has nothing left to resume.
	private void ShowStartupMenu()
	{
		bool hasResumableSave =
			(_pendingLoadOutcome.Result == SaveFileLoadResult.Loaded ||
				_pendingLoadOutcome.Result == SaveFileLoadResult.LoadedFromBackup) &&
			!_pendingLoadOutcome.Envelope.IsComplete;

		_hasUnfinishedResumableRun = hasResumableSave;

		_continueButton.Disabled = !hasResumableSave;
		_startupErrorLabel.Visible = _pendingLoadOutcome.Result == SaveFileLoadResult.LoadedFromBackup;

		if (_startupErrorLabel.Visible)
			_startupErrorLabel.Text = "Recovered from backup - the current save was invalid.";
		else if (_pendingLoadOutcome.Result == SaveFileLoadResult.Invalid)
		{
			_startupErrorLabel.Text = $"Save file could not be loaded: {_pendingLoadOutcome.Error}";
			_startupErrorLabel.Visible = true;
		}

		_startupMenuOverlay.Visible = true;

		if (hasResumableSave)
			_continueButton.GrabFocus();
		else
			_newRunButton.GrabFocus();
	}

	private void OpenSettingsMenu()
	{
		_displayModeOption.Select(_videoSettings.Fullscreen ? 1 : 0);

		int selectedSize = 0;
		for (int i = 0; i < VideoSettings.WindowSizes.Length; i++)
		{
			Vector2I size = VideoSettings.WindowSizes[i];
			if (size.X == _videoSettings.WindowWidth && size.Y == _videoSettings.WindowHeight)
			{
				selectedSize = i;
				break;
			}
		}

		_windowSizeOption.Select(selectedSize);
		_windowSizeOption.Disabled = _videoSettings.Fullscreen;
		_vsyncCheckButton.ButtonPressed = _videoSettings.VSyncEnabled;
		_settingsOverlay.Visible = true;
		_displayModeOption.GrabFocus();
	}

	private void CloseSettingsMenu()
	{
		_settingsOverlay.Visible = false;
		_settingsButton.GrabFocus();
	}

	private void OnDisplayModeSelected(long index)
	{
		_windowSizeOption.Disabled = index == 1;
		ApplyVideoSettings();
	}

	private void OnWindowSizeSelected(long index)
	{
		ApplyVideoSettings();
	}

	private void OnVsyncToggled(bool toggledOn)
	{
		ApplyVideoSettings();
	}

	private void ApplyVideoSettings()
	{
		Vector2I size = VideoSettings.WindowSizes[_windowSizeOption.Selected];
		_videoSettings.WindowWidth = size.X;
		_videoSettings.WindowHeight = size.Y;
		_videoSettings.Fullscreen = _displayModeOption.Selected == 1;
		_videoSettings.VSyncEnabled = _vsyncCheckButton.ButtonPressed;
		_videoSettings.Apply();

		Error saveError = _videoSettings.Save();
		if (saveError != Error.Ok)
			GD.PushError($"Could not save video settings: {saveError}.");
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

		// Compare against the current definitions' fingerprint before
		// touching any live state - a changed weapon/tool/monster shape
		// (not just a missing one) must also refuse Continue rather than
		// silently restoring against content that no longer matches what
		// was saved (NEXT_STEPS milestone 5).
		string currentFingerprint = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(weapon),
			ToolSummary.From(tool),
			enemyDefinitions.Select(MonsterSummary.From)
		);

		if (currentFingerprint != envelope.ContentFingerprint)
		{
			error = "Saved content has changed (weapon, tool, or monster definitions differ from when this run was saved).";
			return false;
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
			_confirmOverwriteCancelButton.GrabFocus();
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

	// Debug-only hand-built encounter for manually playtesting the War
	// Hammer/Charging Beetle/Spike Trap interaction (NEXT_STEPS roadmap
	// item 3), reachable only by pressing "F" on the startup screen -
	// entirely separate from DungeonGenerator/EncounterPlanner, per the
	// instruction to keep this out of procedural generation until the
	// interaction is proven. Autosave is suppressed (_suppressAutosave)
	// so testing it can never overwrite a real save.
	private void StartFixedEncounter()
	{
		_startupMenuOverlay.Visible = false;
		_suppressAutosave = true;

		const int width = 11;
		const int height = 7;

		_dungeonSeed = unchecked((int)_random.Randi());
		_dungeonMap = new DungeonMap(width, height, _dungeonSeed);

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				bool isBoundary = x == 0 || y == 0 || x == width - 1 || y == height - 1;
				_dungeonMap.SetTerrain(x, y, isBoundary ? TerrainKind.SolidWall : TerrainKind.Floor);
			}
		}

		// One spike trap between the player's and the beetle's starting
		// cells, with open floor on every side - enough room to melee the
		// beetle down directly, lure its charge across the spike, or
		// hammer-knock it onto the spike once adjacent.
		GridPosition spikePosition = new(5, 3);
		_dungeonMap.SetTerrain(spikePosition.X, spikePosition.Y, TerrainKind.SpikeTrap);

		BuildAndRenderDungeon();

		GridPosition playerStart = new(2, 3);
		_player.PlaceAt(playerStart, CellToPosition(playerStart));
		_player.EquipWeapon(WeaponDefinitions.WarHammer);
		UpdateWeaponDisplay(_player.Weapon);
		_toolLabel.Text = $"Tool: {_player.DiggingTool.Name}";
		_gameState = new GameState(_dungeonMap, _player.State);
		CreateFollowingCamera();

		GridPosition beetleStart = new(8, 3);
		Enemy beetle = _enemyScene.Instantiate<Enemy>();
		beetle.Name = "fixed_encounter_charging_beetle";
		beetle.Configure(
			MonsterDefinitions.ChargingBeetle,
			beetleStart,
			CellToPosition(beetleStart),
			IsWallAt
		);
		AddChild(beetle);
		_enemies.Add(beetle);
		_gameState.AddEnemy(beetle.State);

		_currentPlayerZoneId = -1;
		_gameStarted = true;
		_player.SetProcessUnhandledInput(true);

		_debugHistory = new DebugHistory(GameSnapshot.Capture(_gameState));

		GD.Print("Started fixed tactical encounter: War Hammer + Charging Beetle + Spike Trap.");
	}

	// Sandbox mode (docs/DEBUG_SCENARIO_EDITOR.md), reachable only from the
	// startup screen. A separate mode rather than a toggle over the active
	// run, entered into a blank hand-built room - completely isolated from
	// DungeonGenerator/EncounterPlanner and from the real save (autosave
	// suppressed for the whole session; exiting reloads the scene, so
	// nothing from a Sandbox session can survive into a normal run).
	private void EnterSandbox()
	{
		_startupMenuOverlay.Visible = false;
		_inSandbox = true;
		_suppressAutosave = true;

		_dungeonSeed = unchecked((int)_random.Randi());
		_dungeonMap = new DungeonMap(SandboxWidth, SandboxHeight, _dungeonSeed);

		for (int y = 0; y < SandboxHeight; y++)
		{
			for (int x = 0; x < SandboxWidth; x++)
			{
				bool isBoundary = x == 0 || y == 0 || x == SandboxWidth - 1 || y == SandboxHeight - 1;
				_dungeonMap.SetTerrain(x, y, isBoundary ? TerrainKind.SolidWall : TerrainKind.Floor);
			}
		}

		BuildAndRenderDungeon();

		GridPosition playerStart = new(SandboxWidth / 2, SandboxHeight / 2);
		_player.PlaceAt(playerStart, CellToPosition(playerStart));
		UpdateWeaponDisplay(_player.Weapon);
		_toolLabel.Text = $"Tool: {_player.DiggingTool.Name}";
		_gameState = new GameState(_dungeonMap, _player.State);
		CreateFollowingCamera();

		_enemies.Clear();
		_currentPlayerZoneId = -1;

		_sandbox = new ScenarioSandbox(SandboxWidth, SandboxHeight, playerStart);
		_sandboxInitialSnapshot = GameSnapshot.Capture(_gameState);
		_sandboxPlaySnapshot = null;
		_sandboxPlayEnemyDefinitions = null;

		_debugHistory = new DebugHistory(GameSnapshot.Capture(_gameState));

		EnterSandboxEditState();

		GD.Print("Entered Sandbox mode.");
	}

	private void OnDebugPressed()
	{
		EnterSandbox();
	}

	private void EnterSandboxEditState()
	{
		_sandbox.EnterEdit();
		_gameStarted = false;
		_player.SetProcessUnhandledInput(false);
		GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
		_sandboxCursorVisual.Visible = true;
		UpdateSandboxCursorVisual();
		UpdateSandboxToolbar();
	}

	// Shared by EnterSandboxPlayState and RestartSandbox - both end up with
	// input active and the game resolving normal turns, they differ only in
	// whether a fresh snapshot is taken first.
	private void ActivateSandboxPlayInput()
	{
		_sandbox.EnterPlay();
		_gameStarted = true;
		_gameEnded = false;
		_endGameOverlay.Visible = false;
		_player.SetProcessUnhandledInput(true);
		_sandboxCursorVisual.Visible = false;
		UpdateSandboxToolbar();
	}

	private void EnterSandboxPlayState()
	{
		_sandboxPlaySnapshot = GameSnapshot.Capture(_gameState);
		_sandboxPlayEnemyDefinitions = _enemies.Select(enemy => enemy.Definition).ToList();
		ActivateSandboxPlayInput();
	}

	private void OnSandboxRunPressed()
	{
		EnterSandboxPlayState();
	}

	private void OnSandboxRestartPressed()
	{
		RestartSandbox();
	}

	private void OnSandboxResetPressed()
	{
		ResetSandbox();
	}

	private void OnSandboxExitPressed()
	{
		ExitSandbox();
	}

	// Restores the snapshot taken when Play began (map, player, and every
	// enemy placed before Run was pressed) and resumes Play immediately, so
	// the same situation can be replayed without leaving Sandbox. Rebuilds
	// from the in-memory snapshot directly (no serialization, no
	// id-to-definition lookup, no fingerprint check) - enemies are restored
	// the same way RestoreRun does for Continue, using
	// _sandboxPlayEnemyDefinitions (captured alongside the snapshot) in
	// place of a save envelope's resolved definition list.
	private void RestartSandbox()
	{
		if (_sandboxPlaySnapshot == null)
			return;

		RestoreSandboxSnapshot(_sandboxPlaySnapshot, _sandboxPlayEnemyDefinitions);
		ActivateSandboxPlayInput();

		GD.Print("Sandbox restarted from the latest setup before Run.");
	}

	// Restores the blank room captured when Sandbox was first entered and
	// returns to Edit. This deliberately discards both edits and play state;
	// the next Run captures a new restart point from the rebuilt setup.
	private void ResetSandbox()
	{
		RestoreSandboxSnapshot(_sandboxInitialSnapshot, Array.Empty<MonsterDefinition>());
		_sandboxPlaySnapshot = null;
		_sandboxPlayEnemyDefinitions = null;
		EnterSandboxEditState();

		GD.Print("Sandbox reset to its initial state.");
	}

	private void RestoreSandboxSnapshot(
		GameSnapshot snapshot,
		IReadOnlyList<MonsterDefinition> enemyDefinitions)
	{

		_dungeonMap = GameSnapshotRestore.RestoreMap(snapshot.Grid, _dungeonSeed);
		BuildAndRenderDungeon();

		WeaponDefinition weapon = _player.Weapon;
		_player.Attack.Equip(weapon.PrimaryAttack);
		ActorState playerState = GameSnapshotRestore.RestoreActor(snapshot.Player, _player.Attack);
		_player.RestoreFrom(playerState, weapon, _player.DiggingTool, CellToPosition(snapshot.Player.Position));

		_gameState = new GameState(_dungeonMap, playerState);
		_gameState.RestoreTurnNumber(snapshot.TurnNumber);

		foreach (Enemy enemy in _enemies)
			enemy.QueueFree();

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

		_debugHistory = new DebugHistory(GameSnapshot.Capture(_gameState));
	}

	// Never writes anything - Sandbox has no real save to protect, so a
	// plain reload is enough. Whatever the player's actual save said before
	// Sandbox was entered is what ShowStartupMenu will see again.
	private void ExitSandbox()
	{
		GetTree().ReloadCurrentScene();
	}

	private void UpdateSandboxCursorVisual()
	{
		_sandboxCursorVisual.Position = CellToPosition(_sandbox.CursorPosition);
	}

	private void UpdateSandboxToolbar()
	{
		_sandboxToolbar.Visible = _inSandbox;

		bool isEdit = _sandbox.State == SandboxState.Edit;
		_sandboxModeLabel.Text = isEdit ? "SANDBOX - EDIT" : "SANDBOX - PLAY";
		_sandboxRunButton.Visible = isEdit;
		_sandboxRestartButton.Visible = !isEdit;
		_sandboxResetButton.Visible = true;
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

		UpdateWeaponDisplay(weapon);
		_toolLabel.Text = $"Tool: {tool.Name}";
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

			case PlayerActionKind.Waited:
				GD.Print("Player waits.");
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

		ResolveTurn(direction, outcome);
	}

	// The explicit Wait command: consumes a turn (enemies still act
	// afterward) without moving, attacking, or digging. Shares every other
	// step of turn resolution with OnPlayerMoveRequested via ResolveTurn -
	// one input here still produces exactly one completed turn, the same
	// guarantee movement already has.
	private void OnPlayerWaitRequested()
	{
		if (_gameEnded || !_gameStarted)
			return;

		ResolveTurn(Vector2.Zero, TurnResolver.ResolvePlayerWait());
	}

	// The shared remainder of turn resolution once the player's half is
	// already decided (move/attack/dig/wait) - narrate it, remove defeated
	// enemies, check for victory, then either stop at a terminal turn or
	// run the enemy phase before finishing.
	private void ResolveTurn(Vector2 direction, PlayerActionOutcome outcome)
	{
		ApplyPlayerActionOutcome(outcome);

		RemoveDefeatedEnemies();
		CheckForVictory();

		if (_gameEnded)
		{
			FinishTurn(direction, outcome, Array.Empty<EnemyActionOutcome>());
			return;
		}

		List<EnemyActionOutcome> enemyOutcomes = TakeEnemyTurns();

		RunEnvironmentPhase();

		RemoveDefeatedEnemies();
		CheckForVictory();
		FinishTurn(direction, outcome, enemyOutcomes);
	}

	// Spike traps tick and damage after enemy actions, before the turn's
	// snapshot/autosave (NEXT_STEPS roadmap item 3) - never on a turn that
	// already ended from the player's own action, since that branch returns
	// before enemies (and now the environment) ever act. Defeat caused by a
	// spike is caught by the RemoveDefeatedEnemies/CheckForVictory call
	// immediately after this, the same as defeat caused by an enemy.
	private void RunEnvironmentPhase()
	{
		IReadOnlyList<GridPosition> changedCells = EnvironmentPhaseResolver.ResolveSpikeTraps(
			_dungeonMap,
			GetCombatants()
		);

		foreach (GridPosition position in changedCells)
		{
			SpikeTrapPhase phase = _dungeonMap.GetCell(position.X, position.Y).SpikeTrapPhase;
			GD.Print($"Spike trap at {position} is now {phase}.");
			_dungeonRenderer.RefreshCell(_dungeonMap, position.X, position.Y);
		}
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
		// Sandbox scenarios routinely have zero enemies (a blank room, or one
		// being edited) - AreAllEnemiesDefeated would read that as an instant
		// win. Sandbox has no win condition in the editor, so skip the check.
		if (_inSandbox)
			return;

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

	private void OnPlayerDied()
	{
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

	private void OnWarHammerSelected()
	{
		SelectWeapon(WeaponDefinitions.WarHammer);
	}

	private void SelectWeapon(WeaponDefinition weapon)
	{
		_player.EquipWeapon(weapon);
		UpdateWeaponDisplay(_player.Weapon);
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
		// The debug fixed encounter (StartFixedEncounter) and Sandbox mode
		// must never overwrite a real save just from being played for
		// testing - set once on entry and never cleared, so no per-state
		// toggle can ever leave it wrong (docs/DEBUG_SCENARIO_EDITOR.md).
		if (_suppressAutosave)
			return;

		try
		{
			RunSaveEnvelope envelope = RunSaveEnvelope.Capture(
				_gameState,
				_player.Weapon,
				_player.DiggingTool,
				_enemies.Select(enemy => enemy.Definition)
			);

			RunSaveFileService.Save(OS.GetUserDataDir(), envelope);
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
