using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Data;
using Lumina.Excel;
using System.Reflection;

public class LazyRowConverter : JsonConverter<RowRef>
{
    public override RowRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Implement deserialization logic here
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, RowRef value, JsonSerializerOptions options)
    {
        // Ensure thread-safe access to SerializationDepth
        lock (typeof(LazyRowConverter))
        {
            if (value.RowId == null || value.RowId == 4294967295) // Check for invalid RowId
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteNumber("RowId", value.RowId);

                // Use reflection to get the private Type field
                var actualType = value.GetType();
                FieldInfo typeField = actualType.GetField("<rowType>P", BindingFlags.NonPublic | BindingFlags.Instance);

                if (typeField != null)
                {
                    Type rowType = typeField.GetValue(value) as Type;

                    if (rowType != null)
                    {
                        writer.WriteString("SheetName", rowType.Name); // Use actual type name
                    }
                    else
                    {
                        writer.WriteString("SheetName", "Unknown");
                    }
                }
                else
                {
                    writer.WriteString("SheetName", "Unknown");
                }

                writer.WriteEndObject();
            }
        }
    }
}

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
            if (value.RowId == null || value.RowId == 4294967295) // Check for invalid RowId
                writer.WriteNullValue();
            else
            {
                writer.WriteStartObject();
                writer.WriteNumber("RowId", value.RowId);
                writer.WriteString("SheetName", typeof(T).Name);
                writer.WriteEndObject();
            }
        }
    }
}
