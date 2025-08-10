using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;

namespace Turbo.Grains.Shared;

public abstract class BaseGrain<TState, TEvent> : Grain, IAutoFlushGrain
    where TState : class
    where TEvent : class
{
    protected IAsyncStream<TEvent> _stream = default!;
    protected readonly AutoDirtyState<TState> _state;
    protected readonly ILogger _logger;

    protected BaseGrain(
        IPersistentState<TState> state,
        ILogger<BaseGrain<TState, TEvent>> logger)
    {
        _state = new AutoDirtyState<TState>(state);
        _logger = logger;
    }

    public bool IsDirty => _state.IsDirty;

    public void AcceptChanges() => _state.AcceptChanges();
    public object GetTrackedState() => _state.State;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await SetupStreamAsync();
    }

    protected virtual bool ValidateState(TState state)
    {
        return state != null;
    }

    public abstract Task FlushExternalAsync(CancellationToken ct);

    protected abstract Task SetupStreamAsync();
}