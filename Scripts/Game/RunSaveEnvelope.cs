using System;
using System.Collections.Generic;
using System.Linq;

// The versioned wrapper around a GameSnapshot for milestone-4 save/resume,
// per docs/SAVE_AND_DEBUG_HISTORY.md's "Configuration" bucket: state
// (GameSnapshot) and generation configuration (Seed) are captured
// separately, since GameSnapshot's job is committed state, not how the map
// was originally generated. SchemaVersion lets a future loader reject a
// save it does not know how to read instead of guessing - bumped to 2 when
// ContentFingerprint was added, since an older save has no fingerprint to
// compare and should be rejected via the existing UnsupportedVersion path
// rather than treated as "content changed".
public sealed class RunSaveEnvelope
{
	public const int CurrentSchemaVersion = 2;

	public int SchemaVersion { get; }
	public int Seed { get; }
	public GameSnapshot Snapshot { get; }
	public DateTime SavedAtUtc { get; }

	// A fingerprint of the weapon/tool/enemy definitions this run actually
	// used (ContentFingerprinter.ComputeForRun) - not the entire game's
	// current content, so adding unrelated weapons/monsters elsewhere can
	// never invalidate an existing save (NEXT_STEPS milestone 5). Compared
	// against the current definitions' fingerprint before Continue touches
	// any live state.
	public string ContentFingerprint { get; }

	// Whether this save represents a finished run - derived from the
	// snapshot's own RunStatus rather than a second, independently-settable
	// flag that could drift from it.
	public bool IsComplete => Snapshot.Status != RunStatus.InProgress;

	public RunSaveEnvelope(
		int schemaVersion,
		int seed,
		GameSnapshot snapshot,
		DateTime savedAtUtc,
		string contentFingerprint)
	{
		SchemaVersion = schemaVersion;
		Seed = seed;
		Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
		SavedAtUtc = savedAtUtc;
		ContentFingerprint = contentFingerprint;
	}

	public static RunSaveEnvelope Capture(
		GameState state,
		WeaponDefinition weapon,
		DiggingToolDefinition tool,
		IEnumerable<MonsterDefinition> enemyDefinitions)
	{
		if (state == null)
			throw new ArgumentNullException(nameof(state));

		if (weapon == null)
			throw new ArgumentNullException(nameof(weapon));

		if (tool == null)
			throw new ArgumentNullException(nameof(tool));

		string fingerprint = ContentFingerprinter.ComputeForRun(
			WeaponSummary.From(weapon),
			ToolSummary.From(tool),
			(enemyDefinitions ?? Enumerable.Empty<MonsterDefinition>()).Select(MonsterSummary.From)
		);

		return new RunSaveEnvelope(
			CurrentSchemaVersion,
			state.Map.Seed,
			GameSnapshot.Capture(state),
			DateTime.UtcNow,
			fingerprint
		);
	}
}
