using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task RefreshControllerLevelForPlayerAsync(ActionContext ctx, CancellationToken ct);
    public Task GiveRightsToPlayerAsync(ActionContext ctx, PlayerId playerId, CancellationToken ct);
    public Task RemoveRightsFromPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    );
}
