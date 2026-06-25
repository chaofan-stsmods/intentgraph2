using IntentGraph2.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntentGraph2.Utils.JsonConverters;
public class SecondaryInitialStateJsonConverter : JsonConverter<SecondaryInitialState>
{
    public override SecondaryInitialState? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string stateId = reader.GetString()!;
            return new SecondaryInitialState(stateId);
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("id", out var stateIdElement))
            {
                throw new JsonException($"Missing required property: {nameof(SecondaryInitialState.Id)}");
            }

            var stateId = stateIdElement.GetString();
            if (string.IsNullOrEmpty(stateId))
            {
                throw new JsonException($"Invalid value for property: {nameof(SecondaryInitialState.Id)}");
            }

            Position offset = default;
            if (root.TryGetProperty("offset", out var offsetElement))
            {
                offset = JsonSerializer.Deserialize<Position>(offsetElement.GetRawText(), options);
            }

            return new SecondaryInitialState(stateId, offset);
        }
        else
        {
            throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, SecondaryInitialState value, JsonSerializerOptions options)
    {
        if (value.Offset == default)
        {
            writer.WriteStringValue(value.Id);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WritePropertyName("offset");
        JsonSerializer.Serialize(writer, value.Offset, options);
        writer.WriteEndObject();
    }
}
