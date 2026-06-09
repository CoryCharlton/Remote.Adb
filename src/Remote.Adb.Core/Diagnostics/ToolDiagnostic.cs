namespace Remote.Adb.Core.Diagnostics;

/// <summary>A configuration problem with a resolved tool, suitable for surfacing to the user.</summary>
/// <param name="Title">A short headline (e.g. "Android SDK").</param>
/// <param name="Message">The actionable detail.</param>
/// <param name="Severity">Whether the tool is merely guessed (<see cref="DiagnosticSeverity.Warning"/>) or missing.</param>
public sealed record ToolDiagnostic(string Title, string Message, DiagnosticSeverity Severity);
