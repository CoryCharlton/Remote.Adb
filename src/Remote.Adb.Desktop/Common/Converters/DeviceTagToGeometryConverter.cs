using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Remote.Adb.Desktop.Common.Converters;

/// <summary>
/// Maps an AVD device tag (e.g. "Google TV", "Wear OS 6.0") to a Phosphor glyph from
/// <c>Themes/Icons.axaml</c>. Falls back to the Android mark for unknown/missing tags.
/// </summary>
public sealed class DeviceTagToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = ResolveKey(value as string);

        if (Application.Current is { } application
            && application.TryGetResource(key, null, out var resource)
            && resource is Geometry geometry)
        {
            return geometry;
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static string ResolveKey(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return "IconAndroidLogo";
        }

        if (tag.Contains("TV", StringComparison.OrdinalIgnoreCase))
        {
            return "IconTelevision";
        }

        if (tag.Contains("Wear", StringComparison.OrdinalIgnoreCase))
        {
            return "IconWatch";
        }

        if (tag.Contains("Tablet", StringComparison.OrdinalIgnoreCase))
        {
            return "IconDeviceTablet";
        }

        if (tag.Contains("Phone", StringComparison.OrdinalIgnoreCase) || tag.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
        {
            return "IconDeviceMobile";
        }

        return "IconAndroidLogo";
    }
}
