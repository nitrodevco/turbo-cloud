using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// The two give-from-chest boxes: hand the users credits or furni out of the picked chests.
/// Only wired chests that are unlocked give; the chests are walked in order until the amount
/// is met. Each user who received something is shown the reward pop-up with the box's text.
/// <para>
/// Int params, as the client's editor writes them: rewarding mode (an amount, or everything),
/// the amount as a value-or-variable input (number, switch, variable target), whether the
/// pop-up opens by itself, then one param of the kind's own (<see cref="KindParamRule"/>).
/// Furni slot 0 is the chests, user slot 0 the users to reward; furni and user slot 1 are where
/// the amount variable is read. The string param is the pop-up's text.
/// </para>
/// </summary>
public abstract class FurnitureWiredGiveFromChestLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    protected const int PARAM_MODE = 0;
    protected const int PARAM_AMOUNT = 1;
    protected const int PARAM_AMOUNT_IS_VARIABLE = 2;
    protected const int PARAM_AMOUNT_TARGET = 3;
    protected const int PARAM_SHOW_POPUP = 4;
    protected const int PARAM_KIND = 5;

    private const int SLOT_CHESTS = 0;
    private const int SLOT_RECEIVERS = 0;
    private const int SLOT_REFERENCE = 1;
    private const int VARIABLE_AMOUNT = 0;

    /// <summary>The longest pop-up text the editor takes.</summary>
    private const int POPUP_TEXT_MAX_LENGTH = 200;

    protected abstract WiredChestKind Kind { get; }

    /// <summary>The kind's own param: the earnings category, or the order furni are taken in.</summary>
    protected abstract IWiredParamRule KindParamRule { get; }

    protected virtual WiredChestIterationMode Order => WiredChestIterationMode.FirstInFirstOut;

    /// <summary>The earnings category the credits given wait under; null for furni.</summary>
    protected virtual WiredEarningsCategory? EarningsCategory => null;

    protected override bool HasPositionalVariableIds => true;

    public override int GetMaxVariableIds() => 1;

    protected override int GetStringParamMaxLength() => POPUP_TEXT_MAX_LENGTH;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredChestRewardingMode>(
                WiredChestRewardingMode.SpecifiedAmount
            ),
            new WiredRangeParamRule(1, int.MaxValue, 1),
            new WiredBoolParamRule(false),
            // The official server opens a fresh box reading a context variable with the pop-up
            // ticked (both kinds, defaults [0,1,0,-20,1,..], 2026-10-20T17-39-35-Open-wait).
            WiredRules.VariableTarget(WiredVariableTargetType.Context),
            new WiredBoolParamRule(true),
            KindParamRule,
        ];

    // The chests default to the picked furni and the variable reference to the selectors
    // (official client capture).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary,
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.SelectorItems),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users.DefaultingTo(WiredPlayerSourceType.SelectorUsers)];

    // The chests slot takes Wired chests of the box's kind; a normal chest does not give to an
    // effect (require_coin_chests / require_furni_chests and their note; which box sends which
    // text is inferred from the wording).
    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        slot != SLOT_CHESTS
        || item.Logic is FurnitureWiredChestLogic { IsWiredEnabled: true } chest
            && chest.Kind == Kind
            ? null
        : Kind == WiredChestKind.Coins ? WiredSaveErrors.REQUIRE_COIN_CHESTS
        : WiredSaveErrors.REQUIRE_FURNI_CHESTS;

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var picked = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_CHESTS))
            .Select(x => x.Logic)
            .OfType<FurnitureWiredChestLogic>()
            .Where(x => x.Kind == Kind)
            .ToList();
        var receivers = GetPlayers(WiredSlotSelection.ForUserSlot(this, ctx, SLOT_RECEIVERS));

        // No chest or no user: only the room hears of it (official client capture 2026-10-24,
        // wired matrix action.45.R1/R1b).
        if (picked.Count == 0 || receivers.Count == 0)
        {
            WiredTransactionSystem.ReportMisconfigured();

            return false;
        }

        var chests = picked.Where(x => x.IsUsableByWired).ToList();

        // Locked chests come first, before whether the users may trade (action.45.B1).
        if (chests.Count == 0)
        {
            await WiredTransactionSystem.RefuseLockedChestsAsync(
                receivers.Select(x => x.PlayerId),
                ct
            );

            return false;
        }

        var giveAll =
            GetIntParamOrDefault(PARAM_MODE, WiredChestRewardingMode.SpecifiedAmount)
            == WiredChestRewardingMode.All;

        // Everything cannot go to each of several users.
        if (giveAll && receivers.Count > 1)
            return false;

        var amount = giveAll ? (int?)null : ResolveAmount(ctx);

        // No amount is "everything" only in that mode: a variable with no value gives nothing.
        if (!giveAll && amount is not > 0)
            return false;

        var gave = false;

        foreach (var receiver in receivers)
        {
            // A user who may not trade is told 8 whatever the chests hold, empty ones too
            // (action.45.B1b, action.46.B1: 6 of 6).
            if (!await WiredTransactionSystem.MayTradeAsync(receiver.PlayerId, false, ct))
            {
                await WiredTransactionSystem.TellFailureAsync(
                    receiver.PlayerId,
                    WiredTransactionFailureType.UserCantTrade,
                    ct
                );

                continue;
            }

            gave |= await GiveToAsync(receiver, chests, amount, ct);
        }

        return gave;
    }

    private async Task<bool> GiveToAsync(
        IRoomPlayer receiver,
        List<FurnitureWiredChestLogic> chests,
        int? amount,
        CancellationToken ct
    )
    {
        var remaining = amount;
        var coins = 0;
        var furni = new Dictionary<ChestItemTypeSnapshot, int>();

        foreach (var chest in chests)
        {
            if (remaining is <= 0)
                break;

            var held = Kind == WiredChestKind.Coins ? chest.Summary.Coins : chest.Summary.ItemCount;
            var take = remaining is { } left ? Math.Min(left, held) : held;

            if (take <= 0)
                continue;

            var result = await chest.GiveAsync(
                receiver.PlayerId,
                receiver.Name,
                take,
                null,
                Order,
                WiredTransactionType.Wired,
                $"{ObjectId}:{take}",
                EarningsCategory,
                ct
            );

            if (!result.Succeeded)
                continue;

            coins += result.Coins;

            foreach (var item in result.Items)
                furni[item.Type] = furni.GetValueOrDefault(item.Type) + item.Count;

            if (remaining is not null)
                remaining -=
                    Kind == WiredChestKind.Coins ? result.Coins : result.Items.Sum(x => x.Count);
        }

        if (coins == 0 && furni.Count == 0)
            return false;

        await _grainFactory.SendComposerToPlayerAsync(
            receiver.PlayerId,
            new WiredTransactionSuccessMessageComposer
            {
                Contents = new()
                {
                    Type = WiredTransactionSuccessType.Rewarded,
                    RewardContents = new()
                    {
                        Nodes =
                        [
                            .. coins > 0
                                ?
                                [
                                    new TradeRequirementNodeSnapshot
                                    {
                                        Type = TradeRequirementNodeType.Coin,
                                        Amount = coins,
                                        ItemType = null,
                                    },
                                ]
                                : Array.Empty<TradeRequirementNodeSnapshot>(),
                            .. furni.Select(x => new TradeRequirementNodeSnapshot
                            {
                                Type = TradeRequirementNodeType.Furni,
                                Amount = x.Value,
                                ItemType = x.Key,
                            }),
                        ],
                    },
                    RewardText = _roomGrain._wordFilter.Filter(GetStringParam()),
                    OpenByDefault = GetIntParamOrDefault(PARAM_SHOW_POPUP, true),
                },
            },
            ct
        );

        return true;
    }

    /// <summary>The fixed amount, or the variable's value on the box's reference furni or users.</summary>
    private int? ResolveAmount(IWiredExecutionContext ctx)
    {
        if (!GetIntParamOrDefault(PARAM_AMOUNT_IS_VARIABLE, false))
            return GetIntParamOrDefault(PARAM_AMOUNT, 1);

        var reference = new WiredSelectionSet();

        reference.UnionWith(WiredSlotSelection.ForSlot(this, ctx, SLOT_REFERENCE));
        reference.UnionWith(WiredSlotSelection.ForUserSlot(this, ctx, SLOT_REFERENCE));

        if (!TryReadVariableOperand(VARIABLE_AMOUNT, PARAM_AMOUNT_TARGET, reference, out var value))
        {
            Logger.LogDebug(
                "Give-from-chest box {BoxId} in room {RoomId} found no amount in its variable",
                ObjectId,
                _roomGrain.RoomId
            );

            return null;
        }

        return (int)Math.Clamp(value, 0, int.MaxValue);
    }
}
