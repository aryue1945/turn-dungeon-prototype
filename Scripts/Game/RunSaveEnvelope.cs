using System;

// The versioned wrapper around a GameSnapshot for milestone-4 save/resume,
// per docs/SAVE_AND_DEBUG_HISTORY.md's "Configuration" bucket: state
// (GameSnapshot) and generation configuration (Seed) are captured
// separately, since GameSnapshot's job is committed state, not how the map
// was originally generated. SchemaVersion lets a future loader reject a
// save it does not know how to read instead of guessing.
public sealed class RunSaveEnvelope
{
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion { get; }
	public int Seed { get; }
	public GameSnapshot Snapshot { get; }

	public RunSaveEnvelope(int schemaVersion, int seed, GameSnapshot snapshot)
	{
		SchemaVersion = schemaVersion;
		Seed = seed;
		Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
	}

	public static RunSaveEnvelope Capture(GameState state)
	{
		if (state == null)
			throw new ArgumentNullException(nameof(state));

		return new RunSaveEnvelope(
			CurrentSchemaVersion,
			state.Map.Seed,
			GameSnapshot.Capture(state)
		);
	}
}
