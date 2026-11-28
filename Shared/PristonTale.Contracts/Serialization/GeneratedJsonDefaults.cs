using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PristonTale.Contracts.Serialization;

// Source-generated construction of init-only records can replace missing initializers with CLR defaults.
// Keep the original typed defaults and supplement only absent JSON members, recursively through authored objects.
// All contracts are generated; no reflection-based serializer or dynamically constructed model is needed.
public sealed class GeneratedJsonDefaults
{
    private sealed record Contract(Dictionary<string, Type> PropertyTypes, JsonProperty[] Defaults);
    private readonly Dictionary<Type, Contract> _contracts = new();

    public GeneratedJsonDefaults(JsonSerializerContext context, params object[] models)
    {
        foreach (var model in models)
        {
            var type = model.GetType();
            var info = context.GetTypeInfo(type) ?? throw new InvalidOperationException($"Missing generated JSON contract for {type}.");
            var properties = info.Properties.ToDictionary(static p => p.Name, static p => p.PropertyType, StringComparer.Ordinal);
            var required = info.Properties.Where(static p => p.IsRequired).Select(static p => p.Name).ToHashSet(StringComparer.Ordinal);
            var defaults = JsonSerializer.SerializeToElement(model, info).EnumerateObject()
                .Where(p => !required.Contains(p.Name) && NeedsDefault(p.Value)).ToArray();
            _contracts.Add(type, new Contract(properties, defaults));
        }
    }

    // Missing null, false and numeric zero already have the correct CLR value. Explicit JSON null/false/zero is
    // never replaced, even where the authored default is different.
    private static bool NeedsDefault(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.False => false,
        JsonValueKind.Number => value.GetDouble() != 0d,
        _ => true
    };

    public T? Deserialize<T>(JsonElement element, JsonTypeInfo<T> info)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, element, typeof(T));
        }

        stream.Position = 0;
        return JsonSerializer.Deserialize(stream, info);
    }

    private void Write(Utf8JsonWriter writer, JsonElement element, Type type)
    {
        if (element.ValueKind == JsonValueKind.Array && type.IsArray)
        {
            var itemType = type.GetElementType()!;
            if (itemType.IsArray || _contracts.ContainsKey(itemType))
            {
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    Write(writer, item, itemType);
                }

                writer.WriteEndArray();
                return;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object && _contracts.TryGetValue(type, out var contract))
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (contract.PropertyTypes.TryGetValue(property.Name, out var propertyType))
                {
                    Write(writer, property.Value, propertyType);
                }
                else
                {
                    property.Value.WriteTo(writer);
                }
            }

            foreach (var property in contract.Defaults)
            {
                if (!element.TryGetProperty(property.Name, out _))
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
            return;
        }

        element.WriteTo(writer);
    }
}
