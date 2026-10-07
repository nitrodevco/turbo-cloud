using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<WiredChestMoveResultSnapshot> DepositIntoWiredChestAsync(
        ActionContext ctx,
        RoomObjectId chestId,
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    )
    {
        if (
            !FurniModule.TryGetFloorItem(chestId, out var item)
            || item.Logic is not FurnitureWiredChestLogic chest
        )
            return Task.FromResult(
                WiredChestMoveResultSnapshot.Failed(
                    WiredTransactionFailureType.ChestNotInRoom,
                    WiredChestSummarySnapshot.Empty
                )
            );

        return chest.DepositAsync(ctx, itemIds, ct);
    }

    public Task<WiredTransactionFailureType?> CompleteWiredContractTradeAsync(
        ActionContext ctx,
        WiredContractTradeRequest request,
        ImmutableArray<FurnitureItemSnapshot> payment,
        int times,
        CancellationToken ct
    ) => WiredTransactionSystem.CompleteTradeAsync(ctx, request, payment, times, ct);

    public Task ReportWiredTransactionFailedAsync(
        PlayerId playerId,
        RoomObjectId sourceId,
        WiredTransactionFailureType reason,
        CancellationToken ct
    ) => WiredTransactionSystem.FailAsync(playerId, sourceId, reason, notify: false, ct);

    public Task LockWiredChestsAsync(
        ActionContext ctx,
        bool locked,
        bool all,
        CancellationToken ct
    ) => WiredChestSystem.LockAsync(ctx, locked, all);

    public async Task<bool> RequestWiredTransactionLogsAsync(
        ActionContext ctx,
        RoomObjectId? chestId,
        int pageSize,
        int page,
        CancellationToken ct
    )
    {
        var ownsChest =
            chestId is { } id
            && FurniModule.TryGetFloorItem(id, out var item)
            && item.Logic is FurnitureWiredChestLogic
            && item.OwnerId == ctx.PlayerId;

        if (!ownsChest && !await CanReadWiredAsync(ctx, ct))
            return false;

        // Told, not awaited: the logs are a query, and the room does not wait on one.
        _grainFactory
            .GetWiredTransactionLogGrain(RoomId)
            .SendLogsAsync(ctx.PlayerId, chestId, pageSize, page, CancellationToken.None)
            .LogAndForget(_logger, "send wired transaction logs in room {RoomId}", RoomId);

        return true;
    }

    public async Task<bool> RequestWiredTransactionDetailsAsync(
        ActionContext ctx,
        long transactionId,
        CancellationToken ct
    )
    {
        if (!await CanReadWiredAsync(ctx, ct))
            return false;

        _grainFactory
            .GetWiredTransactionLogGrain(RoomId)
            .SendDetailsAsync(ctx.PlayerId, transactionId, CancellationToken.None)
            .LogAndForget(_logger, "send wired transaction details in room {RoomId}", RoomId);

        return true;
    }
}
