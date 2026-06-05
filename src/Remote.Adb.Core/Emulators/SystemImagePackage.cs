namespace Remote.Adb.Core.Emulators;

/// <summary>
/// An installed Android system image, identified by its <c>sdkmanager</c> package id.
/// </summary>
/// <param name="Package">The package id, e.g. <c>system-images;android-34;google_apis;x86_64</c> — passed to
/// <c>avdmanager create avd -k</c>.</param>
/// <param name="ApiLevel">The Android API level (e.g. 34).</param>
/// <param name="Tag">The image tag / services variant (e.g. <c>google_apis</c>, <c>google_apis_playstore</c>).</param>
/// <param name="Abi">The ABI (e.g. <c>x86_64</c>, <c>arm64-v8a</c>).</param>
public sealed record SystemImagePackage(string Package, int ApiLevel, string Tag, string Abi)
{
    /// <summary>A friendly name for the image's services variant (<see cref="Tag"/>), e.g. "Google Play".</summary>
    public string Services => Tag switch
    {
        "google_apis_playstore" => "Google Play",
        "google_apis" => "Google APIs",
        "google_apis_tv" => "Google TV",
        "android-tv" => "Android TV",
        "google_apis_automotive_playstore" => "Automotive with Google Play",
        "android-automotive" or "android-automotive-playstore" => "Automotive",
        "android-wear" or "android-wear-signed" => "Wear OS",
        "default" or "aosp_atd" or "google_atd" => "AOSP",
        _ => Tag,
    };

    /// <summary>The Android marketing version for <see cref="ApiLevel"/> (e.g. "Android 14").</summary>
    public string VersionName => AndroidApiLevels.DisplayName(ApiLevel);
}

