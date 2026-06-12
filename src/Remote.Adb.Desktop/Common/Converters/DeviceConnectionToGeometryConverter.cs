using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Remote.Adb.Core.Adb;

namespace Remote.Adb.Desktop.Common.Converters;

/// <summary>
/// Maps a physical device's <see cref="DeviceConnection"/> to a Phosphor glyph from <c>Themes/Icons.axaml</c>:
/// a USB mark for a wired device, a wifi mark for a wireless one.
/// </summary>
public sealed class DeviceConnectionToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is DeviceConnection.Wireless ? "IconWifiHigh" : "IconUsb";

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
}
