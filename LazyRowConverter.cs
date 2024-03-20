using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Data;
using Lumina.Excel;

public class SharedDepth
{
    public static int depth = 0;
};

public class LazyRowConverter<T> : JsonConverter<LazyRow<T>> where T : ExcelRow
{
    private const int MaxDepth = 2; // Set your maximum depth here

    public override LazyRow<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Implement deserialization logic here
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, LazyRow<T> value, JsonSerializerOptions options)
    {
        // Ensure thread-safe access to SerializationDepth
        lock (typeof(LazyRowConverter<T>))
        {
            SharedDepth.depth++;
            
            if (value.Row == 0)
                writer.WriteNullValue();
            else if (SharedDepth.depth > MaxDepth)
            {
                // Serialize as null and add an additional field with the ID of the target
                // Serialize the object normally
                JsonSerializer.Serialize(writer, value.RawRow, options);
            }
            else
            {
                // Serialize the object normally
                JsonSerializer.Serialize(writer, value.Value, options);
            }

            SharedDepth.depth--;
        }
    }
}
