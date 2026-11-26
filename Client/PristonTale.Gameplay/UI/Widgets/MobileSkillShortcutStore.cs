using System.Text.Json;
using PristonTale.Simulation.Skills;

namespace PristonTale.Gameplay.UI.Widgets;

// One local profile per character name and class. Startup loads it before the game thread starts;
// saves capture an immutable byte snapshot and queue the disk work in order.
public sealed class MobileSkillShortcutStore
{
    private readonly string _path;
    private readonly Dictionary<string, HudSkillShortcut[]> _profiles = new(StringComparer.Ordinal);
    private Task _pendingWrite = Task.CompletedTask;

    internal static MobileSkillShortcutStore Current { get; private set; }

    public static void Configure(string settingsDirectory)
    {
        var path = Path.Combine(settingsDirectory, "mobile-skill-shortcuts.json");
        if (Current is null || !string.Equals(Current._path, path, StringComparison.Ordinal))
        {
            Current = new MobileSkillShortcutStore(path);
        }
    }

    internal MobileSkillShortcutStore(string path)
    {
        _path = path;
        try
        {
            if (!File.Exists(path))
            {
                return;
            }
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }
            foreach (var profile in document.RootElement.EnumerateObject())
            {
                if (profile.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                var slots = new HudSkillShortcut[HudSkillBindings.ShortcutCount];
                foreach (var entry in profile.Value.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object
                        || !entry.TryGetProperty("index", out var index) || index.ValueKind != JsonValueKind.Number
                        || !index.TryGetInt32(out var slot) || slot < 0 || slot >= slots.Length
                        || !entry.TryGetProperty("skillId", out var id) || id.ValueKind != JsonValueKind.Number
                        || !id.TryGetUInt32(out var skillId) || skillId == 0
                        || !entry.TryGetProperty("side", out var side) || side.ValueKind != JsonValueKind.String
                        || !Enum.TryParse<SkillMouseUse>(side.GetString(), out var mouseSide)
                        || mouseSide is not (SkillMouseUse.Left or SkillMouseUse.Right))
                    {
                        continue;
                    }
                    slots[slot] = new HudSkillShortcut(skillId, mouseSide);
                }
                _profiles[profile.Name] = slots;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"Mobile skill shortcuts could not be loaded: {error.Message}");
        }
    }

    internal void Restore(string character, HudSkillBindings bindings)
    {
        if (!_profiles.TryGetValue(character, out var slots))
        {
            return;
        }
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i].SkillId != 0)
            {
                // Assign checks learned rank and permitted side, and removes duplicate skill assignments.
                bindings.Assign(i, slots[i].SkillId, slots[i].Side);
            }
        }
    }

    internal Task SaveAsync(string character, HudSkillBindings bindings)
    {
        var slots = new HudSkillShortcut[HudSkillBindings.ShortcutCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = bindings.Shortcut(i);
        }
        _profiles[character] = slots;
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var profile in _profiles)
            {
                writer.WriteStartArray(profile.Key);
                for (var i = 0; i < profile.Value.Length; i++)
                {
                    var shortcut = profile.Value[i];
                    if (shortcut.SkillId == 0)
                    {
                        continue;
                    }
                    writer.WriteStartObject();
                    writer.WriteNumber("index", i);
                    writer.WriteNumber("skillId", shortcut.SkillId);
                    writer.WriteString("side", shortcut.Side.ToString());
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        var snapshot = buffer.ToArray();
        var previous = _pendingWrite;
        _pendingWrite = Task.Run(async () =>
        {
            await previous.ConfigureAwait(false);
            try
            {
                var temporary = _path + ".tmp";
                File.WriteAllBytes(temporary, snapshot);
                File.Move(temporary, _path, overwrite: true);
                Console.WriteLine("Mobile skill shortcuts saved.");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Mobile skill shortcuts could not be saved: {error.Message}");
            }
        });
        return _pendingWrite;
    }
}
