using System;
using System.IO;
using System.Linq;

// Where Sandbox scenarios live on disk (docs/DEBUG_SCENARIO_EDITOR.md): one
// directory per scenario under a dedicated "scenarios" subfolder, so
// RunSaveFileService's directory parameter never resolves to the real save
// slot no matter what name the player picks.
public static class ScenarioStorage
{
	private const string ScenariosFolderName = "scenarios";

	public static string RootDirectory(string userDataDir) =>
		Path.Combine(userDataDir, ScenariosFolderName);

	public static string DirectoryFor(string userDataDir, string scenarioName) =>
		Path.Combine(RootDirectory(userDataDir), scenarioName);

	// Only a save-bearing subdirectory counts as a scenario, so a stray
	// empty folder never shows up as a broken entry in the load list.
	public static string[] ListScenarios(string userDataDir)
	{
		string root = RootDirectory(userDataDir);

		if (!Directory.Exists(root))
			return Array.Empty<string>();

		return Directory.GetDirectories(root)
			.Select(Path.GetFileName)
			.Where(name => RunSaveFileService.HasSaveFile(Path.Combine(root, name)))
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToArray();
	}

	// Rejects anything that could escape the scenarios directory (path
	// separators, "..") or produce an invalid/empty folder name, rather than
	// trying to sanitize a hostile name into something safe.
	public static bool TryValidateScenarioName(string rawName, out string name, out string error)
	{
		name = (rawName ?? string.Empty).Trim();

		if (name.Length == 0)
		{
			error = "Enter a scenario name.";
			return false;
		}

		if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
			name.Contains("..", StringComparison.Ordinal))
		{
			error = "Scenario names can't contain path separators or \"..\".";
			return false;
		}

		error = null;
		return true;
	}
}
