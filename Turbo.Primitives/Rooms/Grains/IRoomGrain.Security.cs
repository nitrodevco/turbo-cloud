using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<RoomControllerType> GetControllerLevelAsync(
        PlayerId playerId,
        CancellationToken ct
    );
    public Task RefreshControllerLevelForPlayerAsync(ActionContext ctx, CancellationToken ct);
    public Task GiveRightsToPlayerAsync(ActionContext ctx, PlayerId playerId, CancellationToken ct);
    public Task RemoveRightsFromPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    );
}
