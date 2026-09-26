using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredExecutionContext : IWiredContext
{
    /// <summary>
    /// Now, in the room wired timezone. Time and date based wired should read this.
    /// </summary>
    public DateTimeOffset RoomLocalTime { get; }
    public List<WiredUserMovementSnapshot> UserMoves { get; }
    public List<WiredFloorItemMovementSnapshot> FloorItemMoves { get; }
    public List<WiredWallItemMovementSnapshot> WallItemMoves { get; }
    public List<WiredUserDirectionSnapshot> UserDirections { get; }
    public List<(RoomObjectId, StuffDataSnapshot)> FloorItemStateUpdates { get; }
    public List<(RoomObjectId, string)> WallItemStateUpdates { get; }
    public Task ProcessItemStateUpdateAsync(IRoomItem item, int state);
    public Task<bool> ProcessFloorItemMovementAsync(
        IRoomFloorItem floorItem,
        int tileIdx,
        Altitude? z = null,
        Rotation? rotation = null
    );

    /// <summary>
    /// Moves a floor item to a tile if it may stand there: the one "move it if it fits" every
    /// wired mover uses. False when the spot is refused (off the map included) or the move is.
    /// </summary>
    public Task<bool> TryMoveFloorItemAsync(
        IRoomFloorItem floorItem,
        int x,
        int y,
        Altitude? z = null,
        Rotation? rotation = null
    );
    public Task ProcessWallItemMovementAsync(
        IRoomWallItem wallItem,
        int x,
        int y,
        Altitude z,
        Rotation rot,
        int wallOffset
    );

    /// <summary>
    /// Puts an avatar on another tile without walking. False when the tile cannot take it.
    /// </summary>
    public Task<bool> ProcessUserMovementAsync(
        IRoomAvatar avatar,
        int tileIdx,
        SlideAvatarMoveType moveType
    );

    /// <summary>Turns an avatar in place.</summary>
    public Task ProcessUserDirectionAsync(IRoomAvatar avatar, Rotation body, Rotation head);

    /// <summary>Runs the placeholder addons of the stack over text an action will show.</summary>
    public Task<string> FormatTextAsync(string text, CancellationToken ct);
    public ActionContext AsActionContext();
    public Task SendComposerToRoomAsync(IComposer composer);
}
