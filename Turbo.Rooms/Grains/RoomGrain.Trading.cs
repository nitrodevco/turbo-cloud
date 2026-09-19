using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<bool> OpenTradeAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId,
        CancellationToken ct
    ) => RunTradeActionAsync(ctx, "open", () => TradeModule.OpenAsync(ctx, targetObjectId, ct));

    public Task<bool> AddTradeItemsAsync(
        ActionContext ctx,
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    ) =>
        RunTradeActionAsync(
            ctx,
            "offer items in",
            () => TradeModule.AddItemsAsync(ctx, itemIds, ct)
        );

    public Task<bool> RemoveTradeItemAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    ) =>
        RunTradeActionAsync(
            ctx,
            "withdraw an item from",
            () => TradeModule.RemoveItemAsync(ctx, itemId, ct)
        );

    public Task<bool> AcceptTradeAsync(ActionContext ctx, bool accept, CancellationToken ct) =>
        RunTradeActionAsync(
            ctx,
            accept ? "accept" : "unaccept",
            () => TradeModule.AcceptAsync(ctx, accept, ct)
        );

    public Task<bool> ConfirmTradeAsync(ActionContext ctx, bool accept, CancellationToken ct) =>
        RunTradeActionAsync(
            ctx,
            accept ? "confirm" : "decline",
            () => TradeModule.ConfirmAsync(ctx, accept, ct)
        );

    public Task<bool> CloseTradeAsync(ActionContext ctx, CancellationToken ct) =>
        RunTradeActionAsync(ctx, "close", () => TradeModule.CloseAsync(ctx, ct));

    private async Task<bool> RunTradeActionAsync(
        ActionContext ctx,
        string action,
        Func<Task<bool>> body
    )
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            return await body();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Player {PlayerId} failed to {Action} a trade in room {RoomId}",
                ctx.PlayerId,
                action,
                _state.RoomId
            );

            return false;
        }
    }
}
