using System;
using System.Collections.Generic;
using System.Text.Json;

// Builds the human/AI-readable JSON for Export Debug History
// (docs/SAVE_AND_DEBUG_HISTORY.md). Pure string-building only - writing it
// to a file is the caller's job (Main), so this stays testable without
// touching disk and reusable if export ever needs a different destination.
// Export must not consume a turn, alter state, or draw random numbers;
// serializing an already-captured DebugHistory can't do any of those.
public static class DebugHistoryExporter
{
	private static readonly JsonSerializerOptions Options = GameJsonOptions.Create();

	// Exports everything currently retained in the ring, with no identity/
	// content context. Kept for callers that want the full ring rather than
	// a menu-selected slice.
	public static string ToJson(DebugHistory history)
	{
		if (history == null)
			throw new ArgumentNullException(nameof(history));

		return JsonSerializer.Serialize(history, Options);
	}

	// Exports only the last transitionCount transitions plus the snapshot
	// immediately before the first of them, per the export-menu spec:
	// exporting turns 7..11 must contain state 6 followed by transitions
	// 7..11. transitionCount is clamped to what history actually retains.
	public static string ToJson(
		DebugHistory history,
		int transitionCount,
		DebugHistoryExportContext context)
	{
		if (history == null)
			throw new ArgumentNullException(nameof(history));

		if (context == null)
			throw new ArgumentNullException(nameof(context));

		GameSnapshot before = history.GetSnapshotBefore(transitionCount);
		IReadOnlyList<TurnTransition> transitions = history.GetLastTransitions(transitionCount);

		DebugHistoryExport export = new(
			DebugHistoryExport.CurrentSchemaVersion,
			context.RunId,
			context.Seed,
			context.FloorId,
			context.BuildVersion,
			context.ComputeContentFingerprint(),
			context.Weapons,
			context.Monsters,
			before,
			transitions
		);

		return JsonSerializer.Serialize(export, Options);
	}
}
