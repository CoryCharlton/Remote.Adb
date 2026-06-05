using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remote.Adb.Core;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Emulators;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSimpleConsole(options => options.SingleLine = true).SetMinimumLevel(LogLevel.Warning));
services.AddRemoteAdbCore();

using var provider = services.BuildServiceProvider();

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

try
{
    return args[0] switch
    {
        "emulator" => await HandleEmulatorAsync(
            args[1..],
            provider.GetRequiredService<IEmulatorService>(),
            provider.GetRequiredService<IAvdConfigStore>(),
            provider.GetRequiredService<IAvdProvisioningService>()),
        _ => Unknown(args[0]),
    };
}
catch (ProcessLaunchException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static async Task<int> HandleEmulatorAsync(string[] args, IEmulatorService emulators, IAvdConfigStore configStore, IAvdProvisioningService provisioning)
{
    var command = args.Length > 0 ? args[0] : string.Empty;

    switch (command)
    {
        case "list":
            var devices = await emulators.ListAsync();

            if (devices.Count == 0)
            {
                Console.WriteLine("No AVDs found.");
                return 0;
            }

            foreach (var device in devices)
            {
                var status = device.IsRunning ? $"running ({device.Serial})" : "stopped";
                var tag = device.Tag is null ? string.Empty : $" — {device.Tag}";
                Console.WriteLine($"{device.DisplayName}  ({device.Name}){tag}  [{status}]");
            }

            return 0;

        case "start":
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: emulator start <avd-name>");
                return 1;
            }

            await emulators.StartAsync(args[1]);
            Console.WriteLine($"Starting {args[1]}...");
            return 0;

        case "stop":
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: emulator stop <serial>");
                return 1;
            }

            await emulators.StopAsync(args[1]);
            Console.WriteLine($"Stopped {args[1]}.");
            return 0;

        case "info":
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: emulator info <avd-name>");
                return 1;
            }

            var configuration = configStore.Read(args[1]);
            if (configuration is null)
            {
                Console.Error.WriteLine($"No AVD found with id '{args[1]}'.");
                return 1;
            }

            PrintConfiguration(configuration);
            return 0;

        case "edit":
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: emulator edit <avd-name> --<key> <value> [--<key> <value>…]  (a bare --<key> clears it)");
                return 1;
            }

            if (!TryParseEdits(args[2..], out var changes, out var removals, out var parseError))
            {
                Console.Error.WriteLine(parseError);
                return 1;
            }

            if (changes.Count == 0 && removals.Count == 0)
            {
                Console.Error.WriteLine("Nothing to change. Pass --<key> <value> to set, or a bare --<key> to clear.");
                return 1;
            }

            var edited = configStore.Write(args[1], changes, removals);
            if (edited is null)
            {
                Console.Error.WriteLine($"No AVD found with id '{args[1]}'.");
                return 1;
            }

            PrintConfiguration(edited);
            return 0;

        case "images":
            var images = await provisioning.ListInstalledImagesAsync();

            if (images.Count == 0)
            {
                Console.WriteLine("No installed system images. Install one with the SDK manager.");
                return 0;
            }

            foreach (var image in images)
            {
                Console.WriteLine($"{image.Package}  (API {image.ApiLevel}, {image.Tag}, {image.Abi})");
            }

            return 0;

        case "devices":
            foreach (var profile in await provisioning.ListDevicesAsync())
            {
                var specs = profile.ScreenWidth is { } width && profile.ScreenHeight is { } height
                    ? $"  {width}x{height}" + (profile.Density is { } density ? $" {density}dpi" : string.Empty)
                    : string.Empty;
                var api = string.IsNullOrEmpty(profile.SupportedApi) ? string.Empty : $" API {profile.SupportedApi}";
                var oem = profile.Oem is null ? string.Empty : $"  — {profile.Oem}";
                Console.WriteLine($"{profile.Id,-26}[{profile.FormFactor,-9}] {profile.Name}{api}{specs}{oem}");
            }

            return 0;

        case "create":
            if (!TryParseCreate(args[1..], out var name, out var package, out var deviceId, out var overrides, out var createError))
            {
                Console.Error.WriteLine(createError);
                return 1;
            }

            if (!await provisioning.CreateAsync(name, package, deviceId))
            {
                Console.Error.WriteLine("avdmanager could not create the AVD. Check the SDK installation.");
                return 1;
            }

            if (overrides.Count > 0)
            {
                configStore.Write(name, overrides);
            }

            Console.WriteLine($"Created {name}.");
            var created = configStore.Read(name);
            if (created is not null)
            {
                PrintConfiguration(created);
            }

            return 0;

        case "delete":
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: emulator delete <avd-name>");
                return 1;
            }

            if (!await provisioning.DeleteAsync(args[1]))
            {
                Console.Error.WriteLine($"Could not delete '{args[1]}'.");
                return 1;
            }

            Console.WriteLine($"Deleted {args[1]}.");
            return 0;

        default:
            Console.Error.WriteLine("Usage: emulator <list|info|edit|images|devices|create|delete|start|stop> …");
            return 1;
    }
}

