namespace Remote.Adb.Core.Adb;

/// <summary>
/// Classifies a device's <see cref="DeviceConnection"/> from its adb serial: <c>emulator-NNNN</c> is an
/// emulator, a serial containing <c>:</c> (host:port) or starting with <c>adb-</c> (mDNS TLS) is wireless,
/// and anything else is a USB-attached hardware serial.
/// </summary>
internal static class DeviceConnectionResolver
{
    public static DeviceConnection Resolve(string serial)
    {
        if (serial.StartsWith("emulator-", StringComparison.Ordinal))
        {
            return DeviceConnection.Emulator;
        }

        if (serial.Contains(':') || serial.StartsWith("adb-", StringComparison.Ordinal))
        {
            return DeviceConnection.Wireless;
        }

        return DeviceConnection.Usb;
    }
}
