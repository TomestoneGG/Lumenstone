using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Data;
using Lumina.Excel;

public class LazyRowConverter<T> : JsonConverter<LazyRow<T>> where T : ExcelRow
{
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
            if (value.Row == 0)
                writer.WriteNullValue();
            else
                JsonSerializer.Serialize(writer, value.RawRow, options);
        }
    }
}
