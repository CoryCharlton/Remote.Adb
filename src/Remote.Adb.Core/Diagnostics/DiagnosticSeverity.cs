namespace Remote.Adb.Core.Diagnostics;

/// <summary>The severity of a <see cref="ToolDiagnostic"/>.</summary>
public enum DiagnosticSeverity
{
    /// <summary>The tool resolved only by guessing (a default or ambient fallback) — it may be wrong.</summary>
    Warning,

    /// <summary>The tool could not be resolved at all — the features that depend on it won't work.</summary>
    Error,
}
