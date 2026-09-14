using Godot;

public sealed class DungeonRenderer
{
	private readonly Node2D _root;
	private readonly PackedScene _wallScene;
	private readonly Texture2D _floorTexture;
	private readonly Texture2D _floorCrackedTexture;
	private readonly Texture2D _wallHorizontalTexture;
	private readonly Texture2D _wallVerticalTexture;
	private readonly Texture2D _wallCornerLeftTexture;
	private readonly Texture2D _wallCornerRightTexture;
	private readonly Texture2D _wallBarsTexture;
	private readonly Texture2D _doorTexture;
	private readonly Vector2 _origin;
	private readonly float _tileSize;

	public DungeonRenderer(
		Node2D root,
		PackedScene wallScene,
		Texture2D floorTexture,
		Texture2D floorCrackedTexture,
		Texture2D wallHorizontalTexture,
		Texture2D wallVerticalTexture,
		Texture2D wallCornerLeftTexture,
		Texture2D wallCornerRightTexture,
		Texture2D wallBarsTexture,
		Texture2D doorTexture,
		Vector2 origin,
		float tileSize)
	{
		_root = root;
		_wallScene = wallScene;
		_floorTexture = floorTexture;
		_floorCrackedTexture = floorCrackedTexture;
		_wallHorizontalTexture = wallHorizontalTexture;
		_wallVerticalTexture = wallVerticalTexture;
		_wallCornerLeftTexture = wallCornerLeftTexture;
		_wallCornerRightTexture = wallCornerRightTexture;
		_wallBarsTexture = wallBarsTexture;
		_doorTexture = doorTexture;
		_origin = origin;
		_tileSize = tileSize;
	}

	public void Render(DungeonMap map)
	{
		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				DungeonTerrain terrain = map.GetCell(x, y).Terrain;

				if (terrain == DungeonTerrain.Floor ||
					terrain == DungeonTerrain.Door)
				{
					CreateFloor(x, y);
				}

				if (terrain == DungeonTerrain.Wall)
					CreateWall(map, x, y);
				else if (terrain == DungeonTerrain.Door)
					CreateDoor(x, y);
			}
		}
	}

	private Vector2 CellToPosition(int x, int y)
	{
		return _origin + new Vector2(x * _tileSize, y * _tileSize);
	}

	private void CreateFloor(int x, int y)
	{
		bool useCrackedFloor = (x * 7 + y * 11) % 9 == 0;

		Sprite2D floor = new()
		{
			Texture = useCrackedFloor
				? _floorCrackedTexture
				: _floorTexture,
			Position = CellToPosition(x, y),
			ZIndex = -10
		};
		_root.AddChild(floor);
	}

	private void CreateWall(DungeonMap map, int x, int y)
	{
		Node2D wall = _wallScene.Instantiate<Node2D>();
		wall.Position = CellToPosition(x, y);
		wall.GetNode<Sprite2D>("Sprite2D").Texture =
			GetWallTexture(map, x, y);
		wall.AddToGroup("walls");
		_root.AddChild(wall);
	}

	private void CreateDoor(int x, int y)
	{
		Sprite2D door = new()
		{
			Texture = _doorTexture,
			Position = CellToPosition(x, y),
			Offset = new Vector2(0, -8),
			ZIndex = -1
		};
		_root.AddChild(door);
	}

	private Texture2D GetWallTexture(DungeonMap map, int x, int y)
	{
		bool isLeft = x == 0;
		bool isRight = x == map.Width - 1;
		bool isTop = y == 0;
		bool isBottom = y == map.Height - 1;

		if ((isTop || isBottom) && isLeft)
			return _wallCornerLeftTexture;

		if ((isTop || isBottom) && isRight)
			return _wallCornerRightTexture;

		if (isTop && x % 3 == 1)
			return _wallBarsTexture;

		if (isTop || isBottom)
			return _wallHorizontalTexture;

		if (isLeft || isRight)
			return _wallVerticalTexture;

		bool continuesVertically = IsDivider(map, x, y - 1) ||
			IsDivider(map, x, y + 1);
		bool continuesHorizontally = IsDivider(map, x - 1, y) ||
			IsDivider(map, x + 1, y);

		return continuesVertically && !continuesHorizontally
			? _wallVerticalTexture
			: _wallHorizontalTexture;
	}

	private static bool IsDivider(DungeonMap map, int x, int y)
	{
		DungeonCell cell = map.GetCell(x, y);
		return cell != null &&
			(cell.Terrain == DungeonTerrain.Wall ||
				cell.Terrain == DungeonTerrain.Door);
	}
}
