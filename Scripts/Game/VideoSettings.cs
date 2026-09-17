using Godot;
using System;

public sealed class VideoSettings
{
	public const string FilePath = "user://video_settings.cfg";
	public const int DefaultWidth = 1280;
	public const int DefaultHeight = 720;

	public static readonly Vector2I[] WindowSizes =
	{
		new(960, 540),
		new(1280, 720),
		new(1600, 900),
		new(1920, 1080)
	};

	public int WindowWidth { get; set; } = DefaultWidth;
	public int WindowHeight { get; set; } = DefaultHeight;
	public bool Fullscreen { get; set; }
	public bool VSyncEnabled { get; set; } = true;

	public static VideoSettings Load(string path = FilePath)
	{
		VideoSettings settings = new();
		ConfigFile config = new();

		if (config.Load(path) != Error.Ok)
			return settings;

		int width = config.GetValue("video", "window_width", DefaultWidth).AsInt32();
		int height = config.GetValue("video", "window_height", DefaultHeight).AsInt32();

		if (Array.Exists(WindowSizes, size => size.X == width && size.Y == height))
		{
			settings.WindowWidth = width;
			settings.WindowHeight = height;
		}

		settings.Fullscreen = config.GetValue("video", "fullscreen", false).AsBool();
		settings.VSyncEnabled = config.GetValue("video", "vsync", true).AsBool();
		return settings;
	}

	public Error Save(string path = FilePath)
	{
		ConfigFile config = new();
		config.SetValue("video", "window_width", WindowWidth);
		config.SetValue("video", "window_height", WindowHeight);
		config.SetValue("video", "fullscreen", Fullscreen);
		config.SetValue("video", "vsync", VSyncEnabled);
		return config.Save(path);
	}

	public void Apply()
	{
		DisplayServer.WindowSetVsyncMode(
			VSyncEnabled
				? DisplayServer.VSyncMode.Enabled
				: DisplayServer.VSyncMode.Disabled
		);

		if (Fullscreen)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
			return;
		}

		DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		Vector2I size = new(WindowWidth, WindowHeight);
		DisplayServer.WindowSetSize(size);

		int screen = DisplayServer.WindowGetCurrentScreen();
		Rect2I usableRect = DisplayServer.ScreenGetUsableRect(screen);
		DisplayServer.WindowSetPosition(usableRect.Position + (usableRect.Size - size) / 2);
	}
}
