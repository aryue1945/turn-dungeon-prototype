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
		EnemyMovementType.Chaser,
		EnemyMovementType.SlowChaser,
		EnemyMovementType.Patroller,
		EnemyMovementType.Stationary
	};

	private readonly RandomNumberGenerator _random = new();
	private readonly List<Enemy> _enemies = new();

	private Player _player;
	private Label _healthLabel;
	private Label _statusLabel;
	private Button _restartButton;
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
		PlacePlayerInCenter();
		SpawnEnemies();
		CreateGameUi();

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

			if (movementType == EnemyMovementType.Chaser)
				enemy.PrepareNextMove(_player.Position);
		}
	}

	private void CreateGameUi()
	{
		CanvasLayer canvasLayer = new();
		AddChild(canvasLayer);

		_healthLabel = new Label
		{
			Position = new Vector2(16, 16),
			Text = $"HP: {_player.Health}"
		};
		_healthLabel.AddThemeFontSizeOverride("font_size", 24);
		_healthLabel.AddThemeColorOverride("font_color", Colors.White);
		canvasLayer.AddChild(_healthLabel);

		_statusLabel = new Label
		{
			Position = new Vector2(188, 176),
			Size = new Vector2(200, 40),
			HorizontalAlignment = Godot.HorizontalAlignment.Center
		};
		_statusLabel.AddThemeFontSizeOverride("font_size", 28);
		canvasLayer.AddChild(_statusLabel);

		_restartButton = new Button
		{
			Position = new Vector2(228, 224),
			Size = new Vector2(120, 40),
			Text = "Restart",
			Visible = false
		};
		_restartButton.Pressed += OnRestartPressed;
		canvasLayer.AddChild(_restartButton);
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

	private Enemy FindEnemyAt(Vector2 position)
	{
		foreach (Enemy enemy in _enemies)
		{
			if (IsEnemyActive(enemy) &&
				enemy.Position.IsEqualApprox(position))
			{
				return enemy;
			}
		}

		return null;
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

	private void TakeEnemyTurns(Enemy attackedEnemy)
	{
		foreach (Enemy enemy in _enemies)
		{
			if (!IsEnemyActive(enemy) || enemy == attackedEnemy)
				continue;

			HashSet<Vector2> occupiedPositions =
				GetOccupiedEnemyPositions(enemy);

			enemy.TakeTurn(
				_player,
				occupiedPositions
			);

			if (_player.Health <= 0)
				break;
		}
	}

	private void OnPlayerMoveRequested(Vector2 direction)
	{
		if (_gameEnded)
			return;

		Vector2 targetPosition =
			_player.Position + direction * TileSize;

		Enemy attackedEnemy = FindEnemyAt(targetPosition);

		if (attackedEnemy != null)
		{
			attackedEnemy.TakeDamage(1);
		}
		else if (!IsWallAt(targetPosition))
		{
			_player.Move(direction);
		}
		else
		{
			GD.Print($"Player hit wall at {targetPosition}");
		}

		RemoveDefeatedEnemies();
		CheckForVictory();

		if (_gameEnded)
			return;

		TakeEnemyTurns(attackedEnemy);

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
		_restartButton.Visible = true;

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

	private void OnRestartPressed()
	{
		GetTree().ReloadCurrentScene();
	}
}
