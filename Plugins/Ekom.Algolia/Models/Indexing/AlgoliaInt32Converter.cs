using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ekom.Algolia.Models.Indexing;

internal sealed class AlgoliaInt32Converter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.True => 1,
            JsonTokenType.False => 0,
            JsonTokenType.String => ReadString(reader.GetString()),
            _ => 0
        };
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);

    private static int ReadString(string? value)
    {
        if (int.TryParse(value, out var intValue))
            return intValue;

        if (bool.TryParse(value, out var boolValue))
            return boolValue ? 1 : 0;

        return 0;
    }
}
