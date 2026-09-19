using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
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

    /// <summary>Turns the player's avatar to face a tile without walking.</summary>
    public Task<bool> LookToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    );

    /// <summary>Hands the carried item to an adjacent player.</summary>
    public Task<bool> PassHandItemAsync(ActionContext ctx, PlayerId targetId, CancellationToken ct);
    public Task<bool> DropHandItemAsync(ActionContext ctx, CancellationToken ct);

    /// <summary>A player clicked another avatar; feeds the "user clicks user" wired trigger.</summary>
    public Task<bool> ClickAvatarAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId,
        CancellationToken ct
    );

    /// <summary>Spends one of the giver's daily respects on a player in the room.</summary>
    public Task<bool> RespectPlayerAsync(
        ActionContext ctx,
        PlayerId targetId,
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
    public Task<bool> SetAvatarEffectAsync(ActionContext ctx, int effectId, CancellationToken ct);
    public Task<bool> SetAvatarExpressionAsync(
        ActionContext ctx,
        AvatarExpressionType expressionType,
        CancellationToken ct
    );
    public Task<bool> SetAvatarSignAsync(ActionContext ctx, int signType, CancellationToken ct);
    public Task<bool> SetAvatarPostureAsync(
        ActionContext ctx,
        AvatarPostureType postureType,
        CancellationToken ct
    );
    public Task<ImmutableArray<RoomAvatarSnapshot>> GetAllAvatarSnapshotsAsync(
        CancellationToken ct
    );
}