// Parses `--key value` pairs into changes; a bare `--key` with no following value becomes a removal.
static bool TryParseEdits(string[] tokens, out Dictionary<string, string> changes, out List<string> removals, out string? error)
{
    changes = new Dictionary<string, string>(StringComparer.Ordinal);
    removals = [];
    error = null;

    for (var i = 0; i < tokens.Length; i++)
    {
        if (!tokens[i].StartsWith("--", StringComparison.Ordinal) || tokens[i].Length == 2)
        {
            error = $"Expected --<key>, got '{tokens[i]}'.";
            return false;
        }

        var key = tokens[i][2..];

        if (i + 1 < tokens.Length && !tokens[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            changes[key] = tokens[++i];
        }
        else
        {
            removals.Add(key);
        }
    }

    return true;
}

// Parses `-n <name> -k <package> -d <device>` plus optional `--<key> <value>` config overrides.
static bool TryParseCreate(string[] tokens, out string name, out string package, out string device, out Dictionary<string, string> overrides, out string? error)
{
    name = string.Empty;
    package = string.Empty;
    device = string.Empty;
    overrides = new Dictionary<string, string>(StringComparer.Ordinal);
    error = null;

    for (var i = 0; i < tokens.Length; i++)
    {
        var hasValue = i + 1 < tokens.Length;

        switch (tokens[i])
        {
            case "-n" when hasValue:
                name = tokens[++i];
                break;
            case "-k" when hasValue:
                package = tokens[++i];
                break;
            case "-d" when hasValue:
                device = tokens[++i];
                break;
            default:
                if (tokens[i].StartsWith("--", StringComparison.Ordinal) && tokens[i].Length > 2
                    && hasValue && !tokens[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    overrides[tokens[i][2..]] = tokens[++i];
                }
                else
                {
                    error = $"Unexpected argument '{tokens[i]}'.";
                    return false;
                }

                break;
        }
    }

    if (name.Length == 0 || package.Length == 0 || device.Length == 0)
    {
        error = "Usage: emulator create -n <name> -k <package> -d <device> [--<key> <value>…]";
        return false;
    }

    return true;
}

static void PrintConfiguration(AvdConfiguration config)
{
    static void Line(string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Console.WriteLine($"  {label,-16}{value.Trim()}");
        }
    }

    Console.WriteLine($"{config.DisplayName}  ({config.AvdId})");
    Line("API level", config.ApiLevel?.ToString());
    Line("ABI", config.Abi);
    Line("Image", config.SystemImage);
    Line("Tag", config.Tag);
    Line("Device", config.DeviceName);
    Line("Manufacturer", config.Manufacturer);
    Line("Resolution", config.LcdWidth is { Length: > 0 } width && config.LcdHeight is { Length: > 0 } height ? $"{width} x {height}" : null);
    Line("Density", config.LcdDensity);
    Line("RAM", config.RamSize);
    Line("VM heap", config.VmHeapSize);
    Line("Data partition", config.DataPartitionSize);
    Line("SD card", config.SdCardSize ?? config.HasSdCard);
    Line("CPU cores", config.CpuCores);
    Line("GPU", config.GpuMode ?? config.GpuEnabled);
    Line("Path", config.Path);
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Remote.Adb console");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  emulator list                 List available AVDs and their running state");
    Console.WriteLine("  emulator info <avd-name>      Show the full configuration of the given AVD");
    Console.WriteLine("  emulator edit <avd-name> …    Set --<key> <value> pairs (bare --<key> clears) and persist");
    Console.WriteLine("  emulator images               List installed system-image packages");
    Console.WriteLine("  emulator devices              List device profiles for create");
    Console.WriteLine("  emulator create -n … -k … -d … [--<key> <value>…]   Create an AVD and apply overrides");
    Console.WriteLine("  emulator delete <avd-name>    Delete the given AVD");
    Console.WriteLine("  emulator start <avd-name>     Launch the given AVD");
    Console.WriteLine("  emulator stop <serial>        Stop the running emulator with the given serial");
}
