namespace Remote.Adb.Core.Adb;

/// <summary>Friendly details resolved from a device's properties: a display <see cref="Name"/>, its
/// <see cref="Form"/> factor, whether it's an <see cref="IsEmulator"/>, and its <see cref="ApiLevel"/> /
/// <see cref="Abi"/>.</summary>
public sealed record DeviceDetails(string? Name, DeviceForm Form, bool IsEmulator, int? ApiLevel = null, string? Abi = null);
