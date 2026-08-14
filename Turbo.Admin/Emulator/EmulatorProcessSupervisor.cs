using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Admin.Configuration;
using Turbo.Admin.Terminal;

namespace Turbo.Admin.Emulator;

public sealed partial class EmulatorProcessSupervisor(
    IOptions<AdminConfig> config,
    IConsoleBroadcaster broadcaster,
    ILogger<EmulatorProcessSupervisor> logger
) : IEmulatorProcessSupervisor, IDisposable
{
    private readonly EmulatorProcessConfig _config = config.Value.Emulator;
    private readonly IConsoleBroadcaster _broadcaster = broadcaster;
    private readonly ILogger<EmulatorProcessSupervisor> _logger = logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Process? _process;

    public EmulatorStatus Status { get; private set; } = EmulatorStatus.Stopped;

    public async Task StartAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (Status is EmulatorStatus.Running or EmulatorStatus.Starting)
            {
                Publish("Emulator is already running.");
                return;
            }

            Status = EmulatorStatus.Starting;

            // A relative WorkingDirectory resolves against the OS-level directory this process was
            // launched from, which varies by launch method (dotnet run vs. running the built exe vs.
            // an IDE's configured working directory) and can silently point at a stale build output
            // with its own leftover appsettings.json - resolve and surface the absolute path up front
            // so a wrong config is obvious immediately instead of showing up as a mysterious runtime
            // failure downstream (e.g. the emulator loading unexpected ports/settings).
            var workingDirectory = Path.GetFullPath(_config.WorkingDirectory);

            Publish(
                $"Starting emulator process ({_config.ExecutablePath} {_config.Arguments}) in {workingDirectory}…"
            );

            if (!Directory.Exists(workingDirectory))
            {
                _logger.LogError(
                    "Emulator working directory does not exist: {WorkingDirectory}",
                    workingDirectory
                );
                Publish(
                    $"Failed to start emulator process: working directory not found ({workingDirectory})."
                );
                Status = EmulatorStatus.Stopped;
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _config.ExecutablePath,
                Arguments = _config.Arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    PublishChildLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    PublishChildLine(e.Data);
            };
            process.Exited += OnProcessExited;

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start emulator process.");
                Publish($"Failed to start emulator process: {ex.Message}");
                Status = EmulatorStatus.Stopped;
                return;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _process = process;
            Status = EmulatorStatus.Running;
            Publish($"Emulator process started (pid {process.Id}).");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var process = _process;

            if (process is null || Status is EmulatorStatus.Stopped or EmulatorStatus.Stopping)
            {
                Publish("Emulator is not running.");
                return;
            }

            Status = EmulatorStatus.Stopping;
            Publish("Stopping emulator process…");

            await StopProcessAsync(process, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RestartAsync(CancellationToken ct)
    {
        await StopAsync(ct).ConfigureAwait(false);
        await StartAsync(ct).ConfigureAwait(false);
    }

    public async Task SendInputAsync(string line, CancellationToken ct)
    {
        var process = _process;

        if (process is null || Status != EmulatorStatus.Running)
        {
            Publish("Cannot send input: emulator is not running.");
            return;
        }

        try
        {
            await process.StandardInput.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write to emulator process stdin.");
            Publish($"Failed to send input: {ex.Message}");
        }
    }

    private async Task StopProcessAsync(Process process, CancellationToken ct)
    {
        try
        {
            if (!process.HasExited)
            {
                await process
                    .StandardInput.WriteLineAsync(_config.GracefulShutdownCommand.AsMemory(), ct)
                    .ConfigureAwait(false);
                await process.StandardInput.FlushAsync(ct).ConfigureAwait(false);

                using var timeoutCts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(_config.GracefulShutdownTimeoutSeconds)
                );
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    ct,
                    timeoutCts.Token
                );

                try
                {
                    await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    Publish("Graceful shutdown timed out, killing process…");
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(ct).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping emulator process.");
            Publish($"Error while stopping emulator process: {ex.Message}");
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        var exitCode = _process?.ExitCode;

        Publish($"Emulator process exited (code {exitCode}).");
        Status = EmulatorStatus.Stopped;
        _process = null;
    }

    private void Publish(string line) => _broadcaster.Publish($"[supervisor] {line}");

    private void PublishChildLine(string line) => _broadcaster.Publish(StripAnsi(line));

    private static string StripAnsi(string line) => AnsiEscapeRegex().Replace(line, string.Empty);

    [GeneratedRegex(@"\x1B\[[0-9;]*[a-zA-Z]")]
    private static partial Regex AnsiEscapeRegex();

    public void Dispose()
    {
        _process?.Dispose();
        _gate.Dispose();
    }
}
