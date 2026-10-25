using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
using Turbo.Primitives.Trading;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Configuration;
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
    private readonly Dictionary<PlayerId, WiredTransactionProgressSnapshot> _progress = [];

    /// <summary>
    /// Keeps where a player's open contract transaction is, as their trade grain reports it;
    /// null forgets it once it ended.
    /// </summary>
    public void SetProgress(PlayerId playerId, WiredTransactionProgressSnapshot? progress)
    {
        if (progress is null)
            _progress.Remove(playerId);
        else
            _progress[playerId] = progress;
    }

    /// <summary>The player's open contract transaction, if they have one here.</summary>
    public bool TryGetProgress(
        PlayerId playerId,
        [NotNullWhen(true)] out WiredTransactionProgressSnapshot? progress
    ) => _progress.TryGetValue(playerId, out progress);

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
                EarningsCategoryOf(contract),
                ct
            );

        if (failure is { } refused)
        {
            await FailAsync(player.PlayerId, sourceId, refused, notify: true, ct);

            return refused;
        }

        await GrainFactory.SendComposerToPlayerAsync(
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

        await CompleteAsync(player.PlayerId, sourceId, Figures(times, [], reward), ct);

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
        var paidInto = new List<FurnitureWiredChestLogic>();
        var failure = await CarryOutTradeAsync(ctx, request, payment, times, paidInto, ct);

        if (failure is { } refused)
        {
            await FailAsync(ctx.PlayerId, request.SourceId, refused, notify: false, ct);

            return failure;
        }

        await CompleteAsync(
            ctx.PlayerId,
            request.SourceId,
            Figures(
                times,
                payment,
                request.Contract.Type == WiredContractType.Trade
                    ? WiredContractOffers.Reward(request.Contract, times)
                    : null
            ),
            ct
        );

        var type =
            request.Contract.Type == WiredContractType.Trade
                ? WiredTransactionType.ContractTrade
                : WiredTransactionType.ContractPayment;
        var info = DefinitionInfo(request.SourceId, times);

        foreach (var chest in paidInto)
            await chest.OverflowToOwnerAsync(type, info, ct);

        return null;
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
                ? GrainFactory.SendComposerToPlayerAsync(
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

    /// <summary>
    /// Whether wired may move chest contents to or from the player. Habbo refuses a player who
    /// may not trade before it looks at what the chests hold: an empty chest, an insufficient one
    /// and a reward all answered 8 to an account without the trading pass (official client
    /// capture 2026-10-24). Who may trade is the hotel's trade policy (trading pass, permission).
    /// </summary>
    public async Task<bool> MayTradeAsync(
        PlayerId playerId,
        bool givingAway,
        CancellationToken ct
    ) => await _roomGrain._tradePolicy.MayTradeAsync(playerId, givingAway, ct);

    /// <summary>Tells the player why wired could not give to or take from them, and nothing else.</summary>
    public Task TellFailureAsync(
        PlayerId playerId,
        WiredTransactionFailureType reason,
        CancellationToken ct
    ) =>
        GrainFactory.SendComposerToPlayerAsync(
            playerId,
            new WiredTransactionFailMessageComposer { FailureType = reason },
            ct
        );

    /// <summary>
    /// A chest box that could not start: nothing to give from or to, no contract, or a contract
    /// that cannot make a transaction. Habbo tells no player and fires no Transaction Failed; the
    /// room counts a TRANSACTION_FAILURE and sends its <c>wired.error</c> bubble, at most once per
    /// cooldown (official client capture 2026-10-24: bubbles 30 s to 79 s after the previous,
    /// none 2.4 s to 15.6 s after, 11 fires).
    /// </summary>
    public void ReportMisconfigured() =>
        WiredSystem.RecordError(WiredErrorKind.TransactionFailure, NowMs());

    /// <summary>
    /// Every picked chest is locked, or not a wired chest any more: each player is told 15 and
    /// the room counts the misconfiguration (official client capture 2026-10-24, Give Currency
    /// From Chest from a locked chest: WiredTransactionFail 15 on every fire, the bubble on the
    /// first). No Transaction Failed fires: no transaction started.
    /// </summary>
    public async Task RefuseLockedChestsAsync(IEnumerable<PlayerId> playerIds, CancellationToken ct)
    {
        foreach (var playerId in playerIds)
            await TellFailureAsync(playerId, WiredTransactionFailureType.NoOrLockedChests, ct);

        ReportMisconfigured();
    }

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
        ICollection<FurnitureWiredChestLogic> paidInto,
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
        // A full furni chest still takes the payment and passes the overflow to its owner
        // (OverflowToOwnerAsync, after the transaction completed).
        var furniChest = furni.IsEmpty
            ? null
            : ChestWithRoom(chests, WiredChestKind.Furni, furni.Length)
                ?? chests.FirstOrDefault(x => x.Kind == WiredChestKind.Furni);

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

            paidInto.Add(chest);
        }

        // A trade contract has no earnings category (only RewardContract.as:45-47 offers one):
        // credits it gives are paid to the purse (inference, not recorded).
        return reward is null
            ? null
            : await GiveAsync(player, chests, reward, type, info, null, ct);
    }

    /// <summary>Pays every element of a reward out of the chests that hold it.</summary>
    private async Task<WiredTransactionFailureType?> GiveAsync(
        IRoomPlayer player,
        IReadOnlyList<FurnitureWiredChestLogic> chests,
        TradeRequirementRuleSnapshot reward,
        WiredTransactionType type,
        string info,
        WiredEarningsCategory? earningsCategory,
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
                    earningsCategory,
                    ct
                );

                remaining -=
                    kind == WiredChestKind.Coins ? given.Coins : given.Items.Sum(x => x.Count);
            }

            if (remaining > 0)
            {
                Logger.LogError(
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

    /// <summary>
    /// The earnings category a reward contract's credits wait under: the one its editor offers
    /// once the reward holds credits (RewardContract.as:45-47, WIN63-202610201032), as Give
    /// Currency files them (action.45.R3; for a contract inferred, its credit payout is not
    /// recorded).
    /// </summary>
    private static WiredEarningsCategory EarningsCategoryOf(WiredContractSnapshot contract) =>
        Enum.IsDefined((WiredEarningsCategory)contract.RewardCategory)
            ? (WiredEarningsCategory)contract.RewardCategory
            : WiredEarningsCategory.Games;

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

    /// <summary>
    /// What a transaction moved, as the <c>@event.transaction_complete.*</c> variables read it: the
    /// payment into the chests and the reward out of them, furni and credits apart.
    /// </summary>
    private static TransactionFigures Figures(
        int times,
        ImmutableArray<FurnitureItemSnapshot> payment,
        TradeRequirementRuleSnapshot? reward
    )
    {
        var credits = payment
            .Where(x => CreditFurniValue.TryParse(x.Definition.Name, out _))
            .ToImmutableArray();
        var nodes = reward?.Nodes ?? [];

        return new(
            times,
            payment.Length - credits.Length,
            WiredContractOffers.Credits(credits),
            nodes.Where(x => x.Type != TradeRequirementNodeType.Coin).Sum(x => x.Amount),
            nodes.Where(x => x.Type == TradeRequirementNodeType.Coin).Sum(x => x.Amount)
        );
    }

    private readonly record struct TransactionFigures(
        int Multiplier,
        int DepositFurni,
        int DepositCoins,
        int WithdrawalFurni,
        int WithdrawalCoins
    );

    private Task CompleteAsync(
        PlayerId playerId,
        RoomObjectId sourceId,
        TransactionFigures figures,
        CancellationToken ct
    ) =>
        _roomGrain.PublishRoomEventAsync(
            new WiredTransactionCompletedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForWired(_roomGrain.RoomId),
                PlayerId = playerId,
                SourceId = sourceId,
                Multiplier = figures.Multiplier,
                DepositFurniCount = figures.DepositFurni,
                DepositCoinsCount = figures.DepositCoins,
                WithdrawalFurniCount = figures.WithdrawalFurni,
                WithdrawalCoinsCount = figures.WithdrawalCoins,
            },
            ct
        );
}
