using Remote.Adb.Core.Common;

namespace Remote.Adb.Core.UnitTests.Fakes;

/// <summary>
/// A controllable <see cref="IProcessSession"/>. Construct it already-exited (a lost bind race) or leave it
/// running and later <see cref="Exit"/> it (a tunnel that drops); <see cref="KillCount"/> records teardown.
/// </summary>
public sealed class FakeProcessSession : IProcessSession
{
    private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public FakeProcessSession(int? immediateExitCode = null, string standardError = "")
    {
        StandardError = standardError;

        if (immediateExitCode is not null)
        {
            ExitCode = immediateExitCode;
            _exit.SetResult(immediateExitCode.Value);
        }
    }

    public int? ExitCode { get; private set; }

    public bool HasExited => _exit.Task.IsCompleted;

    public int KillCount { get; private set; }

    public string StandardError { get; private set; }

    public ValueTask DisposeAsync()
    {
        Kill();
        return ValueTask.CompletedTask;
    }

    public void Exit(int exitCode, string? standardError = null)
    {
        if (standardError is not null)
        {
            StandardError = standardError;
        }

        ExitCode = exitCode;
        _exit.TrySetResult(exitCode);
    }

    public void Kill()
    {
        KillCount++;
        ExitCode ??= -1;
        _exit.TrySetResult(ExitCode.Value);
    }

    public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default) => _exit.Task.WaitAsync(cancellationToken);
}
