using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using PristonTale.Engine3D.Diagnostics;
using PristonTale.Engine3D.Rendering;

namespace PristonTale.Engine3D.IO;

public sealed class ClientSettingsStore
{
    // Use the same generated contract for desktop persistence and Android's initial settings.
    public static string Serialize(ClientSettings settings) => JsonSerializer.Serialize(settings, ClientSettingsJsonContext.Default.ClientSettings);

    private readonly string _path;
    private readonly IClientLog _log;
    private readonly Channel<ClientSettings> _pending = Channel.CreateBounded<ClientSettings>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly Task _writer;
    private string _lastError = string.Empty;
    private bool _readOnly;

    public ClientSettingsStore(string path, IClientLog log)
    {
        _path = path;
        _log = log;
        _writer = Task.Run(WriteLoopAsync);
    }

    // Beside the executable: the game keeps its settings with itself, never in a per-user profile folder.
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "client-settings.json");

    public string LastError => Volatile.Read(ref _lastError);

    // ETERVALE_H700_RESOLUTION_V1: tamanho real do painel H700, sem alterar o perfil PC.
    private ClientSettings H700Native(ClientSettings settings)
    {
        if (Environment.GetEnvironmentVariable("ETERVALE_H700_LITE") != "1")
        {
            return settings;
        }

        var result = settings with
        {
            WindowWidth = 640,
            WindowHeight = 480,
            WindowMode = WindowMode.Windowed,
            WindowX = null,
            WindowY = null,
            WindowMaximized = false
        };
        _log.Info("[H700 RES] Resolucao solicitada: 640x480; painel nativo 4:3.");
        return result;
    }

    public ClientSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                var defaults = new ClientSettings();
                QueueSave(defaults);
                return H700Native(defaults);
            }

            var settings = Deserialize(File.ReadAllBytes(_path));
            if (settings.Version != ClientSettings.CurrentVersion)
            {
                _readOnly = true;
                ReportError($"Unsupported settings version {settings.Version}; preserving {_path} and using defaults.");
                return H700Native(new ClientSettings());
            }

            return H700Native(settings.Validate(_log));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A malformed/unreadable document is retained for recovery. Explicit user changes must not destroy it.
            _readOnly = true;
            ReportError($"Cannot load {_path}; using defaults without overwriting the file: {exception.Message}");
            return H700Native(new ClientSettings());
        }
    }

    // .NET 10 source-generated construction of init-only members loses absent property initializers.
    // Supply only missing fields from the current defaults; keep explicit values and unknown fields for validation.
    private static ClientSettings Deserialize(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var input = document.RootElement;
        if (input.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Settings document must be an object.");
        }

        var defaults = JsonSerializer.SerializeToElement(new ClientSettings(), ClientSettingsJsonContext.Default.ClientSettings);
        using var merged = new MemoryStream();
        using (var writer = new Utf8JsonWriter(merged))
        {
            writer.WriteStartObject();
            foreach (var property in defaults.EnumerateObject())
            {
                if (!input.TryGetProperty(property.Name, out _))
                {
                    property.WriteTo(writer);
                }
            }

            foreach (var property in input.EnumerateObject())
            {
                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        merged.Position = 0;
        return JsonSerializer.Deserialize(merged, ClientSettingsJsonContext.Default.ClientSettings)
            ?? throw new JsonException("Settings document is null.");
    }

    public bool QueueSave(ClientSettings settings)
    {
        return !_readOnly && _pending.Writer.TryWrite(settings);
    }

    public Task CompleteAsync()
    {
        _pending.Writer.TryComplete();
        return _writer;
    }

    private async Task WriteLoopAsync()
    {
        await foreach (var settings in _pending.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
                Directory.CreateDirectory(directory);
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(stream, settings, ClientSettingsJsonContext.Default.ClientSettings).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }

                File.Move(temporary, _path, overwrite: true);
                Volatile.Write(ref _lastError, string.Empty);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                ReportError($"Cannot save {_path}: {exception.Message}");
            }
            finally
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _log.Warn($"[Settings] Cannot remove temporary file {temporary}: {exception.Message}");
                }
            }
        }
    }

    private void ReportError(string message)
    {
        Volatile.Write(ref _lastError, message);
        _log.Error($"[Settings] {message}");
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ClientSettings))]
internal sealed partial class ClientSettingsJsonContext : JsonSerializerContext
{
}
