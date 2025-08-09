using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Orleans.Runtime;

namespace Turbo.Grains.Shared;

public abstract class DatabaseGrain<TState, TDbContext> : Grain, IAutoFlushGrain
    where TState : class
    where TDbContext : DbContext
{
    protected readonly IDbContextFactory<TDbContext> _dbContextFactory;
    protected readonly AutoDirtyState<TState> _state;

    protected DatabaseGrain(
        IDbContextFactory<TDbContext> dbContextFactory,
        IPersistentState<TState> state)
    {
        _dbContextFactory = dbContextFactory;
        _state = new AutoDirtyState<TState>(state);
    }

    public bool IsDirty => _state.IsDirty;
    public void AcceptChanges() => _state.AcceptChanges();
    public object GetTrackedState() => _state.State;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await HydrateFromDatabaseAsync(ct);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        await FlushExternalAsync(ct); // safety net; filter will handle normal calls
    }

    // Called by the filter (and by OnDeactivateAsync)
    public async Task FlushExternalAsync(CancellationToken ct)
    {
        if (!IsDirty) return;

        await WriteToDatabaseAsync(ct);
    }

    // --- abstract hooks for your derived grains ---
    protected abstract Task HydrateFromDatabaseAsync(CancellationToken ct);
    protected abstract Task WriteToDatabaseAsync(CancellationToken ct);
}