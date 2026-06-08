using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Parses Android device-definition XML (the <c>&lt;d:device&gt;</c> entries shipped in <c>sdklib.jar</c> and
/// in the user's <c>devices.xml</c>) into <see cref="DeviceProfile"/>s with screen specs. Matches by element
/// local name so it's tolerant of the schema's namespace/version. Pure and I/O-free.
/// </summary>
public static class DeviceDefinitionParser
{
    public static IReadOnlyList<DeviceProfile> Parse(string xml)
    {
        var devices = new List<DeviceProfile>();

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return devices;
        }

        foreach (var device in document.Descendants().Where(element => element.Name.LocalName == "device"))
        {
            var name = Value(device, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var id = Value(device, "id");
            var screen = Descendant(device, "screen");
            var width = Integer(Value(screen, "x-dimension"));
            var height = Integer(Value(screen, "y-dimension"));
            var diagonal = Real(Value(screen, "diagonal-length"));

            devices.Add(new DeviceProfile(
                string.IsNullOrWhiteSpace(id) ? name : id,
                name,
                Blank(Value(device, "manufacturer")),
                Blank(Value(device, "tag-id")),
                width,
                height,
                Density(Value(screen, "pixel-density"), width, height, diagonal),
                Ram(Descendant(device, "ram")),
                Blank(Value(screen, "screen-size")),
                MinApi(Value(device, "api-level")),
                string.Equals(Value(device, "playstore-enabled"), "true", StringComparison.OrdinalIgnoreCase),
                string.Equals(Attribute(device, "deprecated"), "true", StringComparison.OrdinalIgnoreCase),
                diagonal));
        }

        return devices;
    }

    private static string? Attribute(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // dpi from the pixel-density value ("420dpi" or a bucket like "xxhdpi"), else computed from the
    // resolution and diagonal length.
    private static int? Density(string? pixelDensity, int? width, int? height, double? diagonal)
    {
        if (!string.IsNullOrWhiteSpace(pixelDensity))
        {
            var digits = new string(pixelDensity.TakeWhile(char.IsDigit).ToArray());
            if (digits.Length > 0 && int.TryParse(digits, out var explicitDpi))
            {
                return explicitDpi;
            }

            var bucket = pixelDensity.Trim().ToLowerInvariant() switch
            {
                "ldpi" => 120,
                "mdpi" => 160,
                "tvdpi" => 213,
                "hdpi" => 240,
                "xhdpi" => 320,
                "xxhdpi" => 480,
                "xxxhdpi" => 640,
                _ => 0,
            };

            if (bucket > 0)
            {
                return bucket;
            }
        }

        if (width is > 0 && height is > 0 && diagonal is > 0)
        {
            var diagonalPixels = Math.Sqrt((double)(width.Value * width.Value) + (double)(height.Value * height.Value));
            return (int)Math.Round(diagonalPixels / diagonal.Value);
        }

        return null;
    }

    private static XElement? Descendant(XElement? element, string localName) =>
        element?.Descendants().FirstOrDefault(child => child.Name.LocalName == localName);

    private static int? Integer(string? value) =>
        int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    // api-level is a single level or a range ("24", "7-10"); take the lower bound.
    private static int? MinApi(string? apiLevel)
    {
        var digits = new string((apiLevel ?? string.Empty).Trim().TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var api) ? api : null;
    }

    private static int? Ram(XElement? ram)
    {
        if (ram is null
            || !double.TryParse(ram.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        var unit = ram.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "unit")?.Value;
        var megabytes = unit switch
        {
            "GiB" or "GB" => amount * 1024,
            "KiB" or "KB" => amount / 1024,
            _ => amount, // MiB
        };

        return (int)Math.Round(megabytes);
    }

    private static double? Real(string? value) =>
        double.TryParse((value ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static string? Value(XElement? element, string localName) =>
        Descendant(element, localName)?.Value.Trim();
}
