using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Data;
using Lumina.Excel;

public class LazySubrowConverter<T> : JsonConverter<SubrowRef<T>> where T : struct, IExcelSubrow<T>
{
    public override SubrowRef<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Implement deserialization logic here
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, SubrowRef<T> value, JsonSerializerOptions options)
    {
        // Ensure thread-safe access to SerializationDepth
        lock (typeof(LazySubrowConverter<T>))
        {
            if (value.RowId == 0 || value.RowId == 4294967295) // FIXME: WHy?
                writer.WriteNullValue();
            else {
                writer.WriteStartObject();
                writer.WriteNumber("RowId", value.RowId);
                writer.WriteNumber("SubrowId", 0);
                writer.WriteString("SheetName", typeof(T).Name);
                writer.WriteEndObject();
            }
        }
    }
}
