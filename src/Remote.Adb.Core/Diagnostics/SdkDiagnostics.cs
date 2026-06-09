using Remote.Adb.Core.Common;

namespace Remote.Adb.Core.Diagnostics;

/// <inheritdoc />
public sealed class SdkDiagnostics : ISdkDiagnostics
{
    private readonly IAndroidSdk _androidSdk;

    public SdkDiagnostics(IAndroidSdk androidSdk)
    {
        _androidSdk = androidSdk;
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolDiagnostic> Evaluate()
    {
        var diagnostics = new List<ToolDiagnostic>();

        if (EvaluateSdk() is { } sdk)
        {
            diagnostics.Add(sdk);
        }

        if (EvaluateJdk() is { } jdk)
        {
            diagnostics.Add(jdk);
        }

        return diagnostics;
    }

    private ToolDiagnostic? EvaluateJdk()
    {
        // JavaHome already folds in the override and JAVA_HOME, and avdmanager/sdkmanager use it in preference
        // to PATH — so when it's set, its validity is decisive: a set-but-broken value is an error regardless of
        // any java on PATH (the tools will use the broken value and fail).
        var javaHome = _androidSdk.JavaHome;
        if (javaHome is not null)
        {
            return HasJava(javaHome)
                ? null
                : new ToolDiagnostic(
                    "JDK",
                    $"JAVA_HOME is set to {javaHome}, but no java was found there. Fix or clear the JDK path in Settings.",
                    DiagnosticSeverity.Error);
        }

        if (ExecutableLocator.FindOnPath("java") is { } onPath)
        {
            return new ToolDiagnostic(
                "JDK",
                $"JAVA_HOME not set — using java from PATH ({onPath}). Set JAVA_HOME or the JDK path in Settings to pin it.",
                DiagnosticSeverity.Warning);
        }

        return new ToolDiagnostic(
            "JDK",
            "No JDK found — avdmanager/sdkmanager can't run. Set the JDK path in Settings.",
            DiagnosticSeverity.Error);
    }

    private ToolDiagnostic? EvaluateSdk() => _androidSdk.SdkRootSource switch
    {
        SdkRootSource.DefaultFallback => new ToolDiagnostic(
            "Android SDK",
            $"Android SDK not configured — guessing {_androidSdk.SdkRoot}. Set ANDROID_HOME or the SDK path in Settings if that's wrong.",
            DiagnosticSeverity.Warning),
        SdkRootSource.NotFound => new ToolDiagnostic(
            "Android SDK",
            "Android SDK not found. Set ANDROID_HOME or the SDK path in Settings.",
            DiagnosticSeverity.Error),
        _ => null,
    };

    private static bool HasJava(string javaHome)
    {
        var executable = OperatingSystem.IsWindows() ? "java.exe" : "java";
        return File.Exists(Path.Combine(javaHome, "bin", executable));
    }
}
