using System;
using System.Text.Json;

public enum RunSaveLoadResult
{
	Loaded,
	UnsupportedVersion,
	Invalid
}

public sealed class RunSaveLoadOutcome
{
	public RunSaveLoadResult Result { get; }
	public RunSaveEnvelope Envelope { get; }
	public string Error { get; }

	private RunSaveLoadOutcome(
		RunSaveLoadResult result,
		RunSaveEnvelope envelope,
		string error)
	{
		Result = result;
		Envelope = envelope;
		Error = error;
	}

	public static RunSaveLoadOutcome Loaded(RunSaveEnvelope envelope) =>
		new(RunSaveLoadResult.Loaded, envelope, null);

	public static RunSaveLoadOutcome UnsupportedVersion(int schemaVersion) =>
		new(
			RunSaveLoadResult.UnsupportedVersion,
			null,
			$"Save schema version {schemaVersion} is not supported " +
				$"(expected {RunSaveEnvelope.CurrentSchemaVersion})."
		);

	public static RunSaveLoadOutcome Invalid(string error) =>
		new(RunSaveLoadResult.Invalid, null, error);
}

// Serializes/deserializes a RunSaveEnvelope for milestone-4 save/resume.
// Reuses System.Text.Json's parameterized-constructor deserialization -
// GameSnapshot/ActorSnapshot/CellSnapshot/GridSnapshot each have exactly
// one public constructor whose parameters already match their properties,
// so no separate save-DTO layer is needed. Rejects an unreadable or
// wrong-version save instead of guessing, per
// docs/SAVE_AND_DEBUG_HISTORY.md - "reject unsupported versions, invalid
// bounds/IDs... with a useful error."
public static class RunSaveSerializer
{
	private static readonly JsonSerializerOptions Options = GameJsonOptions.Create();

	public static string ToJson(RunSaveEnvelope envelope)
	{
		if (envelope == null)
			throw new ArgumentNullException(nameof(envelope));

		return JsonSerializer.Serialize(envelope, Options);
	}

	public static RunSaveLoadOutcome FromJson(string json)
	{
		RunSaveEnvelope envelope;

		try
		{
			envelope = JsonSerializer.Deserialize<RunSaveEnvelope>(json, Options);
		}
		catch (JsonException exception)
		{
			return RunSaveLoadOutcome.Invalid($"Save file is not valid JSON: {exception.Message}");
		}

		if (envelope == null)
			return RunSaveLoadOutcome.Invalid("Save file is empty.");

		if (envelope.SchemaVersion != RunSaveEnvelope.CurrentSchemaVersion)
			return RunSaveLoadOutcome.UnsupportedVersion(envelope.SchemaVersion);

		if (envelope.Snapshot == null)
			return RunSaveLoadOutcome.Invalid("Save file has no snapshot.");

		return RunSaveLoadOutcome.Loaded(envelope);
	}
}
