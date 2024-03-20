using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Text;

public class SeStringConverter : JsonConverter<SeString>
{
    public override SeString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // For deserialization, you might want to convert the JSON string back to SeString.
        // This example assumes you're not deserializing SeString objects.
        throw new NotImplementedException("Deserialization is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, SeString value, JsonSerializerOptions options)
    {
        // Serialize the SeString object as a JSON string.
        try { writer.WriteStringValue(value.RawString); } catch (Exception) {
            writer.WriteNullValue();
        }
    }
}