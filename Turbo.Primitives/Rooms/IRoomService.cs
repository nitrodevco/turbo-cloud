using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
{
    /// <summary>
    /// Activates the room and decides whether the player may enter. <paramref name="password"/>
    /// is only consulted for password doors; <paramref name="bypassDoor"/> is for server-driven
    /// moves (teleporters) that ignore the door mode but still honour bans and capacity.
    /// </summary>
    public Task<RoomEntryAccessType> CheckRoomEntryAccessAsync(
        PlayerId playerId,
        RoomId roomId,
        string? password,
        bool bypassDoor,
        CancellationToken ct
    );

    /// <summary>
    /// Acts on an access decision from <see cref="CheckRoomEntryAccessAsync"/>: opens the
    /// connection, then either streams the room, rings the doorbell, or reports the rejection.
    /// </summary>
    public Task OpenRoomForPlayerIdAsync(
        ActionContext ctx,
        PlayerId playerId,
        RoomId roomId,
        RoomEntryAccessType access,
        CancellationToken ct
    );

    /// <summary>
    /// A controller inside <c>ctx.RoomId</c> answered the doorbell for the named player.
    /// </summary>
    public Task AnswerDoorbellAsync(
        ActionContext ctx,
        string playerName,
        bool accepted,
        CancellationToken ct
    );
    public Task CloseRoomForPlayerAsync(PlayerId playerId, CancellationToken ct);
    public Task ClickTileAsync(ActionContext ctx, int targetX, int targetY, CancellationToken ct);
    public Task PickupItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        bool isConfirm = true
    );
    public Task UseItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    );
    public Task ClickItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    );
}
