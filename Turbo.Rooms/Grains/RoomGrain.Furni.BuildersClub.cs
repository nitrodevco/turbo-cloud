using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> PlaceBuildersClubFloorItemAsync(
        ActionContext ctx,
        int pageId,
        int offerId,
        string extraParam,
        int x,
        int y,
        Rotation rot,
        bool confirmedHideRoom,
        CancellationToken ct
    )
    {
        try
        {
            return await FurniModule.PlaceBuildersClubFloorItemAsync(
                ctx,
                pageId,
                offerId,
                extraParam,
                x,
                y,
                rot,
                confirmedHideRoom,
                ct
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to borrow offer {OfferId} into room {RoomId} for player {PlayerId}",
                offerId,
                _state.RoomId,
                ctx.PlayerId
            );

            return false;
        }
    }

    public async Task<bool> PlaceBuildersClubWallItemAsync(
        ActionContext ctx,
        int pageId,
        int offerId,
        string extraParam,
        string location,
        bool confirmedHideRoom,
        CancellationToken ct
    )
    {
        if (!WallPosition.TryParse(location, out var position))
            return false;

        try
        {
            return await FurniModule.PlaceBuildersClubWallItemAsync(
                ctx,
                pageId,
                offerId,
                extraParam,
                location,
                position,
                confirmedHideRoom,
                ct
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to borrow offer {OfferId} onto the wall of room {RoomId} for player {PlayerId}",
                offerId,
                _state.RoomId,
                ctx.PlayerId
            );

            return false;
        }
    }

    /// <summary>
    /// Hides the room from the navigator, or shows it again, because of Builders Club furni.
    /// A hidden room is also closed to everyone but its owner
    /// (<see cref="Modules.RoomEntryModule.CheckAccessAsync"/>). The door mode is deliberately
    /// left alone: the owner's own choice of door is theirs, and overwriting it would give
    /// nothing to put back when the membership is renewed.
    /// </summary>
    public async Task SetHiddenByBuildersClubAsync(bool hidden, CancellationToken ct)
    {
        var current = _state.RoomSnapshot;

        if (current.HiddenByBc == hidden)
            return;

        var next = current with { HiddenByBc = hidden };

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var entity = await dbCtx.Rooms.FirstOrDefaultAsync(
                x => x.Id == _state.RoomId.Value,
                ct
            );

            if (entity is null)
                return;

            entity.HiddenByBc = hidden;

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to set the Builders Club visibility of room {RoomId} to {Hidden}",
                _state.RoomId,
                hidden
            );

            return;
        }

        _logger.LogInformation(
            "Room {RoomId} is {Visibility} because of Builders Club furni",
            _state.RoomId,
            hidden ? "hidden" : "visible again"
        );

        // The navigator listing has to be republished either way, because what changed is
        // whether the room may appear in it at all.
        await ApplySettingsAsync(current, next, ct);

        await _grainFactory.SendComposerToPlayerAsync(
            next.OwnerId,
            new NotificationDialogMessageComposer
            {
                NotificationType = hidden
                    ? BuildersClubNotifications.ROOM_LOCKED
                    : BuildersClubNotifications.ROOM_UNLOCKED,
            },
            ct
        );
    }
}
