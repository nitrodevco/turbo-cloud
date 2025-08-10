using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace Turbo.Grains.Shared;

public abstract class DatabaseGrain<TState, TEvent, TDbContext> : BaseGrain<TState, TEvent>
    where TState : class
    where TEvent : class
    where TDbContext : DbContext
{
    protected readonly IDbContextFactory<TDbContext> _dbContextFactory;

    protected DatabaseGrain(
        IDbContextFactory<TDbContext> dbContextFactory,
        IPersistentState<TState> state,
        ILogger<DatabaseGrain<TState, TEvent, TDbContext>> logger) : base(state, logger)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await base.OnActivateAsync(ct);

        try
        {
            await HydrateFromDatabaseAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hydrating grain state from database.");

            throw;
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        try
        {
            await FlushExternalAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing grain state to database on deactivation.");
            throw;
        }
    }

    public override async Task FlushExternalAsync(CancellationToken ct)
    {
        if (!IsDirty) return;

        if (!ValidateState(_state.State))
        {
            _logger.LogWarning("State validation failed. Skipping database write.");
            return;
        }

        await ExecuteWithRetryAsync(() => WriteToDatabaseAsync(ct), ct);
    }

    protected async Task ExecuteWithRetryAsync(Func<Task> operation, CancellationToken ct, int maxRetries = 3)
    {
        int attempt = 0;

        while (true)
        {
            try
            {
                await operation();
                return;
            }
            catch (DbUpdateException ex) when (attempt < maxRetries)
            {
                attempt++;
                _logger.LogWarning(ex, $"Transient DB error, retrying (attempt {attempt})...");
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database operation.");
                throw;
            }
        }
    }

    protected abstract Task HydrateFromDatabaseAsync(CancellationToken ct);

    protected abstract Task WriteToDatabaseAsync(CancellationToken ct);
}