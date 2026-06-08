using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Remote.Adb.Core.Settings;

/// <inheritdoc />
public sealed class SettingsStore : ISettingsStore
{
    // Enums are written as names (e.g. "Dark") so the file is human-readable and stable against enum
    // reordering. Reflection-based serialization keeps "add a setting" to a single edit on SettingsModel.
    // NOTE: if Core is ever trim/AOT-published, swap to a source-generated JsonSerializerContext.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly ILogger<SettingsStore> _logger;

    public SettingsStore(ILogger<SettingsStore> logger, string? filePath = null)
    {
        _logger = logger;
        _filePath = filePath ?? DefaultFilePath();
    }

    /// <summary>The default settings path: <c>{ApplicationData}/Remote.Adb/settings.json</c>.</summary>
    public static string DefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Remote.Adb", "settings.json");
    }

    /// <inheritdoc />
    public SettingsModel Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogDebug("No settings file at {Path}; using defaults.", _filePath);
                return new SettingsModel();
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<SettingsModel>(json, JsonOptions) ?? new SettingsModel();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogDebug(exception, "Could not read settings from {Path}; using defaults.", _filePath);
            return new SettingsModel();
        }
    }

    /// <inheritdoc />
    public void Save(SettingsModel settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);

            // Write to a sibling temp file then move it over the target, so a crash mid-write can never leave
            // a half-written settings.json (the move is atomic on a single volume).
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(exception, "Could not write settings to {Path}.", _filePath);
        }
    }
}
