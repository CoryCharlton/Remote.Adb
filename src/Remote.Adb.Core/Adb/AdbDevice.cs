namespace Remote.Adb.Core.Adb;

/// <summary>
/// A device reported by <c>adb devices -l</c>. <see cref="State"/> is the adb connection state
/// (<c>device</c>, <c>offline</c>, <c>unauthorized</c>, …); the descriptive columns are only present for
/// devices in the online <c>device</c> state.
/// </summary>
public sealed record AdbDevice(string Serial, string State, string? Model, string? Product, string? Device, string? TransportId)
{
    /// <summary>The system image ABI (e.g. <c>arm64-v8a</c>), resolved from the device's properties.</summary>
    public string? Abi { get; init; }

    /// <summary>The Android API level, resolved from the device's properties.</summary>
    public int? ApiLevel { get; init; }

    /// <summary>How the device is attached (USB, wireless, or emulator), derived from its serial.</summary>
    public DeviceConnection Connection => DeviceConnectionResolver.Resolve(Serial);

    /// <summary>The device's form factor, resolved from its properties; <see cref="DeviceForm.Phone"/> until enriched.</summary>
    public DeviceForm Form { get; init; }

    /// <summary>Whether the device is a running emulator, resolved from its properties.</summary>
    public bool IsEmulator { get; init; }

    /// <summary>Whether the device is online and usable (adb state <c>device</c>).</summary>
    public bool IsOnline => State.Equals("device", StringComparison.Ordinal);

    /// <summary>A friendly display name (marketing/AVD name) resolved from the device's properties, if available.</summary>
    public string? Name { get; init; }
}
