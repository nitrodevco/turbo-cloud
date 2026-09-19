using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

/// <summary>
/// The room's side of trading, called by <see cref="IRoomTradeGrain"/> only. The room knows who
/// stands where and what its trade mode allows; the trade itself is not room state.
/// </summary>
public partial interface IRoomGrain
{
    /// <summary>
    /// Resolves the avatar the initiator clicked into a trade partner. Null when either side is
    /// not a player in the room, or they are the same player.
    /// </summary>
    public Task<TradePartiesSnapshot?> GetTradePartiesAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId,
        CancellationToken ct
    );

    /// <summary>Shows or clears the trading status on the avatars that are still in the room.</summary>
    public Task SetTradingStatusAsync(
        ImmutableArray<PlayerId> playerIds,
        bool trading,
        CancellationToken ct
    );
}
