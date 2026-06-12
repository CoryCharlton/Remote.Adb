namespace Remote.Adb.Core.Adb;

/// <summary>How a device is attached to the local adb server, derived from its serial shape.</summary>
public enum DeviceConnection
{
    /// <summary>A USB-attached physical device (a hardware serial).</summary>
    Usb,

    /// <summary>A device reached over the network (<c>host:port</c> or an mDNS-paired <c>adb-…_tcp</c> serial).</summary>
    Wireless,

    /// <summary>A running emulator (<c>emulator-NNNN</c>).</summary>
    Emulator,
}
