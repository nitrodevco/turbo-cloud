using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> CreateAvatarFromPlayerAsync(
        ActionContext ctx,
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    );
    public Task<bool> RemoveAvatarFromPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    );
    public Task<bool> WalkAvatarToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    );
    public Task<bool> UpdateAvatarWithPlayerAsync(
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    );
    public Task<bool> SetAvatarDanceAsync(
        ActionContext ctx,
        AvatarDanceType danceType,
        CancellationToken ct
    );
    public Task<bool> SetAvatarExpressionAsync(
        ActionContext ctx,
        AvatarExpressionType expressionType,
        CancellationToken ct
    );
    public Task SendChatFromPlayerAsync(
        PlayerId playerId,
        string text,
        AvatarGestureType gesture,
        int styleId,
        List<(string, string, bool)> links,
        int trackingId
    );

    public Task<ImmutableArray<RoomAvatarSnapshot>> GetAllAvatarSnapshotsAsync(
        CancellationToken ct
    );
}
