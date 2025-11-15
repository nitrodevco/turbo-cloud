using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Turbo.Contracts.Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Players;

namespace Turbo.Players.Grains;

public class PlayerGrain(
    [PersistentState(OrleansStateNames.PLAYER_SNAPSHOT, OrleansStorageNames.PLAYER_STORE)]
        IPersistentState<PlayerSnapshot> inner,
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<PlayerGrain> logger
) : Grain, IPlayerGrain
{
    private readonly IPersistentState<PlayerSnapshot> _state = inner;
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<PlayerGrain> _logger = logger;

    public Task<long> GetPlayerIdAsync() => Task.FromResult(this.GetPrimaryKeyLong());

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateFromExternalAsync(ct);

            _logger.LogInformation("PlayerGrain {PlayerId} activated.", this.GetPrimaryKeyLong());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error activating PlayerGrain:{PlayerId}",
                this.GetPrimaryKeyLong()
            );

            throw;
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        try
        {
            await WriteToDatabaseAsync(ct);

            _logger.LogInformation("PlayerGrain {PlayerId} deactivated.", this.GetPrimaryKeyLong());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deactivating PlayerGrain:{PlayerId}",
                this.GetPrimaryKeyLong()
            );

            throw;
        }
    }

    protected async Task HydrateFromExternalAsync(CancellationToken ct)
    {
        if (_state.RecordExists)
            return;

        using var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            var entity =
                await dbCtx
                    .Players.AsNoTracking()
                    .SingleOrDefaultAsync(e => e.Id == this.GetPrimaryKeyLong(), ct)
                ?? throw new Exception($"Player not found");

            _state.State = new PlayerSnapshot
            {
                PlayerId = this.GetPrimaryKeyLong(),
                Name = entity.Name ?? string.Empty,
                Motto = entity.Motto ?? string.Empty,
                Figure = entity.Figure ?? string.Empty,
                Gender = entity.Gender,
                CreatedAt = entity.CreatedAt,
            };

            await _state.WriteStateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error hydrating PlayerGrain {PlayerId} from database.",
                this.GetPrimaryKeyLong()
            );

            throw;
        }
    }

    protected async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        var snapshot = await GetSnapshotAsync(ct);

        using var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            await dbCtx
                .Players.Where(p => p.Id == this.GetPrimaryKeyLong())
                .ExecuteUpdateAsync(
                    up =>
                        up.SetProperty(p => p.Name, snapshot.Name)
                            .SetProperty(p => p.Motto, snapshot.Motto)
                            .SetProperty(p => p.Figure, snapshot.Figure)
                            .SetProperty(p => p.Gender, snapshot.Gender),
                    ct
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error writing PlayerGrain {PlayerId} to database.",
                this.GetPrimaryKeyLong()
            );

            throw;
        }
    }

    public ValueTask<PlayerSnapshot> GetSnapshotAsync(CancellationToken ct) =>
        ValueTask.FromResult(
            new PlayerSnapshot
            {
                PlayerId = this.GetPrimaryKeyLong(),
                Name = _state.State.Name,
                Motto = _state.State.Motto,
                Figure = _state.State.Figure,
                Gender = _state.State.Gender,
                CreatedAt = _state.State.CreatedAt,
            }
        );
}
