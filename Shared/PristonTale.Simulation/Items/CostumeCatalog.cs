using System.Text.Json;

namespace PristonTale.Simulation.Items;

// Server authority, independent of visible equipment. No file configured means no permitted costumes.
public sealed class CostumeCatalog
{
    private readonly HashSet<string> _keys;
    private CostumeCatalog(HashSet<string> keys) => _keys = keys;

    public static CostumeCatalog Empty { get; } = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    public bool Contains(string key) => key is not null && _keys.Contains(key);

    public static CostumeCatalog FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("version").GetInt32() != 1)
            throw new InvalidDataException("Unsupported costume manifest version.");
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var costume in root.GetProperty("costumes").EnumerateArray())
        {
            var key = costume.GetProperty("itemKey").GetString();
            if (string.IsNullOrWhiteSpace(key) || key.StartsWith("CA", StringComparison.OrdinalIgnoreCase)
                || !ItemCodes.TryParse(key, out _) || !keys.Add(key))
                throw new InvalidDataException($"Invalid, disabled or duplicate costume: {key}");
            var bodies = costume.GetProperty("bodies");
            if (bodies.ValueKind != JsonValueKind.Object || !bodies.EnumerateObject().Any())
                throw new InvalidDataException($"Costume has no class models: {key}");
        }
        return new CostumeCatalog(keys);
    }

    public static CostumeCatalog Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Empty;
        if (!File.Exists(path)) throw new FileNotFoundException("Configured costume manifest is missing.", path);
        return FromJson(File.ReadAllText(path));
    }
}
