namespace Remote.Adb.Core.Common;

/// <inheritdoc />
public sealed class ExecutableFinder : IExecutableFinder
{
    /// <inheritdoc />
    public string? FindOnPath(string fileName) => ExecutableLocator.FindOnPath(fileName);
}
