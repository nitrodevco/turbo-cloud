using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Configuration;
using Turbo.Operations.Movements;
using Turbo.Primitives.Movements.Grains;

namespace Turbo.Operations.Grains;

/// <summary>
/// The asset movement outbox's dispatcher, one grain for the hotel: every
/// <see cref="MovementsConfig.PollMs"/> it runs one <see cref="AssetMovementDispatcher"/> poll.
/// It polls only while a consumer is registered: a poll that finds none stops the timer and lets
/// the grain be collected, and the next registration wakes it. Nothing it holds is written back
/// (each consumer stores its own cursor), so there is nothing to flush on deactivation; a second
/// activation, which Orleans may briefly run, only delivers rows twice.
/// </summary>
internal sealed class AssetMovementDispatcherGrain : Grain, IAssetMovementDispatcherGrain
{
    private readonly MovementsConfig _config;
    private readonly AssetMovementDispatcher _dispatcher;
    private readonly ILogger<IAssetMovementDispatcherGrain> _logger;

    private IDisposable? _pollTimer;

    public AssetMovementDispatcherGrain(
        IOptions<MovementsConfig> config,
        AssetMovementDispatcher dispatcher,
        ILogger<IAssetMovementDispatcherGrain> logger
    )
    {
        _config = config.Value;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        StopPolling();

        return Task.CompletedTask;
    }

    public Task WakeAsync(CancellationToken ct)
    {
        if (_pollTimer is not null)
            return Task.CompletedTask;

        _pollTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((AssetMovementDispatcherGrain)self!).PollAsync(ct),
            this,
            new GrainTimerCreationOptions
            {
                DueTime = TimeSpan.Zero,
                Period = TimeSpan.FromMilliseconds(_config.PollMs),
            }
        );

        PinWhilePolling();

        return Task.CompletedTask;
    }

    private async Task PollAsync(CancellationToken ct)
    {
        try
        {
            if (await _dispatcher.DispatchAsync(ct))
            {
                PinWhilePolling();

                return;
            }

            // Nobody to deliver to: stop reading until a registration wakes the grain again.
            StopPolling();
            DelayDeactivation(TimeSpan.Zero);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to dispatch the asset movement outbox");
        }
    }

    /// <summary>
    /// Two poll periods (<c>DelayDeactivation</c>), so the grain is not collected between polls
    /// whatever <see cref="MovementsConfig.PollMs"/> is, nor after one slow poll.
    /// </summary>
    private void PinWhilePolling() =>
        DelayDeactivation(TimeSpan.FromMilliseconds(2L * _config.PollMs));

    private void StopPolling()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }
}
