using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<IRoomAvatar> CreateAvatarFromPlayerAsync(
        ActionContext ctx,
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    ) => _avatarModule.CreateAvatarFromPlayerAsync(ctx, snapshot, ct);

    public Task RemoveAvatarFromPlayerAsync(long playerId, CancellationToken ct) =>
        _avatarModule.RemoveAvatarFromPlayerAsync(playerId, ct);

    public Task WalkAvatarToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    ) => _avatarModule.WalkAvatarToAsync(ctx, targetX, targetY, ct);

    public Task<ImmutableArray<RoomAvatarSnapshot>> GetAllAvatarSnapshotsAsync(
        CancellationToken ct
    ) => _avatarModule.GetAllAvatarSnapshotsAsync(ct);
}
