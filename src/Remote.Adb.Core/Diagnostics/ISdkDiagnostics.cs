namespace Remote.Adb.Core.Diagnostics;

/// <summary>
/// Evaluates how the Android toolchain (SDK, JDK) resolved and reports any tool that is only guessed at or
/// missing, so the app can warn or error proactively rather than failing later.
/// </summary>
public interface ISdkDiagnostics
{
    /// <summary>The non-ok tool resolutions (empty when everything resolved explicitly).</summary>
    IReadOnlyList<ToolDiagnostic> Evaluate();
}
