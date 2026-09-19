using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>Opens a trade with the player standing at the given room object id.</summary>
    public Task<bool> OpenTradeAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId,
        CancellationToken ct
    );
    public Task<bool> AddTradeItemsAsync(
        ActionContext ctx,
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    );
    public Task<bool> RemoveTradeItemAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    );

    /// <summary>Accepts (or withdraws acceptance of) the current offers.</summary>
    public Task<bool> AcceptTradeAsync(ActionContext ctx, bool accept, CancellationToken ct);

    /// <summary>The final confirmation after both sides accepted; declining reopens the offers.</summary>
    public Task<bool> ConfirmTradeAsync(ActionContext ctx, bool accept, CancellationToken ct);
    public Task<bool> CloseTradeAsync(ActionContext ctx, CancellationToken ct);
}
