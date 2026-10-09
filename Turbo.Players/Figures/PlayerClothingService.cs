using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Messages.Outgoing.Inventory.Clothing;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Players.Figures;

/// <summary><see cref="IPlayerClothingService"/>, kept in <c>player_figure_sets</c>.</summary>
internal sealed class PlayerClothingService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory
) : IPlayerClothingService
{
    public async Task<ImmutableHashSet<int>> GetOwnedAsync(PlayerId playerId, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var owned = await dbCtx
            .PlayerFigureSets.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId.Value)
            .Select(x => x.SetId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. owned];
    }

    public async Task<int> GrantAsync(
        PlayerId playerId,
        IEnumerable<int> setIds,
        CancellationToken ct
    )
    {
        var wanted = setIds.Where(x => x >= 0).Distinct().ToList();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var had = await dbCtx
            .PlayerFigureSets.Where(x =>
                x.PlayerEntityId == playerId.Value && wanted.Contains(x.SetId)
            )
            .Select(x => x.SetId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var added = wanted.Except(had).ToList();

        if (added.Count == 0)
            return 0;

        dbCtx.PlayerFigureSets.AddRange(
            added.Select(x => new PlayerFigureSetEntity
            {
                PlayerEntityId = playerId.Value,
                SetId = x,
            })
        );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await SendOwnedAsync(playerId, ct).ConfigureAwait(false);

        return added.Count;
    }

    public async Task<ImmutableArray<string>> GetBoundFurnitureNamesAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var names = await dbCtx
            .PlayerBoundClothing.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId.Value)
            .OrderBy(x => x.Id)
            .Select(x => x.FurnitureDefinitionEntity!.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. names];
    }

    public async Task BindFurnitureAsync(
        PlayerId playerId,
        int definitionId,
        IEnumerable<int> setIds,
        CancellationToken ct
    )
    {
        var wanted = setIds.Where(x => x >= 0).Distinct().ToList();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var had = await dbCtx
            .PlayerFigureSets.Where(x =>
                x.PlayerEntityId == playerId.Value && wanted.Contains(x.SetId)
            )
            .Select(x => x.SetId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        dbCtx.PlayerFigureSets.AddRange(
            wanted
                .Except(had)
                .Select(x => new PlayerFigureSetEntity
                {
                    PlayerEntityId = playerId.Value,
                    SetId = x,
                })
        );

        var bound = await dbCtx
            .PlayerBoundClothing.AnyAsync(
                x =>
                    x.PlayerEntityId == playerId.Value
                    && x.FurnitureDefinitionEntityId == definitionId,
                ct
            )
            .ConfigureAwait(false);

        if (!bound)
            dbCtx.PlayerBoundClothing.Add(
                new PlayerBoundClothingEntity
                {
                    PlayerEntityId = playerId.Value,
                    FurnitureDefinitionEntityId = definitionId,
                }
            );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await SendOwnedAsync(playerId, ct).ConfigureAwait(false);
    }

    public async Task<int> RevokeAsync(
        PlayerId playerId,
        IEnumerable<int> setIds,
        CancellationToken ct
    )
    {
        var unwanted = setIds.Distinct().ToList();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var removed = await dbCtx
            .PlayerFigureSets.Where(x =>
                x.PlayerEntityId == playerId.Value && unwanted.Contains(x.SetId)
            )
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        if (removed > 0)
        {
            await SendOwnedAsync(playerId, ct).ConfigureAwait(false);

            // What they wear may no longer be theirs: it comes off at once, not at their next change.
            await grainFactory.GetPlayerGrain(playerId).RefitFigureAsync(ct).ConfigureAwait(false);
        }

        return removed;
    }

    public async Task SendOwnedAsync(PlayerId playerId, CancellationToken ct) =>
        await grainFactory
            .TrySendComposerToPlayerAsync(
                playerId,
                new FigureSetIdsEventMessageComposer
                {
                    FigureSetIds =
                    [
                        .. (await GetOwnedAsync(playerId, ct).ConfigureAwait(false)).Order(),
                    ],
                    BoundFurnitureNames = await GetBoundFurnitureNamesAsync(playerId, ct)
                        .ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);
}
