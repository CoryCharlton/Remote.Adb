using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Remote.Adb.Desktop.Common.Converters;

/// <summary>
/// Resolves a resource key (e.g. <c>"IconAndroidLogo"</c>) to the <see cref="Geometry"/>
/// registered under that key in the application resources (<c>Themes/Icons.axaml</c>).
/// </summary>
public sealed class ResourceKeyToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key
            && Application.Current is { } application
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
