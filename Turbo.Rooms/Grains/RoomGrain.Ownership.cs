using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    /// <summary>
    /// The room row and its rights change in one transaction, then the room: its snapshot (what
    /// GetGuestRoom and the navigator show), the owner's account level that sizes its wired cap,
    /// the listings that name the owner, and both players' controller levels, which tell a
    /// player standing in the room and the wired permissions they are shown. The client only
    /// ever sets its owner flag (<c>RoomPermissionsHandler.onYouAreOwner</c>), so a former owner
    /// standing in the room keeps their owner menus until they enter again; the room refuses
    /// what those menus ask.
    /// </summary>
    public async Task<RoomOwnerTransferOutcome> TransferOwnershipAsync(
        PlayerId newOwnerId,
        string newOwnerName,
        CancellationToken ct
    )
    {
        if (_state.IsDeleting)
            return RoomOwnerTransferOutcome.Deleting;

        var current = _state.RoomSnapshot;
        var formerOwnerId = current.OwnerId;

        if (formerOwnerId == newOwnerId)
            return RoomOwnerTransferOutcome.AlreadyOwner;

        // A group home room's rights are the group's, never room_rights rows, so the former
        // owner is given none there either way.
        var isGroupRoom = await GetGuildAsync(ct) is not null;

        if (isGroupRoom && !_roomConfig.TransferGroupHomeRooms)
            return RoomOwnerTransferOutcome.GroupHomeRoom;

        await SecurityModule.EnsureRightsLoadedAsync(ct);

        var formerOwnerGetsRights =
            _roomConfig.TransferGivesFormerOwnerRights
            && !isGroupRoom
            && formerOwnerId.Value > 0
            && !_state.PlayerIdsWithRights.Contains(formerOwnerId);

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
            await using var tx = await dbCtx.Database.BeginTransactionAsync(ct);

            var changed = await dbCtx
                .Rooms.Where(x => x.Id == _state.RoomId.Value)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.PlayerEntityId, newOwnerId.Value),
                    ct
                );

            if (changed == 0)
                return RoomOwnerTransferOutcome.NoSuchRoom;

            await dbCtx
                .RoomRights.Where(x =>
                    x.RoomEntityId == _state.RoomId.Value && x.PlayerEntityId == newOwnerId.Value
                )
                .ExecuteDeleteAsync(ct);

            if (formerOwnerGetsRights)
                dbCtx.RoomRights.Add(
                    new RoomRightEntity
                    {
                        RoomEntityId = _state.RoomId.Value,
                        PlayerEntityId = formerOwnerId.Value,
                    }
                );

            await dbCtx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to give room {RoomId} to player {PlayerId}",
                _state.RoomId,
                newOwnerId
            );

            return RoomOwnerTransferOutcome.Failed;
        }

        var rightsChanged = new List<PlayerId>();

        if (_state.PlayerIdsWithRights.Remove(newOwnerId))
            rightsChanged.Add(newOwnerId);

        if (formerOwnerGetsRights)
        {
            _state.PlayerIdsWithRights.Add(formerOwnerId);
            rightsChanged.Add(formerOwnerId);
        }

        _state.RoomSnapshot = current with { OwnerId = newOwnerId, OwnerName = newOwnerName };
        _state.OwnerAccountLevel = await GetOwnerAccountLevelAsync(ct);

        await PublishRoomInfoUpdatedAsync(ct);

        // Every listing row names the owner, and both owners' room lists and counts moved.
        await _grainFactory
            .GetRoomDirectoryGrain()
            .PublishListingChangesAsync(
                [
                    NavigatorListingKeys.Room(_state.RoomId),
                    NavigatorListingKeys.Owner(formerOwnerId),
                    NavigatorListingKeys.Owner(newOwnerId),
                    NavigatorListingKeys.OwnerRoomCount(formerOwnerId),
                    NavigatorListingKeys.OwnerRoomCount(newOwnerId),
                    NavigatorListingKeys.Category(current.CategoryId),
                    NavigatorListingKeys.HIGHEST_SCORED,
                    NavigatorListingKeys.STAFF_PICKS,
                    NavigatorListingKeys.SEARCH,
                    NavigatorListingKeys.TAGS,
                    NavigatorListingKeys.EVENTS,
                    .. rightsChanged.Select(NavigatorListingKeys.Rights),
                ],
                ct
            );

        if (formerOwnerId.Value > 0)
            await SecurityModule.RefreshControllerLevelForPlayerAsync(formerOwnerId, ct);

        await SecurityModule.RefreshControllerLevelForPlayerAsync(newOwnerId, ct);

        return RoomOwnerTransferOutcome.Transferred;
    }
}
