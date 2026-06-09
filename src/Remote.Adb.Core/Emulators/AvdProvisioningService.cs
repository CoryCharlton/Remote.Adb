using Microsoft.Extensions.Logging;
using Remote.Adb.Core.Common;

namespace Remote.Adb.Core.Emulators;

/// <inheritdoc />
public sealed class AvdProvisioningService : IAvdProvisioningService
{
    // avdmanager create avd prompts "Do you wish to create a custom hardware profile? [no]"; answering keeps
    // the device profile's hardware and lets the process exit instead of blocking on stdin.
    private const string DeclineCustomHardware = "no\n";

    private readonly IAndroidSdk _androidSdk;
    private readonly SemaphoreSlim _devicesLock = new(1, 1);
    private readonly ILogger<AvdProvisioningService> _logger;
    private readonly IProcessRunner _processRunner;
    private IReadOnlyList<DeviceProfile>? _cachedDevices;

    public AvdProvisioningService(IProcessRunner processRunner, IAndroidSdk androidSdk, ILogger<AvdProvisioningService> logger)
    {
        _processRunner = processRunner;
        _androidSdk = androidSdk;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AvdOperationResult> CreateAsync(string avdId, string systemImagePackage, string device, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating AVD {AvdId} from {Package} on {Device}", avdId, systemImagePackage, device);

        var result = await _processRunner.RunAsync(
            _androidSdk.AvdManagerPath,
            ["create", "avd", "-n", avdId, "-k", systemImagePackage, "-d", device],
            DeclineCustomHardware,
            cancellationToken);

        if (result.Success)
        {
            return AvdOperationResult.Ok;
        }

        var detail = DescribeFailure(result);
        _logger.LogWarning("Creating {AvdId} exited with code {ExitCode}: {Detail}", avdId, result.ExitCode, detail ?? "(no output)");

        return AvdOperationResult.Fail(detail);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string avdId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting AVD {AvdId}", avdId);

        var result = await _processRunner.RunAsync(
            _androidSdk.AvdManagerPath,
            ["delete", "avd", "-n", avdId],
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Deleting {AvdId} exited with code {ExitCode}: {Error}", avdId, result.ExitCode, result.StandardError);
        }

        return result.Success;
    }

    // avdmanager prints its failures (a missing JDK, an unknown device, a bad package) to stdout, not stderr,
    // so merge both streams to surface the real reason instead of an empty error.
    private static string? DescribeFailure(ProcessResult result)
    {
        var detail = string.Join(
            Environment.NewLine,
            new[] { result.StandardError, result.StandardOutput }
                .Where(stream => !string.IsNullOrWhiteSpace(stream))
                .Select(stream => stream.Trim()));

        return string.IsNullOrWhiteSpace(detail) ? null : detail;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SystemImagePackage>> ListInstalledImagesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SystemImageScanner.Scan(_androidSdk.SdkRoot));

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeviceProfile>> ListDevicesAsync(CancellationToken cancellationToken = default)
    {
        // avdmanager spins up a JVM and is slow (~seconds); the device catalog rarely changes within a
        // session, so cache the first successful result.
        if (_cachedDevices is not null)
        {
            return _cachedDevices;
        }

        // Serialize the lazy build so the startup warm-up and a user-triggered call don't both run the
        // expensive scan; the second waiter sees the cache populated by the first.
        await _devicesLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedDevices is not null)
            {
                return _cachedDevices;
            }

            // Prefer the rich device definitions (with screen specs + form factor) read straight from the
            // SDK — no JVM, and far more detail than avdmanager exposes.
            var fromDefinitions = DeviceDefinitionReader.Read(_androidSdk.SdkRoot);
            if (fromDefinitions.Count > 0)
            {
                _logger.LogDebug("Loaded {Count} device definitions from the SDK device XML.", fromDefinitions.Count);
                return _cachedDevices = fromDefinitions;
            }

            _logger.LogWarning("No SDK device definitions found; falling back to 'avdmanager list device'.");

            var result = await _processRunner.RunAsync(
                _androidSdk.AvdManagerPath,
                ["list", "device"],
                cancellationToken: cancellationToken);

            var devices = AvdManagerOutputParser.ParseDevices(result.StandardOutput);

            // avdmanager is a Java wrapper; without a JDK (Android Studio bundles one, the standalone
            // cmdline-tools don't) it exits non-zero with the reason on stderr and no parseable stdout.
            // Surface that instead of silently returning an empty list.
            if (!result.Success || devices.Count == 0)
            {
                // Don't cache a failure — a later retry (e.g. after the user fixes JAVA_HOME) should re-run.
                _logger.LogWarning(
                    "avdmanager list device produced no device profiles (exit {ExitCode}). stderr: {Error}",
                    result.ExitCode,
                    string.IsNullOrWhiteSpace(result.StandardError) ? "(none)" : result.StandardError.Trim());
                return devices;
            }

            return _cachedDevices = devices;
        }
        finally
        {
            _devicesLock.Release();
        }
    }
}
