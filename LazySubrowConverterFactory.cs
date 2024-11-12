using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Data;
using Lumina.Excel;

public class LazySubrowConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        // Check if the type is a generic type and matches SubrowRef<>
        if (typeToConvert.IsGenericType && 
            typeToConvert.GetGenericTypeDefinition() == typeof(SubrowRef<>))
        {
            // Get the type argument T
            Type argumentType = typeToConvert.GetGenericArguments()[0];

            // Check if T is a struct and implements IExcelRow<T>
            return argumentType.IsValueType && 
                argumentType.GetInterface(typeof(IExcelSubrow<>).FullName) != null;
        }

        return false;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        // Create a converter for the specific type
        Type converterType = typeof(LazySubrowConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]);
        return (JsonConverter)Activator.CreateInstance(converterType);
    }
}
