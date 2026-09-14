using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<RoomControllerType> GetControllerLevelAsync(
        PlayerId playerId,
        CancellationToken ct
    );

    /// <summary>
    /// Players with explicitly assigned rights, or null when the caller is below owner level.
    /// </summary>
    public Task<ImmutableArray<RoomControllerSnapshot>?> GetControllersAsync(
        ActionContext ctx,
        CancellationToken ct
    );
    public Task RefreshControllerLevelForPlayerAsync(ActionContext ctx, CancellationToken ct);
    public Task GiveRightsToPlayerAsync(ActionContext ctx, PlayerId playerId, CancellationToken ct);
    public Task RemoveRightsFromPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    );

    /// <summary>
    /// Revokes every explicitly assigned right in the room. Owner only.
    /// </summary>
    public Task RemoveAllRightsAsync(ActionContext ctx, CancellationToken ct);
}
