using Microsoft.Extensions.Logging;
using Remote.Adb.Core.Common;

namespace Remote.Adb.Core.Adb;

/// <inheritdoc />
public sealed class DeviceService : IDeviceService
{
    private readonly IAndroidSdk _androidSdk;
    private readonly ILogger<DeviceService> _logger;
    private readonly IProcessRunner _processRunner;

    public DeviceService(IProcessRunner processRunner, IAndroidSdk androidSdk, ILogger<DeviceService> logger)
    {
        _processRunner = processRunner;
        _androidSdk = androidSdk;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdbDevice>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(_androidSdk.AdbPath, ["devices", "-l"], cancellationToken: cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("adb devices -l exited with code {ExitCode}: {Error}", result.ExitCode, result.StandardError);
        }

        return AdbOutputParser.ParseDeviceList(result.StandardOutput);
    }
}
