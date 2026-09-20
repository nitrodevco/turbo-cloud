using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
{
    /// <summary>
    /// Borrows a floor item from the Builders Club warehouse into the room the player is in.
    /// Unlike placing furni they own, nothing is taken out of an inventory: the room makes the
    /// furni from the offer and writes the row that lends it.
    /// </summary>
    public Task PlaceBuildersClubFloorItemInRoomAsync(
        ActionContext ctx,
        int pageId,
        int offerId,
        string extraParam,
        int x,
        int y,
        Rotation rot,
        bool confirmedHideRoom,
        CancellationToken ct
    );

    /// <summary>The wall counterpart, taking the location string the client sent.</summary>
    public Task PlaceBuildersClubWallItemInRoomAsync(
        ActionContext ctx,
        int pageId,
        int offerId,
        string extraParam,
        string location,
        bool confirmedHideRoom,
        CancellationToken ct
    );
}
