using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Carries out the transactions wired offers players: a contract's reward paid out of chests,
/// and a confirmed payment or trade, paid into chests and rewarded out of them. Whatever the
/// outcome, it raises the event the transaction triggers listen to.
/// <para>
/// A transaction checks that the chests can cover everything before it moves anything. The room
/// is not reentrant, so nothing else moves chest contents in between; only a failed write can
/// leave a transaction half done, and that is logged.
/// </para>
/// </summary>
public sealed class RoomWiredTransactionSystem(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    /// <summary>Pays a contract's reward to a player and tells them, or says why it could not.</summary>
    public async Task<WiredTransactionFailureType?> RewardAsync(
        IRoomPlayer player,
        IReadOnlyList<FurnitureWiredChestLogic> chests,
        WiredContractSnapshot contract,
        RoomObjectId sourceId,
        int times,
        CancellationToken ct
    )
    {
        var reward = WiredContractOffers.Reward(contract, times);
        var failure = reward is null
            ? WiredTransactionFailureType.Misconfig
            : await GiveAsync(
                player,
                chests,
                reward,
                WiredTransactionType.ContractReward,
                DefinitionInfo(sourceId, times),
                ct
            );

        if (failure is { } refused)
        {
            await FailAsync(player.PlayerId, sourceId, refused, notify: true, ct);

            return refused;
        }

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            player.PlayerId,
            new WiredTransactionSuccessMessageComposer
            {
                Contents = new()
                {
                    Type = WiredTransactionSuccessType.Rewarded,
                    RewardContents = reward,
                    RewardText = contract.RewardText,
                    OpenByDefault = contract.ShowDialog,
                },
            },
            ct
        );

        await CompleteAsync(player.PlayerId, sourceId, ct);

        return null;
    }

    /// <summary>
    /// A payment or trade the player confirmed: their payment goes into the chests, then what the
    /// contract gives comes out of them. Null when it went through.
    /// </summary>
    public async Task<WiredTransactionFailureType?> CompleteTradeAsync(
        ActionContext ctx,
        WiredContractTradeRequest request,
        ImmutableArray<FurnitureItemSnapshot> payment,
        int times,
        CancellationToken ct
    )
    {
        var failure = await CarryOutTradeAsync(ctx, request, payment, times, ct);

        if (failure is { } refused)
            await FailAsync(ctx.PlayerId, request.SourceId, refused, notify: false, ct);
        else
            await CompleteAsync(ctx.PlayerId, request.SourceId, ct);

        return failure;
    }

    /// <summary>A transaction ended without going through; the triggers hear why.</summary>
    public Task FailAsync(
        PlayerId playerId,
        RoomObjectId sourceId,
        WiredTransactionFailureType reason,
        bool notify,
        CancellationToken ct
    ) =>
        Task.WhenAll(
            notify
                ? _roomGrain._grainFactory.SendComposerToPlayerAsync(
                    playerId,
                    new WiredTransactionFailMessageComposer { FailureType = reason },
                    ct
                )
                : Task.CompletedTask,
            _roomGrain.PublishRoomEventAsync(
                new WiredTransactionFailedEvent
                {
                    RoomId = _roomGrain.RoomId,
                    CausedBy = ActionContext.CreateForWired(_roomGrain.RoomId),
                    PlayerId = playerId,
                    SourceId = sourceId,
                    Reason = reason,
                },
                ct
            )
        );

    /// <summary>The chests among these ids that stand here and wired may use.</summary>
    public List<FurnitureWiredChestLogic> UsableChests(IEnumerable<RoomObjectId> chestIds) =>
        [
            .. chestIds
                .Select(id => FurniModule.TryGetFloorItem(id, out var item) ? item.Logic : null)
                .OfType<FurnitureWiredChestLogic>()
                .Where(x => x.IsUsableByWired),
        ];

    public static string DefinitionInfo(RoomObjectId sourceId, int times) =>
        $"contract:{sourceId.Value} x{times}";

    private async Task<WiredTransactionFailureType?> CarryOutTradeAsync(
        ActionContext ctx,
        WiredContractTradeRequest request,
        ImmutableArray<FurnitureItemSnapshot> payment,
        int times,
        CancellationToken ct
    )
    {
        if (!AvatarModule.TryGetPlayer(ctx.PlayerId, out var player))
            return WiredTransactionFailureType.Invalid;

        var chests = UsableChests(request.ChestIds);

        if (chests.Count == 0)
            return WiredTransactionFailureType.NoOrLockedChests;

        var reward =
            request.Contract.Type == WiredContractType.Trade
                ? WiredContractOffers.Reward(request.Contract, times)
                : null;

        if (reward is not null && !CanCover(chests, reward))
            return WiredTransactionFailureType.FundsGone;

        var credits = payment
            .Where(x => CreditFurniValue.TryParse(x.Definition.Name, out _))
            .ToImmutableArray();
        var furni = payment.Except(credits).ToImmutableArray();
        var creditValue = WiredContractOffers.Credits(credits);
        var coinChest = credits.IsEmpty
            ? null
            : ChestWithRoom(chests, WiredChestKind.Coins, creditValue);
        var furniChest = furni.IsEmpty
            ? null
            : ChestWithRoom(chests, WiredChestKind.Furni, furni.Length);

        if ((!credits.IsEmpty && coinChest is null) || (!furni.IsEmpty && furniChest is null))
            return WiredTransactionFailureType.ChestFull;

        var type =
            request.Contract.Type == WiredContractType.Trade
                ? WiredTransactionType.ContractTrade
                : WiredTransactionType.ContractPayment;
        var info = DefinitionInfo(request.SourceId, times);

        foreach (var (chest, items) in new[] { (furniChest, furni), (coinChest, credits) })
        {
            if (chest is null || items.IsEmpty)
                continue;

            var paid = await chest.TakePaymentAsync(
                player.PlayerId,
                player.Name,
                [.. items.Select(x => x.ItemId)],
                type,
                info,
                ct
            );

            if (paid.Failure is { } failure)
                return failure;
        }

        return reward is null ? null : await GiveAsync(player, chests, reward, type, info, ct);
    }

    /// <summary>Pays every element of a reward out of the chests that hold it.</summary>
    private async Task<WiredTransactionFailureType?> GiveAsync(
        IRoomPlayer player,
        IReadOnlyList<FurnitureWiredChestLogic> chests,
        TradeRequirementRuleSnapshot reward,
        WiredTransactionType type,
        string info,
        CancellationToken ct
    )
    {
        if (!CanCover(chests, reward))
            return WiredTransactionFailureType.FundsGone;

        foreach (var node in reward.Nodes)
        {
            var remaining = node.Amount;
            var kind =
                node.Type == TradeRequirementNodeType.Coin
                    ? WiredChestKind.Coins
                    : WiredChestKind.Furni;

            foreach (var chest in chests.Where(x => x.Kind == kind))
            {
                if (remaining <= 0)
                    break;

                var take = Math.Min(remaining, Holds(chest, node));

                if (take <= 0)
                    continue;

                var given = await chest.GiveAsync(
                    player.PlayerId,
                    player.Name,
                    take,
                    node.ItemType,
                    WiredChestIterationMode.FirstInFirstOut,
                    type,
                    info,
                    ct
                );

                remaining -=
                    kind == WiredChestKind.Coins ? given.Coins : given.Items.Sum(x => x.Count);
            }

            if (remaining > 0)
            {
                _roomGrain._logger.LogError(
                    "Reward to player {PlayerId} in room {RoomId} came {Remaining} short of {Amount} after the chests said they held it",
                    player.PlayerId,
                    _roomGrain.RoomId,
                    remaining,
                    node.Amount
                );

                return WiredTransactionFailureType.InternalError;
            }
        }

        return null;
    }

    private static bool CanCover(
        IReadOnlyList<FurnitureWiredChestLogic> chests,
        TradeRequirementRuleSnapshot reward
    ) =>
        reward
            .Nodes.GroupBy(node => (node.Type, node.ItemType))
            .All(group =>
                chests.Sum(chest => (long)Holds(chest, group.First()))
                >= group.Sum(node => (long)node.Amount)
            );

    /// <summary>How much of what a node asks for one chest holds.</summary>
    private static int Holds(FurnitureWiredChestLogic chest, TradeRequirementNodeSnapshot node) =>
        node.Type == TradeRequirementNodeType.Coin
            ? chest.Kind == WiredChestKind.Coins
                ? chest.Summary.Coins
                : 0
            : chest.Kind == WiredChestKind.Furni
            && node.ItemType is { } itemType
            && chest.Summary.CountsByType.TryGetValue(itemType, out var count)
                ? count
                : 0;

    private static FurnitureWiredChestLogic? ChestWithRoom(
        IEnumerable<FurnitureWiredChestLogic> chests,
        WiredChestKind kind,
        int amount
    ) => chests.FirstOrDefault(x => x.Kind == kind && x.FreeCapacity >= amount);

    private Task CompleteAsync(PlayerId playerId, RoomObjectId sourceId, CancellationToken ct) =>
        _roomGrain.PublishRoomEventAsync(
            new WiredTransactionCompletedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForWired(_roomGrain.RoomId),
                PlayerId = playerId,
                SourceId = sourceId,
            },
            ct
        );
}
