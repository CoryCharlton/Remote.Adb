using Remote.Adb.Core.Common;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common;

namespace Remote.Adb.Desktop.Devices;

/// <summary>
/// Builds the grouped <see cref="AvdField"/> model shared by the AVD details pane (view/edit) and the create
/// wizard. The grouping mirrors the Android-Studio "Add Device" UI. Kept in one place so all three surfaces
/// stay in sync; the rendering control (<see cref="PropertyGroupView"/>) is the only thing that differs.
/// </summary>
public static class AvdDetailFields
{
    private static readonly string[] BooleanChoices = ["yes", "no"];
    private static readonly string[] CameraChoices = ["none", "emulated", "virtualscene", "webcam0"];
    private static readonly string[] GpuModeChoices = ["auto", "host", "swiftshader_indirect", "guest", "off"];
    private static readonly string[] NetworkLatencyChoices = ["none", "umts", "edge", "gprs", "hscsd", "gsm"];
    private static readonly string[] NetworkSpeedChoices = ["full", "lte", "hsdpa", "umts", "edge", "gprs", "hscsd", "gsm"];
    private static readonly string[] OrientationChoices = ["portrait", "landscape"];

    /// <summary>The full grouped model (descriptive identity/image/device + tunables + location).</summary>
    public static GroupedFields BuildAll(AvdConfiguration config)
    {
        var fields = new List<AvdField>();
        var groups = new List<DetailGroup>();

        Add(groups, "Identity",
            ReadOnly(fields, "AVD id", config.AvdId),
            Text(fields, "avd.ini.displayname", "Display name", config.DisplayName, Required),
            ReadOnly(fields, "Category", config.Tag),
            ReadOnly(fields, "Image tag", config.TagId));

        Add(groups, "System image",
            ReadOnly(fields, "API level", config.ApiLevel is { } api ? $"{api}  ·  {AndroidApiLevels.DisplayName(api)}" : null),
            ReadOnly(fields, "ABI", config.Abi),
            ReadOnly(fields, "Image", config.SystemImage),
            ReadOnly(fields, "Target", config.Target ?? config.SiblingTarget));

        Add(groups, "Device",
            ReadOnly(fields, "Profile", config.DeviceName),
            ReadOnly(fields, "Manufacturer", config.Manufacturer),
            ReadOnly(fields, "Resolution", Resolution(config)),
            Text(fields, "hw.lcd.density", "Density", config.LcdDensity, Count));

        AddTunable(config, groups, fields);

        Add(groups, "Location",
            ReadOnly(fields, "Path", config.Path));

        return new GroupedFields(groups, fields);
    }

    /// <summary>Just the editable "Additional settings" groups, for the create wizard.</summary>
    public static GroupedFields BuildTunable(AvdConfiguration config)
    {
        var fields = new List<AvdField>();
        var groups = new List<DetailGroup>();
        AddTunable(config, groups, fields);
        return new GroupedFields(groups, fields);
    }

    private static void Add(List<DetailGroup> groups, string header, params AvdField[] rows) =>
        groups.Add(new DetailGroup(header, rows));

    private static void AddTunable(AvdConfiguration config, List<DetailGroup> groups, List<AvdField> fields)
    {
        Add(groups, "Camera",
            Choice(fields, "hw.camera.front", "Front camera", config.FrontCamera, CameraChoices),
            Choice(fields, "hw.camera.back", "Rear camera", config.BackCamera, CameraChoices));

        Add(groups, "Network",
            Choice(fields, "runtime.network.speed", "Speed", config.NetworkSpeed, NetworkSpeedChoices),
            Choice(fields, "runtime.network.latency", "Latency", config.NetworkLatency, NetworkLatencyChoices));

        Add(groups, "Startup",
            Choice(fields, "hw.initialOrientation", "Orientation", config.InitialOrientation, OrientationChoices));

        Add(groups, "Storage",
            Text(fields, "disk.dataPartition.size", "Internal storage", config.DataPartitionSize, Size),
            Choice(fields, "hw.sdCard", "SD card", config.HasSdCard, BooleanChoices),
            Text(fields, "sdcard.size", "SD card size", config.SdCardSize, Size));

        Add(groups, "Emulated performance",
            Text(fields, "hw.cpu.ncore", "CPU cores", config.CpuCores, Count),
            Choice(fields, "hw.gpu.mode", "Graphics", config.GpuMode, GpuModeChoices),
            Choice(fields, "hw.gpu.enabled", "GPU enabled", config.GpuEnabled, BooleanChoices),
            Text(fields, "hw.ramSize", "RAM", config.RamSize, Size, Megabytes(config.RamSize)),
            Text(fields, "vm.heapSize", "VM heap", config.VmHeapSize, Size, Megabytes(config.VmHeapSize)));

        Add(groups, "Skin",
            Text(fields, "skin.name", "Device skin", config.SkinName),
            Choice(fields, "showDeviceFrame", "Device frame", config.ShowDeviceFrame, BooleanChoices));

        Add(groups, "Sensors & input",
            Choice(fields, "hw.gps", "GPS", config.Gps, BooleanChoices),
            Choice(fields, "hw.keyboard", "Keyboard", config.Keyboard, BooleanChoices),
            Choice(fields, "hw.audioInput", "Audio input", config.AudioInput, BooleanChoices));
    }

    private static AvdField Choice(List<AvdField> fields, string key, string label, string? value, IReadOnlyList<string> baseChoices) =>
        Track(fields, new AvdField(key, label, value, choices: ChoiceList(baseChoices, value)));

    // A choice field keeps the raw config value; the list leads with a blank entry (so it can be unset, which
    // omits the key) and includes the current value if it isn't one of the standard options.
    private static IReadOnlyList<string> ChoiceList(IReadOnlyList<string> baseChoices, string? current)
    {
        var list = new List<string> { string.Empty };
        list.AddRange(baseChoices);

        var trimmed = (current ?? string.Empty).Trim();
        if (trimmed.Length > 0 && !list.Contains(trimmed, StringComparer.Ordinal))
        {
            list.Add(trimmed);
        }

        return list;
    }

    private static string? Count(string? value) =>
        string.IsNullOrWhiteSpace(value) || AvdValueConventions.IsValidCount(value)
            ? null
            : "Enter a positive whole number.";

    private static string? Megabytes(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.All(char.IsDigit) ? $"{value} MB" : value;

    private static AvdField ReadOnly(List<AvdField> fields, string label, string? value) =>
        Track(fields, new AvdField(string.Empty, label, value, isReadOnly: true));

    private static string? Required(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Required." : null;

    private static string? Resolution(AvdConfiguration config) =>
        string.IsNullOrWhiteSpace(config.LcdWidth) || string.IsNullOrWhiteSpace(config.LcdHeight)
            ? null
            : $"{config.LcdWidth} × {config.LcdHeight}";

    private static string? Size(string? value) =>
        string.IsNullOrWhiteSpace(value) || AvdValueConventions.IsValidSize(value)
            ? null
            : "Enter a size like 2048, 512M, or 2G.";

    private static AvdField Text(List<AvdField> fields, string key, string label, string? value, Func<string?, string?>? validate = null, string? display = null) =>
        Track(fields, new AvdField(key, label, value, validate: validate, displayValue: display));

    private static AvdField Track(List<AvdField> fields, AvdField field)
    {
        fields.Add(field);
        return field;
    }
}
