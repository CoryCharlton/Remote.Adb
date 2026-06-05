using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Enumerates the system images installed under an SDK root by walking
/// <c>system-images/android-&lt;n&gt;/&lt;tag&gt;/&lt;abi&gt;/</c> — version-independent and far simpler than
/// parsing <c>sdkmanager --list</c>. The root is passed in so it stays pure and testable.
/// </summary>
public static class SystemImageScanner
{
    /// <summary>
    /// Returns the installed <see cref="SystemImagePackage"/>s under <paramref name="sdkRoot"/> (newest API
    /// first), or an empty list when the root is null/missing or has no <c>system-images</c> directory.
    /// </summary>
    public static IReadOnlyList<SystemImagePackage> Scan(string? sdkRoot)
    {
        if (string.IsNullOrWhiteSpace(sdkRoot))
        {
            return [];
        }

        var root = Path.Combine(sdkRoot, "system-images");
        if (!Directory.Exists(root))
        {
            return [];
        }

        var images = new List<SystemImagePackage>();

        foreach (var apiDirectory in Directory.EnumerateDirectories(root))
        {
            var apiName = Path.GetFileName(apiDirectory); // e.g. "android-34"
            if (!apiName.StartsWith("android-", StringComparison.Ordinal)
                || !int.TryParse(apiName["android-".Length..], out var apiLevel))
            {
                continue;
            }

            foreach (var tagDirectory in Directory.EnumerateDirectories(apiDirectory))
            {
                var tag = Path.GetFileName(tagDirectory); // e.g. "google_apis"

                foreach (var abiDirectory in Directory.EnumerateDirectories(tagDirectory))
                {
                    var abi = Path.GetFileName(abiDirectory); // e.g. "x86_64"
                    images.Add(new SystemImagePackage($"system-images;{apiName};{tag};{abi}", apiLevel, tag, abi));
                }
            }
        }

        return images
            .OrderByDescending(image => image.ApiLevel)
            .ThenBy(image => image.Tag, StringComparer.Ordinal)
            .ThenBy(image => image.Abi, StringComparer.Ordinal)
            .ToList();
    }
}
