using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remote.Adb.Core.Settings;
using Remote.Adb.Core.Tunnel;

namespace Remote.Adb.Desktop.Tunnel;

/// <summary>
/// Opens the tunnel once at app startup when auto-connect is enabled and a host is configured — distinct from
/// the page-activation flow, so the tunnel is up before the Tunnel page is ever shown. A launch-time failure is
/// swallowed (no notification sink exists yet); it surfaces as <see cref="TunnelState.Faulted"/> on the page.
/// </summary>
public sealed class TunnelAutoConnect : BackgroundService
{
    private readonly ILogger<TunnelAutoConnect> _logger;
    private readonly ISettingsService _settings;
    private readonly ITunnelService _tunnel;

    public TunnelAutoConnect(ITunnelService tunnel, ISettingsService settings, ILogger<TunnelAutoConnect> logger)
    {
        _tunnel = tunnel;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.TunnelAutoConnect || string.IsNullOrWhiteSpace(_settings.TunnelHost))
        {
            return;
        }

        try
        {
            await _tunnel.ConnectAsync(cancellationToken: stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down before the connect finished — nothing to do.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Auto-connecting the tunnel at launch failed.");
        }
    }
}
