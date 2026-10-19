using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
{
    public Task PlaceWallItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    );

    /// <summary>
    /// A sheet of the player's post-it pad stuck to a wall in the room they are in; a refusal is
    /// told as a placement's is.
    /// </summary>
    public Task PlacePostItInRoomAsync(
        ActionContext ctx,
        RoomObjectId padId,
        string location,
        CancellationToken ct
    );
    public Task MoveWallItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    );
}
