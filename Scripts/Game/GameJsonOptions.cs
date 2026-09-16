using Godot;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

// Godot.Vector2 does not round-trip through System.Text.Json's default
// parameterized-constructor/property binding - reading one back silently
// produces (0,0) instead of throwing, which is worse than not supporting it
// at all. This converter reads/writes it explicitly as {"x":.., "y":..}.
public sealed class Vector2JsonConverter : JsonConverter<Vector2>
{
	public override Vector2 Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException("Expected an object for Vector2.");

		float x = 0f;
		float y = 0f;

		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			string propertyName = reader.GetString();
			reader.Read();

			if (string.Equals(propertyName, "x", StringComparison.OrdinalIgnoreCase))
				x = reader.GetSingle();
			else if (string.Equals(propertyName, "y", StringComparison.OrdinalIgnoreCase))
				y = reader.GetSingle();
		}

		return new Vector2(x, y);
	}

	public override void Write(
		Utf8JsonWriter writer,
		Vector2 value,
		JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WriteNumber("x", value.X);
		writer.WriteNumber("y", value.Y);
		writer.WriteEndObject();
	}
}

// Shared System.Text.Json configuration for Export Debug History and
// milestone-4 save/resume, so the two JSON shapes stay consistent instead
// of drifting apart.
public static class GameJsonOptions
{
	public static JsonSerializerOptions Create()
	{
		return new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters =
			{
				new JsonStringEnumConverter(),
				new Vector2JsonConverter()
			}
		};
	}
}
