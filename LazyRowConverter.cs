using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Data;
using Lumina.Excel;

public class LazyRowConverter<T> : JsonConverter<LazyRow<T>> where T : ExcelRow
{
    private static readonly AsyncLocal<int> SerializationDepth = new AsyncLocal<int>();
    private const int MaxDepth = 5; // Set your maximum depth here

    public override LazyRow<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Implement deserialization logic here
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, LazyRow<T> value, JsonSerializerOptions options)
    {
        SerializationDepth.Value++;

        if (SerializationDepth.Value > MaxDepth)
        {
            // Serialize as null and add an additional field with the ID of the target
            writer.WriteNullValue();
        }
        else
        {
            // Serialize the object normally
            JsonSerializer.Serialize(writer, value, options);
        }

        SerializationDepth.Value--;
    }
}
