using Remote.Adb.Core.Common;

namespace Remote.Adb.Core.Emulators;

/// <summary>
/// An AVD's full configuration: typed accessors over its <c>config.ini</c> (and the sibling
/// <c>&lt;AvdId&gt;.ini</c>), backed by the complete <see cref="IniDocument"/> so an edit can round-trip
/// every key — including ones not surfaced here. Read via <see cref="IAvdConfigStore"/>. Accessors return
/// the raw <c>config.ini</c> values (typed conversion of
/// the editable fields lands with the edit feature); <see cref="ApiLevel"/> is the one derived value.
/// </summary>
public sealed class AvdConfiguration
{
    public AvdConfiguration(string avdId, IniDocument config, IniDocument? sibling)
    {
        AvdId = avdId;
        Config = config;
        Sibling = sibling;
    }

    /// <summary><c>abi.type</c> — the system image ABI (e.g. <c>x86_64</c>, <c>arm64-v8a</c>).</summary>
    public string? Abi => Config.Get("abi.type");

    /// <summary>The API level parsed from <see cref="SystemImage"/> (e.g. 34), or <see langword="null"/>.</summary>
    public int? ApiLevel => ParseApiLevel(SystemImage);

    /// <summary><c>hw.audioInput</c>.</summary>
    public string? AudioInput => Config.Get("hw.audioInput");

    /// <summary>The AVD id as reported by <c>emulator -list-avds</c> — the <c>config.ini</c> <c>AvdId</c> when
    /// present (Android Studio writes it), else the <c>.avd</c> folder name (<c>avdmanager create</c> doesn't).</summary>
    public string AvdId { get; }

    /// <summary><c>hw.camera.back</c>.</summary>
    public string? BackCamera => Config.Get("hw.camera.back");

    /// <summary>The full parsed <c>config.ini</c> backing this configuration (preserves every key).</summary>
    public IniDocument Config { get; }

    /// <summary><c>hw.cpu.arch</c>.</summary>
    public string? CpuArch => Config.Get("hw.cpu.arch");

    /// <summary><c>hw.cpu.ncore</c> — the number of emulated CPU cores.</summary>
    public string? CpuCores => Config.Get("hw.cpu.ncore");

    /// <summary><c>disk.dataPartition.size</c>.</summary>
    public string? DataPartitionSize => Config.Get("disk.dataPartition.size");

    /// <summary><c>hw.device.name</c> — the device profile id (e.g. <c>pixel_6</c>).</summary>
    public string? DeviceName => Config.Get("hw.device.name");

    /// <summary><c>avd.ini.displayname</c>; falls back to <see cref="AvdId"/>.</summary>
    public string DisplayName => Config.Get("avd.ini.displayname") is { Length: > 0 } name ? name : AvdId;

    /// <summary><c>hw.camera.front</c>.</summary>
    public string? FrontCamera => Config.Get("hw.camera.front");

    /// <summary><c>hw.gps</c>.</summary>
    public string? Gps => Config.Get("hw.gps");

    /// <summary><c>hw.gpu.enabled</c>.</summary>
    public string? GpuEnabled => Config.Get("hw.gpu.enabled");

    /// <summary><c>hw.gpu.mode</c>.</summary>
    public string? GpuMode => Config.Get("hw.gpu.mode");

    /// <summary><c>hw.sdCard</c>.</summary>
    public string? HasSdCard => Config.Get("hw.sdCard");

    /// <summary><c>hw.initialOrientation</c>.</summary>
    public string? InitialOrientation => Config.Get("hw.initialOrientation");

    /// <summary><c>hw.keyboard</c>.</summary>
    public string? Keyboard => Config.Get("hw.keyboard");

    /// <summary><c>hw.lcd.density</c>.</summary>
    public string? LcdDensity => Config.Get("hw.lcd.density");

    /// <summary><c>hw.lcd.height</c>.</summary>
    public string? LcdHeight => Config.Get("hw.lcd.height");

    /// <summary><c>hw.lcd.width</c>.</summary>
    public string? LcdWidth => Config.Get("hw.lcd.width");

    /// <summary><c>hw.device.manufacturer</c>.</summary>
    public string? Manufacturer => Config.Get("hw.device.manufacturer");

    /// <summary><c>runtime.network.latency</c>.</summary>
    public string? NetworkLatency => Config.Get("runtime.network.latency");

    /// <summary><c>runtime.network.speed</c>.</summary>
    public string? NetworkSpeed => Config.Get("runtime.network.speed");

    /// <summary><c>path</c> from the sibling <c>&lt;AvdId&gt;.ini</c> — the on-disk <c>.avd</c> folder.</summary>
    public string? Path => Sibling?.Get("path");

    /// <summary><c>hw.ramSize</c>.</summary>
    public string? RamSize => Config.Get("hw.ramSize");

    /// <summary><c>sdcard.size</c>.</summary>
    public string? SdCardSize => Config.Get("sdcard.size");

    /// <summary><c>showDeviceFrame</c>.</summary>
    public string? ShowDeviceFrame => Config.Get("showDeviceFrame");

    /// <summary>The sibling <c>&lt;AvdId&gt;.ini</c> document (<c>path=</c>/<c>target=</c>), if present.</summary>
    public IniDocument? Sibling { get; }

    /// <summary><c>target</c> from the sibling <c>&lt;AvdId&gt;.ini</c>.</summary>
    public string? SiblingTarget => Sibling?.Get("target");

    /// <summary><c>skin.name</c>.</summary>
    public string? SkinName => Config.Get("skin.name");

    /// <summary><c>skin.path</c>.</summary>
    public string? SkinPath => Config.Get("skin.path");

    /// <summary><c>image.sysdir.1</c> — the system image directory.</summary>
    public string? SystemImage => Config.Get("image.sysdir.1");

    /// <summary><c>tag.displaynames</c> — the device category (e.g. "Google TV").</summary>
    public string? Tag => Config.Get("tag.displaynames");

    /// <summary><c>tag.id</c> — the system image tag id (e.g. <c>google_apis</c>).</summary>
    public string? TagId => Config.Get("tag.id");

    /// <summary><c>target</c> from <c>config.ini</c>.</summary>
    public string? Target => Config.Get("target");

    /// <summary><c>vm.heapSize</c>.</summary>
    public string? VmHeapSize => Config.Get("vm.heapSize");

    private static int? ParseApiLevel(string? systemImage)
    {
        if (string.IsNullOrEmpty(systemImage))
        {
            return null;
        }

        // e.g. "system-images/android-34/google_apis/x86_64/" -> 34; "android-36.1/..." -> 36.
        const string prefix = "android-";
        foreach (var segment in systemImage.Split('/', '\\'))
        {
            if (segment.StartsWith(prefix, StringComparison.Ordinal)
                && AndroidApiLevels.TryParseLevel(segment, out var level))
            {
                return level;
            }
        }

        return null;
    }

    /// <summary>Projects the lightweight list metadata (<see cref="AvdMetadata"/>) from this configuration.</summary>
    public AvdMetadata ToMetadata() => new(AvdId, DisplayName, Tag, ApiLevel, Abi);
}
