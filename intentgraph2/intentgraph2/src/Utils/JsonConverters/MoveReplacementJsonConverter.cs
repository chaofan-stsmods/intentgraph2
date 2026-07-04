using IntentGraph2.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntentGraph2.Utils.JsonConverters;
public class MoveReplacementJsonConverter : JsonConverter<MoveReplacement>
{
    public override MoveReplacement? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var intentOverrides = JsonSerializer.Deserialize<IntentOverride[]>(ref reader, options);
            return new MoveReplacement(intentOverrides, null);
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            IntentOverride[]? intentOverrides = null;
            if (root.TryGetProperty("intentOverrides", out var intentOverridesElement))
            {
                intentOverrides = JsonSerializer.Deserialize<IntentOverride[]>(intentOverridesElement, options);
            }

            ArrowOverride? arrowOverride = null;
            if (root.TryGetProperty("arrowOverride", out var arrowOverrideElement))
            {
                arrowOverride = JsonSerializer.Deserialize<ArrowOverride>(arrowOverrideElement, options);
            }

            return new MoveReplacement(intentOverrides, arrowOverride);
        }
        else
        {
            throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, MoveReplacement value, JsonSerializerOptions options)
    {
        if (value.ArrowOverride == null && value.IntentOverrides != null)
        {
            JsonSerializer.Serialize(writer, value.IntentOverrides, options);
            return;
        }

        writer.WriteStartObject();
        if (value.IntentOverrides != null)
        {
            writer.WritePropertyName("intentOverrides");
            JsonSerializer.Serialize(writer, value.IntentOverrides, options);
        }
        if (value.ArrowOverride != null)
        {
            writer.WritePropertyName("arrowOverride");
            JsonSerializer.Serialize(writer, value.ArrowOverride, options);
        }
        writer.WriteEndObject();
    }
}
