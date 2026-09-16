public enum SaveFileLoadResult
{
	NoSaveFound,
	Loaded,
	LoadedFromBackup,
	Invalid
}

// What RunSaveFileService.Load found on disk: nothing, a good current
// save, a good backup used because the current save was missing/invalid,
// or nothing usable at all. Mirrors RunSaveLoadOutcome's typed-outcome
// shape one level up, at the file/backup layer instead of the JSON layer.
public sealed class SaveFileLoadOutcome
{
	public SaveFileLoadResult Result { get; }
	public RunSaveEnvelope Envelope { get; }
	public string Error { get; }

	private SaveFileLoadOutcome(
		SaveFileLoadResult result,
		RunSaveEnvelope envelope,
		string error)
	{
		Result = result;
		Envelope = envelope;
		Error = error;
	}

	public static SaveFileLoadOutcome NoSaveFound() =>
		new(SaveFileLoadResult.NoSaveFound, null, null);

	public static SaveFileLoadOutcome Loaded(RunSaveEnvelope envelope) =>
		new(SaveFileLoadResult.Loaded, envelope, null);

	// currentSaveError is the reason the current save was rejected, kept
	// around so the caller can still surface it even though the backup
	// recovered a playable envelope.
	public static SaveFileLoadOutcome LoadedFromBackup(
		RunSaveEnvelope envelope,
		string currentSaveError) =>
		new(SaveFileLoadResult.LoadedFromBackup, envelope, currentSaveError);

	public static SaveFileLoadOutcome Invalid(string error) =>
		new(SaveFileLoadResult.Invalid, null, error);
}
