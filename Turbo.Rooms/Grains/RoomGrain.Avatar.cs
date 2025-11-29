using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task WalkAvatarToAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int targetX,
        int targetY,
        CancellationToken ct = default
    ) => _avatarModule.WalkAvatarToAsync(ctx, objectId, targetX, targetY, ct);
}
