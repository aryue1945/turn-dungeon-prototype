using Godot;
using System.Collections.Generic;

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
	private readonly Dictionary<GridPosition, List<Node2D>> _cellNodes = new();

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
				RenderCell(map, x, y);
		}
	}

	public void RefreshCell(DungeonMap map, int x, int y)
	{
		ClearCell(new GridPosition(x, y));
		RenderCell(map, x, y);
	}

	private void RenderCell(DungeonMap map, int x, int y)
	{
		DungeonCell cell = map.GetCell(x, y);

		if (cell == null || cell.Terrain.Kind == TerrainKind.Empty)
			return;

		GridPosition position = new(x, y);

		if (cell.IsWalkable)
			Track(position, CreateFloor(x, y));

		if (cell.Terrain.Kind == TerrainKind.SolidWall ||
			cell.Terrain.Kind == TerrainKind.BreakableWall)
		{
			Track(position, CreateWall(map, x, y));
		}
		else if (cell.Terrain.Kind == TerrainKind.Door)
			Track(position, CreateDoor(x, y));
	}

	private void Track(GridPosition position, Node2D node)
	{
		if (!_cellNodes.TryGetValue(position, out List<Node2D> nodes))
		{
			nodes = new List<Node2D>();
			_cellNodes[position] = nodes;
		}

		nodes.Add(node);
	}

	private void ClearCell(GridPosition position)
	{
		if (!_cellNodes.Remove(position, out List<Node2D> nodes))
			return;

		foreach (Node2D node in nodes)
		{
			if (GodotObject.IsInstanceValid(node))
				node.QueueFree();
		}
	}

	private Vector2 CellToPosition(int x, int y)
	{
		return _origin + new Vector2(x * _tileSize, y * _tileSize);
	}

	private Node2D CreateFloor(int x, int y)
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
		return floor;
	}

	private Node2D CreateWall(DungeonMap map, int x, int y)
	{
		Node2D wall = _wallScene.Instantiate<Node2D>();
		wall.Position = CellToPosition(x, y);
		wall.GetNode<Sprite2D>("Sprite2D").Texture =
			GetWallTexture(map, x, y);
		wall.AddToGroup("walls");
		_root.AddChild(wall);
		return wall;
	}

	private Node2D CreateDoor(int x, int y)
	{
		Sprite2D door = new()
		{
			Texture = _doorTexture,
			Position = CellToPosition(x, y),
			Offset = new Vector2(0, -8),
			ZIndex = -1
		};
		_root.AddChild(door);
		return door;
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
			(cell.Terrain.Kind == TerrainKind.SolidWall ||
				cell.Terrain.Kind == TerrainKind.BreakableWall ||
				cell.Terrain.Kind == TerrainKind.Door);
	}
}
