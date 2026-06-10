using Microsoft.Extensions.Logging;
using Remote.Adb.Core.Adb;
using Remote.Adb.Core.Common;

namespace Remote.Adb.Core.Emulators;

/// <inheritdoc />
public sealed class EmulatorService : IEmulatorService
{
    private const string EmulatorSerialPrefix = "emulator-";

    private readonly IAndroidSdk _androidSdk;
    private readonly IAvdCatalog _avdCatalog;
    private readonly ILogger<EmulatorService> _logger;
    private readonly IProcessRunner _processRunner;

    public EmulatorService(IProcessRunner processRunner, IAndroidSdk androidSdk, IAvdCatalog avdCatalog, ILogger<EmulatorService> logger)
    {
        _processRunner = processRunner;
        _androidSdk = androidSdk;
        _avdCatalog = avdCatalog;
        _logger = logger;
    }

    private static AndroidVirtualDevice CreateDevice(string name, bool isRunning, string? serial, IReadOnlyDictionary<string, AvdMetadata> catalog)
    {
        catalog.TryGetValue(name, out var metadata);
        var displayName = metadata?.DisplayName ?? name;
        return new AndroidVirtualDevice(name, displayName, metadata?.Tag, isRunning, serial, metadata?.ApiLevel, metadata?.Abi);
    }

    private async Task<string?> GetAvdNameAsync(string serial, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync(_androidSdk.AdbPath, ["-s", serial, "emu", "avd", "name"], cancellationToken: cancellationToken);
        return EmulatorOutputParser.ParseAvdName(result.StandardOutput);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AndroidVirtualDevice>> ListAsync(CancellationToken cancellationToken = default)
    {
        // The AVD list and the adb device list are independent, so spawn both processes concurrently.
        var avdTask = _processRunner.RunAsync(_androidSdk.EmulatorPath, ["-list-avds"], cancellationToken: cancellationToken);
        var devicesTask = _processRunner.RunAsync(_androidSdk.AdbPath, ["devices"], cancellationToken: cancellationToken);
        await Task.WhenAll(avdTask, devicesTask);

        var names = EmulatorOutputParser.ParseAvdList(avdTask.Result.StandardOutput);
        var runningSerials = AdbOutputParser.ParseDevices(devicesTask.Result.StandardOutput)
            .Where(serial => serial.StartsWith(EmulatorSerialPrefix, StringComparison.Ordinal));

        // Correlate each running emulator serial back to its AVD name via the emulator console.
        var serialsByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var serial in runningSerials)
        {
            var name = await GetAvdNameAsync(serial, cancellationToken);
            if (name is not null)
            {
                serialsByName[name] = serial;
            }
        }

        var catalog = _avdCatalog.Read();

        var devices = new List<AndroidVirtualDevice>();
        var knownNames = new HashSet<string>(names, StringComparer.Ordinal);

        foreach (var name in names)
        {
            serialsByName.TryGetValue(name, out var serial);
            devices.Add(CreateDevice(name, serial is not null, serial, catalog));
        }

        // A running emulator whose AVD no longer appears in -list-avds (e.g. deleted on disk).
        foreach (var (name, serial) in serialsByName)
        {
            if (!knownNames.Contains(name))
            {
                devices.Add(CreateDevice(name, true, serial, catalog));
            }
        }

        return devices;
    }

    /// <inheritdoc />
    public Task StartAsync(string avdName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting emulator {AvdName}", avdName);
        _processRunner.Start(_androidSdk.EmulatorPath, ["-avd", avdName]);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(string serial, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping emulator {Serial}", serial);
        var result = await _processRunner.RunAsync(_androidSdk.AdbPath, ["-s", serial, "emu", "kill"], cancellationToken: cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Stopping {Serial} exited with code {ExitCode}: {Error}", serial, result.ExitCode, result.StandardError);
        }
    }
}
