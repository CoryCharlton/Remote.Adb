using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Desktop.Emulators;

/// <summary>
/// Warms the device-profile cache at startup, so the first time the create wizard opens the slow,
/// JVM-backed <c>avdmanager list device</c> result is already loaded. Best-effort: failures are logged and
/// the wizard falls back to loading on demand.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DeviceCatalogWarmup : BackgroundService
{
    private readonly ILogger<DeviceCatalogWarmup> _logger;
    private readonly IAvdProvisioningService _provisioning;

    public DeviceCatalogWarmup(IAvdProvisioningService provisioning, ILogger<DeviceCatalogWarmup> logger)
    {
        _provisioning = provisioning;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _provisioning.ListDevicesAsync(stoppingToken);
            _logger.LogDebug("Device catalog warm-up complete.");
        }
        catch (OperationCanceledException)
        {
            // Shutting down before the warm-up finished — nothing to do.
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Device catalog warm-up failed; the wizard will load it on demand.");
        }
    }
}
