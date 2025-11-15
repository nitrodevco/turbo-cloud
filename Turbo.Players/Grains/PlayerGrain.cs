using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Orleans.Runtime;
using Turbo.Contracts.Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Orleans.States.Players;
using Turbo.Primitives.Players;

namespace Turbo.Players.Grains;

public class PlayerGrain(
    [PersistentState(OrleansStateNames.PLAYER_STATE, OrleansStorageNames.PLAYER_STORE)]
        IPersistentState<PlayerState> state,
    IDbContextFactory<TurboDbContext> dbContextFactory
) : Grain, IPlayerGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await HydrateFromExternalAsync(ct);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        await WriteToDatabaseAsync(ct);
    }

    protected async Task HydrateFromExternalAsync(CancellationToken ct)
    {
        if (state.State.IsLoaded)
            return;

        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            var entity =
                await dbCtx
                    .Players.AsNoTracking()
                    .SingleOrDefaultAsync(e => e.Id == this.GetPrimaryKeyLong(), ct)
                ?? throw new Exception(
                    $"PlayerGrain:{this.GetPrimaryKeyLong()} not found in external database"
                );

            state.State.Name = entity.Name ?? string.Empty;
            state.State.Motto = entity.Motto ?? string.Empty;
            state.State.Figure = entity.Figure ?? string.Empty;
            state.State.Gender = entity.Gender;
            state.State.CreatedAt = entity.CreatedAt;
            state.State.IsLoaded = true;
            state.State.LastUpdated = DateTime.UtcNow;

            await state.WriteStateAsync(ct);
        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
            await dbCtx.DisposeAsync();
        }
    }

    protected async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            var snapshot = await GetSummaryAsync(ct);

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
            throw;
        }
        finally
        {
            await dbCtx.DisposeAsync();
        }
    }

    public Task<PlayerSummarySnapshot> GetSummaryAsync(CancellationToken ct) =>
        Task.FromResult(
            new PlayerSummarySnapshot
            {
                PlayerId = this.GetPrimaryKeyLong(),
                Name = state.State.Name,
                Motto = state.State.Motto,
                Figure = state.State.Figure,
                Gender = state.State.Gender,
                CreatedAt = state.State.CreatedAt,
            }
        );
}
