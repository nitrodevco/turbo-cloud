using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Common;
using Turbo.Admin.Configuration;

namespace Turbo.Admin.Supervisor.Orleans;

/// <summary>
/// Owns the full lifecycle of the Orleans client: builds a fresh <see cref="IClusterClient"/>, tries to start
/// it, and on failure (or later disconnect) discards it and builds another one after a short delay - forever,
/// in the background. This never blocks the admin web host's own startup, so the panel is always reachable even
/// when the emulator (and its Orleans silo) isn't running.
/// </summary>
internal sealed class OrleansClientConnector(
    IConfiguration configuration,
    IOptions<AdminConfig> adminConfig,
    GrainClientHolder holder,
    OrleansConnectionState connectionState,
    ILogger<OrleansClientConnector> logger
) : IHostedService
{
    private readonly IConfiguration _configuration = configuration;
    private readonly int _gatewayPort = adminConfig.Value.OrleansGatewayPort;
    private readonly GrainClientHolder _holder = holder;
    private readonly OrleansConnectionState _connectionState = connectionState;
    private readonly ILogger<OrleansClientConnector> _logger = logger;
    private CancellationTokenSource? _cts;

    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _ = RunAsync(_cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _cts?.Cancel();

        var client = _holder.Current;
        _holder.Current = null;
        _connectionState.IsConnected = false;

        if (client is null)
            return;

        try
        {
            await ((IHostedService)client).StopAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing Orleans client during shutdown.");
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        // This runs detached (fire-and-forget from StartAsync), so nothing may ever throw out of this
        // method - an unobserved exception here would silently kill retries forever with no crash and no
        // further log output. The outer try/catch is a last-resort safety net around the per-attempt one.
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await RunSingleAttemptAsync(ct).ConfigureAwait(false);

                if (ct.IsCancellationRequested)
                    return;

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Orleans client connector loop crashed unexpectedly and has stopped retrying."
            );
        }
    }

    private async Task RunSingleAttemptAsync(CancellationToken ct)
    {
        var lostTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OrleansClientHandle? handle = null;

        try
        {
            handle = OrleansClientFactory.Create(
                _configuration,
                _gatewayPort,
                () => lostTcs.TrySetResult()
            );

            await ((IHostedService)handle.Client).StartAsync(ct).ConfigureAwait(false);

            _holder.Current = handle.Client;
            _connectionState.IsConnected = true;
            _logger.LogInformation("Connected to the emulator's Orleans cluster.");

            using var ctReg = ct.Register(() => lostTcs.TrySetResult());
            await lostTcs.Task.ConfigureAwait(false);

            _connectionState.IsConnected = false;
            _holder.Current = null;

            if (!ct.IsCancellationRequested)
                _logger.LogWarning("Lost connection to the emulator's Orleans cluster.");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "Waiting for the emulator's Orleans cluster to become reachable…"
            );
        }
        finally
        {
            handle?.Dispose();
        }
    }
}
