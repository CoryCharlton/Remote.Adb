namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Buckets a device or system-image tag into a coarse category (tv, wear, automotive, …, or general for
/// phones/tablets). A device is only compatible with images in the same category — used by the create wizard
/// to filter the image list to the selected device.
/// </summary>
public static class AvdCategories
{
    /// <summary>The category for a device or image tag (e.g. <c>android-tv</c> → "tv", <c>null</c> → "general").</summary>
    public static string Of(string? tag)
    {
        var value = (tag ?? string.Empty).ToLowerInvariant();

        if (value.Contains("automotive"))
        {
            return "automotive";
        }

        if (value.Contains("tv"))
        {
            return "tv";
        }

        if (value.Contains("wear"))
        {
            return "wear";
        }

        if (value.Contains("desktop"))
        {
            return "desktop";
        }

        if (value.Contains("xr") || value.Contains("glass"))
        {
            return "xr";
        }

        return "general";
    }
}
