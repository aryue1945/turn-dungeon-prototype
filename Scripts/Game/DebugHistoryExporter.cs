using System.Text.Json;
using System.Text.Json.Serialization;

// Builds the human/AI-readable JSON for Export Debug History
// (docs/SAVE_AND_DEBUG_HISTORY.md). Pure string-building only - writing it
// to a file is the caller's job (Main), so this stays testable without
// touching disk and reusable if export ever needs a different destination.
// Export must not consume a turn, alter state, or draw random numbers;
// serializing an already-captured DebugHistory can't do any of those.
public static class DebugHistoryExporter
{
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter() }
	};

	public static string ToJson(DebugHistory history)
	{
		return JsonSerializer.Serialize(history, Options);
	}
}
