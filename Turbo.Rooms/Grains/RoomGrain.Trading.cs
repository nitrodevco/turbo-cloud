using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<TradePartiesSnapshot?> GetTradePartiesAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId,
        CancellationToken ct
    )
    {
        AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

        return TradeModule.GetPartiesAsync(ctx, targetObjectId);
    }

    public Task SetTradingStatusAsync(
        ImmutableArray<PlayerId> playerIds,
        bool trading,
        CancellationToken ct
    )
    {
        TradeModule.SetTradingStatus(playerIds, trading);

        return Task.CompletedTask;
    }
}
