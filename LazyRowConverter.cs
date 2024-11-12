using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Data;
using Lumina.Excel;

public class LazyRowConverter<T> : JsonConverter<RowRef<T>> where T : struct, IExcelRow<T>
{
    public override RowRef<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Implement deserialization logic here
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, RowRef<T> value, JsonSerializerOptions options)
    {
        // Ensure thread-safe access to SerializationDepth
        lock (typeof(LazyRowConverter<T>))
        {
            if (value.RowId == 0 || value.RowId == 4294967295) // FIXME: WHy?
                writer.WriteNullValue();
            else {
                writer.WriteStartObject();
                writer.WriteNumber("RowId", value.RowId);
                writer.WriteString("SheetName", typeof(T).Name);
                writer.WriteEndObject();
            }
        }
    }
}
