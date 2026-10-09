using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<RoomEntryAccessType> CheckEntryAccessAsync(
        PlayerId playerId,
        string? password,
        bool bypassDoor,
        CancellationToken ct
    )
    {
        try
        {
            return await EntryModule.CheckAccessAsync(playerId, password, bypassDoor);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to check entry access for player {PlayerId} in room {RoomId}",
                playerId,
                _state.RoomId
            );

            throw;
        }
    }

    public async Task<bool> RingDoorbellAsync(
        PlayerId playerId,
        string playerName,
        CancellationToken ct
    )
    {
        try
        {
            return await EntryModule.RingDoorbellAsync(playerId, playerName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to ring doorbell for player {PlayerId} in room {RoomId}",
                playerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<PlayerId?> AnswerDoorbellAsync(
        ActionContext ctx,
        string playerName,
        bool accepted,
        CancellationToken ct
    )
    {
        try
        {
            return await EntryModule.AnswerDoorbellAsync(ctx, playerName, accepted, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to answer doorbell for {PlayerName} (accepted:{Accepted}) by {PlayerId} in room {RoomId}",
                playerName,
                accepted,
                ctx.PlayerId,
                _state.RoomId
            );

            return null;
        }
    }

    public Task<RoomEntryViewSnapshot> GetEntryViewAsync(PlayerId playerId, CancellationToken ct) =>
        Task.FromResult(
            new RoomEntryViewSnapshot
            {
                Room = _state.RoomSnapshot,
                Map = MapModule.GetMapSnapshot(ct),
                OwnerNames = FurniModule.GetOwnerNames(),
                FloorItems = FurniModule.GetShownFloorItemSnapshots(),
                WallItems = FurniModule.GetShownWallItemSnapshots(),
                Avatars = AvatarModule.GetAvatarSnapshots(),
                Properties = [.. _state.RoomProperties],
                CanRate = CanRate(playerId),
                ActiveEvent = GetActiveEvent(),
                IsMuted = _state.IsRoomMuted,
                AreaHides =
                [
                    .. FurniModule
                        .Items.Select(x => (x.Logic as FurnitureAreaHideLogic)?.GetActiveArea())
                        .OfType<AreaHideDataSnapshot>(),
                ],
                InvisibleFurni = FurnitureInvisibleFurniControlLogic.IsInvisibleFurniOn(
                    FurniModule
                ),
            }
        );

    public Task RemoveDoorbellRingerAsync(PlayerId playerId, CancellationToken ct)
    {
        EntryModule.RemoveDoorbellRinger(playerId);

        return Task.CompletedTask;
    }
}
