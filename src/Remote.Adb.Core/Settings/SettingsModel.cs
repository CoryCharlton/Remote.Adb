using System.Text.Json;
using System.Text.Json.Serialization;

namespace Remote.Adb.Core.Settings;

/// <summary>
/// The on-disk shape of persisted settings (serialized as camelCase JSON under the app-data folder). Kept
/// separate from <see cref="ISettingsService"/> so the file schema and the service surface can evolve
/// independently. Add a new setting by adding a property here (with a default) plus a member on the service.
/// </summary>
public sealed class SettingsModel
{
    /// <summary>Override for the AVD home directory; <see langword="null"/> uses the env-var/default resolution.</summary>
    public string? AvdHome { get; set; }

    /// <summary>The selected layout density. Serialized as its enum name.</summary>
    public AppDensity Density { get; set; } = AppDensity.Compact;

    /// <summary>
    /// Unknown keys carried in a newer build's settings file, preserved verbatim so an older build doesn't
    /// drop them when it re-saves.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>Override for the Java home (JDK) directory; <see langword="null"/> uses <c>JAVA_HOME</c>/PATH.</summary>
    public string? JavaHome { get; set; }

    /// <summary>Override for the Android SDK root; <see langword="null"/> uses the env-var/default resolution.</summary>
    public string? SdkRoot { get; set; }

    /// <summary>The selected application color theme. Serialized as its enum name.</summary>
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>Whether to open the SSH reverse tunnel automatically when the app launches.</summary>
    public bool TunnelAutoConnect { get; set; } = true;

    /// <summary>The remote SSH host the reverse tunnel connects to; <see langword="null"/> until configured.</summary>
    public string? TunnelHost { get; set; }

    /// <summary>The local port the tunnel forwards back to (the local adb server port).</summary>
    public int TunnelLocalPort { get; set; } = 5037;

    /// <summary>The remote port the tunnel binds (the port the remote adb client connects to).</summary>
    public int TunnelRemotePort { get; set; } = 5037;
}
