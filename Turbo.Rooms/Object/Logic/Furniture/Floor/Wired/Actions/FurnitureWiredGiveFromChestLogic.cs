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
            WiredRules.VariableTarget(WiredVariableTargetType.User),
            new WiredBoolParamRule(false),
            KindParamRule,
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni, WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var chests = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_CHESTS))
            .Select(x => x.Logic)
            .OfType<FurnitureWiredChestLogic>()
            .Where(x => x.Kind == Kind && x.IsUsableByWired)
            .ToList();
        var receivers = GetPlayers(WiredSlotSelection.ForUserSlot(this, ctx, SLOT_RECEIVERS));

        if (chests.Count == 0 || receivers.Count == 0)
            return false;

        var giveAll =
            GetIntParamOrDefault(PARAM_MODE, WiredChestRewardingMode.SpecifiedAmount)
            == WiredChestRewardingMode.All;

        // Everything cannot go to each of several users.
        if (giveAll && receivers.Count > 1)
            return false;

        var amount = giveAll ? (int?)null : ResolveAmount(ctx);

        if (amount is <= 0)
            return false;

        var gave = false;

        foreach (var receiver in receivers)
            gave |= await GiveToAsync(receiver, chests, amount, ct);

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

        await _grainFactory
            .SendComposerToPlayerAsync(
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
                        OpenByDefault = GetIntParamOrDefault(PARAM_SHOW_POPUP, false),
                    },
                },
                ct
            )
            .ConfigureAwait(false);

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
            _roomGrain._logger.LogDebug(
                "Give-from-chest box {BoxId} in room {RoomId} found no amount in its variable",
                ObjectId,
                _roomGrain.RoomId
            );

            return null;
        }

        return (int)Math.Clamp(value, 0, int.MaxValue);
    }
}
