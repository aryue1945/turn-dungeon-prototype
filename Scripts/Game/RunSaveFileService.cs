using System;
using System.IO;

// Disk I/O for the one current-run save slot plus its recovery backup, per
// docs/SAVE_AND_DEBUG_HISTORY.md's decision table: one current-run slot and
// a recovery backup; an invalid save is reported with a useful error and
// preserved rather than deleted, with recovery offered if the backup
// validates. Takes the save directory as a parameter rather than reading
// Godot's OS.GetUserDataDir() itself (mirroring MonsterModLoader taking a
// path parameter instead of a hard-coded res:// path), so it can be unit
// tested against a plain temp directory with no Godot engine running.
public static class RunSaveFileService
{
	public const string SaveFileName = "run_save.json";
	public const string BackupFileName = "run_save.backup.json";
	private const string TempFileName = "run_save.tmp.json";

	public static bool HasSaveFile(string directory) =>
		File.Exists(SavePath(directory));

	// Writes envelope as the new current save. The temp file is written and
	// validated before anything already on disk is touched, so a failure
	// here (a serialization bug, a full disk, whatever) never corrupts or
	// deletes an existing current/backup save - nothing is moved or
	// replaced until the new data is confirmed to round-trip. The
	// (unconditionally trusted) existing current save then rotates into the
	// single backup slot, and the validated temp file atomically becomes
	// the new current save.
	public static void Save(string directory, RunSaveEnvelope envelope)
	{
		if (envelope == null)
			throw new ArgumentNullException(nameof(envelope));

		Directory.CreateDirectory(directory);

		string savePath = SavePath(directory);
		string backupPath = BackupPath(directory);
		string tempPath = TempPath(directory);

		string json = RunSaveSerializer.ToJson(envelope);
		File.WriteAllText(tempPath, json);

		RunSaveLoadOutcome validation = RunSaveSerializer.FromJson(File.ReadAllText(tempPath));

		if (validation.Result != RunSaveLoadResult.Loaded)
		{
			throw new InvalidOperationException(
				"Refusing to save: the freshly written save file failed to " +
					$"validate ({validation.Error})."
			);
		}

		if (File.Exists(savePath))
			File.Copy(savePath, backupPath, overwrite: true);

		File.Move(tempPath, savePath, overwrite: true);
	}

	// Loads the current save, falling back to the backup if the current
	// save is missing or fails to validate and the backup itself validates.
	// Never deletes or overwrites either file - an invalid save stays on
	// disk exactly as found, for the caller to report and the player to
	// investigate or recover from manually.
	public static SaveFileLoadOutcome Load(string directory)
	{
		string savePath = SavePath(directory);

		if (!File.Exists(savePath))
			return SaveFileLoadOutcome.NoSaveFound();

		RunSaveLoadOutcome current = ReadAndValidate(savePath);

		if (current.Result == RunSaveLoadResult.Loaded)
			return SaveFileLoadOutcome.Loaded(current.Envelope);

		string backupPath = BackupPath(directory);

		if (File.Exists(backupPath))
		{
			RunSaveLoadOutcome backup = ReadAndValidate(backupPath);

			if (backup.Result == RunSaveLoadResult.Loaded)
				return SaveFileLoadOutcome.LoadedFromBackup(backup.Envelope, current.Error);
		}

		return SaveFileLoadOutcome.Invalid(current.Error);
	}

	private static RunSaveLoadOutcome ReadAndValidate(string path)
	{
		try
		{
			return RunSaveSerializer.FromJson(File.ReadAllText(path));
		}
		catch (IOException exception)
		{
			return RunSaveLoadOutcome.Invalid($"Could not read save file: {exception.Message}");
		}
	}

	private static string SavePath(string directory) => Path.Combine(directory, SaveFileName);
	private static string BackupPath(string directory) => Path.Combine(directory, BackupFileName);
	private static string TempPath(string directory) => Path.Combine(directory, TempFileName);
}
