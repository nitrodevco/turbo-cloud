using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms;

internal sealed partial class RoomService
{
    public async Task PlaceBuildersClubFloorItemInRoomAsync(
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
        if (
            ctx.Origin != ActionOrigin.Player
            || ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || offerId <= 0
        )
            return;

        try
        {
            await _grainFactory
                .GetRoomGrain(ctx.RoomId)
                .PlaceBuildersClubFloorItemAsync(
                    ctx,
                    pageId,
                    offerId,
                    extraParam,
                    x,
                    y,
                    rot,
                    confirmedHideRoom,
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to borrow offer {OfferId} into room {RoomId} for player {PlayerId}",
                offerId,
                ctx.RoomId,
                ctx.PlayerId
            );
        }
    }

    public async Task PlaceBuildersClubWallItemInRoomAsync(
        ActionContext ctx,
        int pageId,
        int offerId,
        string extraParam,
        string location,
        bool confirmedHideRoom,
        CancellationToken ct
    )
    {
        if (
            ctx.Origin != ActionOrigin.Player
            || ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || offerId <= 0
        )
            return;

        try
        {
            await _grainFactory
                .GetRoomGrain(ctx.RoomId)
                .PlaceBuildersClubWallItemAsync(
                    ctx,
                    pageId,
                    offerId,
                    extraParam,
                    location,
                    confirmedHideRoom,
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to borrow offer {OfferId} onto the wall of room {RoomId} for player {PlayerId}",
                offerId,
                ctx.RoomId,
                ctx.PlayerId
            );
        }
    }
}
