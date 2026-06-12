using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Remote.Adb.Core.Adb;

namespace Remote.Adb.Desktop.Common.Converters;

/// <summary>
/// Maps a physical device's <see cref="DeviceForm"/> to a Phosphor glyph from <c>Themes/Icons.axaml</c>.
/// Falls back to the phone mark.
/// </summary>
public sealed class DeviceFormToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            DeviceForm.Tablet => "IconDeviceTablet",
            DeviceForm.Watch => "IconWatch",
            DeviceForm.Television => "IconTelevision",
            _ => "IconDeviceMobile",
        };

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
