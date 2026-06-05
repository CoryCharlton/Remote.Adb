using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Remote.Adb.Core.Common;

/// <inheritdoc />
public sealed class ProcessRunner : IProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger)
    {
        _logger = logger;
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // On Windows a .bat/.cmd (avdmanager, sdkmanager) isn't a valid executable for CreateProcess —
        // Process.Start throws Win32Exception. Run it through the command interpreter instead.
        if (OperatingSystem.IsWindows() && IsBatchScript(fileName))
        {
            startInfo.FileName = "cmd.exe";
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(fileName);
        }
        else
        {
            startInfo.FileName = fileName;
        }

        // ArgumentList handles quoting/escaping per-argument, avoiding command-line injection bugs.
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static bool IsBatchScript(string fileName) =>
        fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);

    private static void KillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // The process already exited (or can't be killed) — nothing more to do.
        }
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // Swallow: we're already unwinding a cancellation; awaiting just marks the task observed.
        }
    }

    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? standardInput = null, CancellationToken cancellationToken = default)
    {
        var startInfo = CreateStartInfo(fileName, arguments);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardInput = standardInput is not null;

        _logger.LogDebug("Running {FileName} {Arguments}", fileName, string.Join(' ', arguments));

        using var process = new Process { StartInfo = startInfo };
        StartProcess(process, fileName);

        if (standardInput is not null)
        {
            // Feed the prompt response (e.g. avdmanager's "create a custom hardware profile? [no]") then
            // close stdin so the tool stops waiting on input. A tool that errors out before reading stdin
            // (bad package, missing JDK) closes the pipe early, so a broken-pipe write here is benign — let
            // the non-zero exit code carry the failure instead of throwing an IOException out of RunAsync.
            try
            {
                await process.StandardInput.WriteAsync(standardInput);
                process.StandardInput.Close();
            }
            catch (IOException)
            {
            }
        }

        // Read both streams concurrently and only then wait for exit, otherwise a process that
        // fills the stderr pipe buffer while we drain stdout (or vice versa) would deadlock.
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;

            return new ProcessResult(process.ExitCode, standardOutput, standardError);
        }
        catch (OperationCanceledException)
        {
            // Cancellation must not leave the spawned tool (a JVM/emulator) running detached. Kill the whole
            // tree, then observe the stream reads — which cancel too — so they don't surface later as
            // unobserved-task exceptions, before propagating the cancellation.
            KillTree(process);
            await ObserveAsync(standardOutputTask);
            await ObserveAsync(standardErrorTask);
            throw;
        }
    }

    /// <inheritdoc />
    public Process Start(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = CreateStartInfo(fileName, arguments);

        _logger.LogDebug("Starting {FileName} {Arguments}", fileName, string.Join(' ', arguments));

        var process = new Process { StartInfo = startInfo };
        StartProcess(process, fileName);

        return process;
    }

    private static void StartProcess(Process process, string fileName)
    {
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new ProcessLaunchException(fileName, exception);
        }
    }
}
