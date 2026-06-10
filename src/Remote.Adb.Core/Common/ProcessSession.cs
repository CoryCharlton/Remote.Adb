using System.Diagnostics;

namespace Remote.Adb.Core.Common;

/// <inheritdoc />
public sealed class ProcessSession : IProcessSession
{
    private readonly Process _process;
    private readonly Task<string> _standardErrorTask;
    private readonly Task<string> _standardOutputTask;
    private string _standardError = string.Empty;

    public ProcessSession(Process process)
    {
        _process = process;

        // Drain both pipes from the start: a process that fills the stderr buffer while we ignore it (or vice
        // versa) would block forever. stdout is drained but discarded — ssh -N writes its diagnostics to stderr.
        _standardOutputTask = process.StandardOutput.ReadToEndAsync();
        _standardErrorTask = process.StandardError.ReadToEndAsync();
    }

    /// <inheritdoc />
    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    /// <inheritdoc />
    public bool HasExited => _process.HasExited;

    /// <inheritdoc />
    public string StandardError => _standardError;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Kill();

        try
        {
            await Task.WhenAll(_standardOutputTask, _standardErrorTask);
        }
        catch
        {
            // Killing the process tears the pipes down; the drain reads completing in error is expected.
        }

        _process.Dispose();
    }

    /// <inheritdoc />
    public void Kill() => _process.KillTree();

    /// <inheritdoc />
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        await _process.WaitForExitAsync(cancellationToken);

        _standardError = await _standardErrorTask;
        await _standardOutputTask;

        return _process.ExitCode;
    }
}
