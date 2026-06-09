namespace Remote.Adb.Core.Common;

/// <summary>
/// Where <see cref="IAndroidSdk.SdkRoot"/> was resolved from, so the UI can explain (and warn about) the
/// effective path.
/// </summary>
public enum SdkRootSource
{
    /// <summary>No SDK root could be resolved.</summary>
    NotFound,

    /// <summary>From the Settings override.</summary>
    Override,

    /// <summary>From the <c>ANDROID_HOME</c> / <c>ANDROID_SDK_ROOT</c> environment variable.</summary>
    EnvironmentVariable,

    /// <summary>From the platform-default install location (a best guess — may be the wrong SDK).</summary>
    DefaultFallback,
}
