using Godot;

// Resolves a MonsterDefinition.SpritePath to a Texture2D. Built-in monsters
// use an imported "res://" path (GD.Load). Mod-loaded monsters point at a
// plain filesystem path under the mod's sprites/ folder, which isn't a
// project-imported resource, so it's read as raw image bytes instead.
public static class MonsterSpriteLoader
{
	public static Texture2D Load(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return null;

		if (path.StartsWith("res://"))
			return GD.Load<Texture2D>(path);

		Image image = new();
		Error error = image.Load(path);

		if (error != Error.Ok)
		{
			GD.PushError($"Failed to load monster sprite \"{path}\": {error}.");
			return null;
		}

		return ImageTexture.CreateFromImage(image);
	}
}
