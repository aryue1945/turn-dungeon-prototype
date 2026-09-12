using Godot;

public partial class Main : Node2D
{
	private const float TileSize = 32.0f;
	private const int RoomWidth = 15;
	private const int RoomHeight = 11;

	private static readonly Vector2 RoomOrigin = new(64, 64);

	private readonly RandomNumberGenerator _random = new();

	private Player _player;
	private Enemy _enemy;

	private PackedScene _enemyScene;
	private PackedScene _wallScene;

	public override void _Ready()
	{
		_player = GetNode<Player>("Player");

		_enemyScene =
			GD.Load<PackedScene>("res://Enemy.tscn");

		_wallScene =
			GD.Load<PackedScene>("res://Wall.tscn");

		_random.Randomize();

		CreateRoom();
		PlacePlayerInCenter();
		SpawnEnemy();

		_player.MoveRequested += OnPlayerMoveRequested;
	}

	private Vector2 CellToPosition(int x, int y)
	{
		return RoomOrigin + new Vector2(
			x * TileSize,
			y * TileSize
		);
	}

	private void CreateRoom()
	{
		// Top and bottom walls
		for (int x = 0; x < RoomWidth; x++)
		{
			CreateWall(x, 0);
			CreateWall(x, RoomHeight - 1);
		}

		// Left and right walls
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

	private void SpawnEnemy()
	{
		int playerX = RoomWidth / 2;
		int playerY = RoomHeight / 2;

		int enemyX;
		int enemyY;

		do
		{
			enemyX = _random.RandiRange(1, RoomWidth - 2);
			enemyY = _random.RandiRange(1, RoomHeight - 2);
		}
		while (enemyX == playerX && enemyY == playerY);

		_enemy = _enemyScene.Instantiate<Enemy>();
		_enemy.Name = "Enemy";
		_enemy.Position = CellToPosition(enemyX, enemyY);

		AddChild(_enemy);
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

	private void OnPlayerMoveRequested(Vector2 direction)
	{
		Vector2 targetPosition =
			_player.Position + direction * TileSize;

		bool enemyExists =
			IsInstanceValid(_enemy) &&
			!_enemy.IsQueuedForDeletion();

		bool playerAttacked = false;
		bool turnTaken = false;

		if (enemyExists &&
			targetPosition.IsEqualApprox(_enemy.Position))
		{
			_enemy.TakeDamage(1);
			playerAttacked = true;
			turnTaken = true;
		}
		else if (!IsWallAt(targetPosition))
		{
			_player.Move(direction);
			turnTaken = true;
		}
		else
		{
			GD.Print($"Player hit wall at {targetPosition}");
		}

		if (turnTaken &&
			!playerAttacked &&
			IsInstanceValid(_enemy) &&
			!_enemy.IsQueuedForDeletion())
		{
			_enemy.TakeTurn(_player.Position);
		}
	}
}
