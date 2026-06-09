namespace Remote.Adb.Core.Emulators;

/// <summary>
/// The outcome of an <c>avdmanager</c> operation: whether it succeeded and, on failure, the tool's own message.
/// <c>avdmanager</c> reports errors (a missing JDK, an unknown device, a bad package) on stdout rather than
/// stderr, so <see cref="Error"/> merges both streams to carry the actual reason.
/// </summary>
/// <param name="Success">Whether the operation completed successfully.</param>
/// <param name="Error">The failure detail to surface, or <see langword="null"/> on success.</param>
public sealed record AvdOperationResult(bool Success, string? Error)
{
    /// <summary>A successful result, with no error.</summary>
    public static AvdOperationResult Ok { get; } = new(true, null);

    /// <summary>A failed result carrying <paramref name="error"/> (the tool's output, when any).</summary>
    public static AvdOperationResult Fail(string? error) => new(false, error);
}
