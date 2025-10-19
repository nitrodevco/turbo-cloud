using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Context;
using Turbo.Primitives.Grains;
using Turbo.Primitives.Snapshots.Players;

namespace Turbo.Grains.Players;

public class PlayerGrain(
    [PersistentState("snap", "player-snapshots")] IPersistentState<PlayerSnapshot> inner,
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<PlayerGrain> logger
) : Grain, IPlayerGrain
{
    private readonly IPersistentState<PlayerSnapshot> _state = inner;
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<PlayerGrain> _logger = logger;

    public long PlayerId => this.GetPrimaryKeyLong();

    public Task<long> GetPlayerIdAsync() => Task.FromResult(PlayerId);

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateFromExternalAsync(ct);

            _logger.LogInformation("PlayerGrain {PlayerId} activated.", PlayerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating PlayerGrain {PlayerId}", PlayerId);

            throw;
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        try
        {
            await WriteToDatabaseAsync(ct);
            _logger.LogInformation("PlayerGrain {PlayerId} deactivated.", PlayerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating PlayerGrain {PlayerId}", PlayerId);

            throw;
        }
    }

    protected async Task HydrateFromExternalAsync(CancellationToken ct)
    {
        if (_state.RecordExists)
            return;

        TurboDbContext? dbCtx = null;

        try
        {
            dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

            var entity = await dbCtx
                .Players.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == PlayerId, ct);

            if (entity is null)
            {
                throw new Exception($"Player with ID {PlayerId} not found in database.");
            }
            else
            {
                _state.State = new PlayerSnapshot
                {
                    PlayerId = PlayerId,
                    Name = entity.Name ?? string.Empty,
                    Motto = entity.Motto ?? string.Empty,
                    Figure = entity.Figure ?? string.Empty,
                    Gender = entity.Gender,
                    CreatedAt = entity.CreatedAt,
                };

                await _state.WriteStateAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hydrating PlayerGrain {PlayerId} from database.", PlayerId);

            throw;
        }
        finally
        {
            if (dbCtx is not null)
                await dbCtx.DisposeAsync();
        }
    }

    protected async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        TurboDbContext? dbCtx = null;

        try
        {
            dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);
            var snapshot = await GetSnapshotAsync(ct);

            await dbCtx
                .Players.Where(p => p.Id == PlayerId)
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
            _logger.LogError(ex, "Error writing PlayerGrain {PlayerId} to database.", PlayerId);

            throw;
        }
        finally
        {
            if (dbCtx is not null)
                await dbCtx.DisposeAsync();
        }
    }

    public ValueTask<PlayerSnapshot> GetSnapshotAsync(CancellationToken ct) =>
        ValueTask.FromResult(
            new PlayerSnapshot
            {
                PlayerId = PlayerId,
                Name = _state.State.Name,
                Motto = _state.State.Motto,
                Figure = _state.State.Figure,
                Gender = _state.State.Gender,
                CreatedAt = _state.State.CreatedAt,
            }
        );
}
