using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms;

/// <summary>
/// Checks that the room and the player are there, then has the room grain make the change, so a
/// missing room is never activated (its activation throws) and the room alone writes its state.
/// </summary>
internal sealed class RoomOwnershipService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    ILogger<RoomOwnershipService> logger
) : IRoomOwnershipService
{
    public async Task<RoomOwnerTransferOutcome> TransferAsync(
        RoomId roomId,
        PlayerId newOwnerId,
        CancellationToken ct
    )
    {
        if (roomId.Value <= 0)
            return RoomOwnerTransferOutcome.NoSuchRoom;

        if (newOwnerId.Value <= 0)
            return RoomOwnerTransferOutcome.NoSuchPlayer;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = dbCtx.ConfigureAwait(false);

        var ownerId = await dbCtx
            .Rooms.AsNoTracking()
            .Where(x => x.Id == roomId.Value)
            .Select(x => (int?)x.PlayerEntityId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (ownerId is null)
            return RoomOwnerTransferOutcome.NoSuchRoom;

        var name = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Id == newOwnerId.Value)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (name is null)
            return RoomOwnerTransferOutcome.NoSuchPlayer;

        if (ownerId == newOwnerId.Value)
            return RoomOwnerTransferOutcome.AlreadyOwner;

        var outcome = await grainFactory
            .GetRoomGrain(roomId)
            .TransferOwnershipAsync(newOwnerId, name, ct)
            .ConfigureAwait(false);

        if (outcome == RoomOwnerTransferOutcome.Transferred)
            logger.LogInformation(
                "Room {RoomId} was given by player {FormerOwnerId} to player {OwnerId}",
                roomId,
                ownerId,
                newOwnerId
            );

        return outcome;
    }
}
