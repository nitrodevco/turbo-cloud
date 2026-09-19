using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Providers;

namespace Turbo.Rooms.Providers;

internal sealed class RoomNpcProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory
) : IRoomNpcProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async Task<IReadOnlyList<PetSnapshot>> LoadPetsByRoomIdAsync(
        RoomId roomId,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .Pets.AsNoTracking()
            .Where(x => x.RoomEntityId == roomId.Value)
            .ToListAsync(ct);

        var ownerNames = await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync(
                entities.Select(x => (PlayerId)x.PlayerEntityId).Distinct().ToList(),
                ct
            );

        return entities
            .Select(x =>
                x.ToSnapshot(
                    ownerNames.TryGetValue(x.PlayerEntityId, out var name) ? name : string.Empty
                )
            )
            .ToList();
    }

    public async Task<IReadOnlyList<BotSnapshot>> LoadBotsByRoomIdAsync(
        RoomId roomId,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .Bots.AsNoTracking()
            .Where(x => x.RoomEntityId == roomId.Value)
            .ToListAsync(ct);

        var ownerNames = await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync(
                entities.Select(x => (PlayerId)x.PlayerEntityId).Distinct().ToList(),
                ct
            );

        return entities
            .Select(x =>
                x.ToSnapshot(
                    ownerNames.TryGetValue(x.PlayerEntityId, out var name) ? name : string.Empty
                )
            )
            .ToList();
    }
}
