using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>
    /// Applies a room paper from the acting player's inventory (a wallpaper, floor or
    /// landscape) to this room: the room's owner only. The pattern is saved and shown to
    /// everyone here, and the item is used up. False when it was refused.
    /// </summary>
    public Task<bool> ApplyDecorationAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    );
}
