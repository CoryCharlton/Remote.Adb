using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Remote.Adb.Core.Common;

namespace Remote.Adb.Core.Adb;

/// <inheritdoc />
public sealed class AdbServerService : IAdbServerService
{
    private readonly IAndroidSdk _androidSdk;
    private readonly ILogger<AdbServerService> _logger;
    private readonly IProcessRunner _processRunner;

    public AdbServerService(IProcessRunner processRunner, IAndroidSdk androidSdk, ILogger<AdbServerService> logger)
    {
        _processRunner = processRunner;
        _androidSdk = androidSdk;
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler? ServerRestarted;

    /// <inheritdoc />
    public async Task<bool> IsRunningAsync(int port, CancellationToken cancellationToken = default)
    {
        if (port is < 1 or > 65535)
        {
            return false;
        }

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
            return true;
        }
        catch (SocketException)
        {
            // Connection refused / unreachable — the server is not listening. A cancellation (OperationCanceledException)
            // is deliberately left to propagate so the health monitor stops probing rather than counting a failure.
            return false;
        }
    }

    /// <inheritdoc />
    public async Task KillAsync(CancellationToken cancellationToken = default)
    {
        // The "use pkill, not kill-server" caveat is about the *remote* adb (where 5037 may be the forwarded
        // port and the round-trip can hang). The local server binds the real 5037, so kill-server is fine here.
        _logger.LogInformation("Killing the local adb server.");
        await RunAsync("kill-server", cancellationToken);
    }

    /// <inheritdoc />
    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        await KillAsync(cancellationToken);
        await StartAsync(cancellationToken);
        ServerRestarted?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting the local adb server.");
        await RunAsync("start-server", cancellationToken);
    }

    private async Task RunAsync(string command, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync(_androidSdk.AdbPath, [command], cancellationToken: cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("adb {Command} exited with code {ExitCode}: {Error}", command, result.ExitCode, result.StandardError);
        }
    }
}
