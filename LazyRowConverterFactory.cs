using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina.Excel;

public class LazyRowConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        // Check if the type is a generic type and matches RowRef<> or SubrowRef<>
        if (typeToConvert.IsGenericType && 
            (typeToConvert.GetGenericTypeDefinition() == typeof(RowRef<>)))
        {
            return true;
        }

        // Check if the type is RowRef without a generic type
        if (typeToConvert == typeof(RowRef))
        {
            return true;
        }

        return false;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        // Handle the case for RowRef without a specific type
        if (typeToConvert == typeof(RowRef))
        {
            return new LazyRowConverter();
        }

        // Create a converter for the specific generic type
        var genericType = typeToConvert.GetGenericArguments()[0];

        // Ensure that the generic type satisfies the constraints
        if (!genericType.IsValueType || !typeof(IExcelRow<>).MakeGenericType(genericType).IsAssignableFrom(genericType))
        {
            throw new ArgumentException($"Type {genericType} does not satisfy the constraints for LazyRowConverter<T>.");
        }

        var converterType = typeof(LazyRowConverter<>).MakeGenericType(genericType);
        return (JsonConverter)Activator.CreateInstance(converterType);
    }
}