using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task WalkAvatarToAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int targetX,
        int targetY,
        CancellationToken ct = default
    );
}
