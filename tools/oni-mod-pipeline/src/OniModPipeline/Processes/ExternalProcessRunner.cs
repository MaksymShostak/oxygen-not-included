using System.ComponentModel;
using System.Diagnostics;

namespace MaksymShostak.OniModPipeline.Processes;

internal sealed class ExternalProcessRunner : IExternalProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);
        ArgumentNullException.ThrowIfNull(request.Arguments);
        ArgumentNullException.ThrowIfNull(request.EnvironmentVariables);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = request.StandardInput is not null,
            CreateNoWindow = true
        };
        if (request.StandardInput is not null)
        {
            startInfo.StandardInputEncoding = new System.Text.UTF8Encoding(false, true);
            startInfo.StandardOutputEncoding = new System.Text.UTF8Encoding(false, true);
            startInfo.StandardErrorEncoding = new System.Text.UTF8Encoding(false, true);
        }

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var pair in request.EnvironmentVariables)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Process '{request.FileName}' could not be started.");
        }

        var cancellationState = new ProcessCancellationState(process);
        var standardOutputTask = ReadOutputAsync(process.StandardOutput);
        var standardErrorTask = ReadOutputAsync(process.StandardError);
        using var cancellationRegistration = cancellationToken.Register(
            static state => ((ProcessCancellationState)state!).TerminateProcessTree(),
            cancellationState);

        try
        {
            if (request.StandardInput is { } input)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
                process.StandardInput.Close();
            }
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            await CompleteCancellationAsync(
                process,
                cancellationState,
                standardOutputTask,
                standardErrorTask,
                cancellationToken).ConfigureAwait(false);
            throw new OperationCanceledException(
                "External process execution was cancelled.",
                exception,
                cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            await CompleteCancellationAsync(
                process,
                cancellationState,
                standardOutputTask,
                standardErrorTask,
                cancellationToken).ConfigureAwait(false);
            throw new OperationCanceledException(cancellationToken);
        }

        await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
        var standardOutput = standardOutputTask.Result;
        var standardError = standardErrorTask.Result;
        return new ProcessResult(process.ExitCode, standardOutput, standardError);

        async Task<string> ReadOutputAsync(StreamReader reader)
        {
            var output = new System.Text.StringBuilder();
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) != 0)
            {
                if (request.OutputLimitCharacters is { } limit && output.Length > limit - count)
                {
                    cancellationState.TerminateProcessTree();
                    if (cancellationState.Failure is { } failure)
                        throw new InvalidDataException("Output overflow could not terminate the process tree.", failure);
                    // Drain after termination so neither redirected pipe can keep the child alive.
                    while (await reader.ReadAsync(buffer).ConfigureAwait(false) != 0) { }
                    throw new InvalidDataException("External process output exceeded its transport limit.");
                }
                output.Append(buffer, 0, count);
            }
            return output.ToString();
        }
    }

    private static async Task CompleteCancellationAsync(
        Process process,
        ProcessCancellationState cancellationState,
        Task<string> standardOutputTask,
        Task<string> standardErrorTask,
        CancellationToken cancellationToken)
    {
        cancellationState.TerminateProcessTree();
        if (cancellationState.Failure is { } failure)
        {
            throw new OperationCanceledException(
                "Cancellation was requested, but the process tree could not be terminated.",
                failure,
                cancellationToken);
        }

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
    }

    private sealed class ProcessCancellationState(Process process)
    {
        private readonly Lock synchronization = new();
        private Exception? failure;

        internal Exception? Failure
        {
            get
            {
                lock (synchronization)
                {
                    return failure;
                }
            }
        }

        internal void TerminateProcessTree()
        {
            lock (synchronization)
            {
                if (failure is not null)
                {
                    return;
                }

                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // The process exited between the state check and termination.
                }
                catch (Exception exception) when (
                    exception is Win32Exception or NotSupportedException)
                {
                    failure = exception;
                }
            }
        }
    }
}
