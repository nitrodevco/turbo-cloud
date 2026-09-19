using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Grains;

/// <summary>
/// The trades going on in one room, keyed by room id. It lives beside the room grain so the
/// inventory reads and the commit never hold up the room's turn.
/// </summary>
public interface IRoomTradeGrain : IGrainWithIntegerKey
{
    /// <summary>Opens a trade with the player standing at the given room object id.</summary>
    public Task<bool> OpenAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId,
        CancellationToken ct
    );

    public Task<bool> AddItemsAsync(
        ActionContext ctx,
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    );

    public Task<bool> RemoveItemAsync(ActionContext ctx, RoomObjectId itemId, CancellationToken ct);

    /// <summary>Accepts (or withdraws acceptance of) the current offers.</summary>
    public Task<bool> AcceptAsync(ActionContext ctx, bool accept, CancellationToken ct);

    /// <summary>The final confirmation after both sides accepted; declining reopens the offers.</summary>
    public Task<bool> ConfirmAsync(ActionContext ctx, bool accept, CancellationToken ct);

    public Task<bool> CloseAsync(ActionContext ctx, CancellationToken ct);

    /// <summary>
    /// A party left the room; whatever trade they had ends for both. The room grain fires this
    /// without awaiting it, because this grain calls the room back.
    /// </summary>
    public Task CloseForPlayerAsync(PlayerId playerId, CancellationToken ct);
}
