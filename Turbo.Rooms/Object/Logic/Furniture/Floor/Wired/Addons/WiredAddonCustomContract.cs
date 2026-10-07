using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// A contract of one payment element and one reward element, built each time the stack fires,
/// for the Initiate Transaction box beside it. Payment alone makes a payment, reward alone a
/// reward, both a trade.
/// <para>
/// Int params, as the client's editor writes them: for the payment and then the reward, on or
/// off, credits or furni, then the amount as a value-or-variable input (switch, number,
/// variable target). Furni slot 0 is the furni whose type is paid, slot 1 the furni whose type
/// is given; slots 2 and 3 and user slots 0 and 1 are where the two amount variables are read.
/// </para>
/// </summary>
[RoomObjectLogic("wf_xtra_custom_contract")]
public class WiredAddonCustomContract(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx), IWiredContractSource
{
    private const int PAYMENT_PARAMS = 0;
    private const int REWARD_PARAMS = 5;

    // Offsets within one side's params.
    private const int ENABLED = 0;
    private const int TYPE = 1;
    private const int IS_VARIABLE = 2;
    private const int VALUE = 3;
    private const int TARGET = 4;

    private const int MAX_AMOUNT = 100000;

    public override int WiredCode => (int)WiredAddonType.CUSTOM_CONTRACT;

    protected override bool HasPositionalVariableIds => true;

    public override int GetMaxVariableIds() => 2;

    public override List<IWiredParamRule> GetIntParamRules() => [.. SideRules(), .. SideRules()];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.PickedFurni,
            WiredSources.PickedFurni,
            WiredSources.Furni,
            WiredSources.Furni,
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.CustomContract = this;

        return Task.FromResult(true);
    }

    public WiredContractSnapshot? BuildContract(IWiredContext ctx)
    {
        var payment = BuildNode(ctx, PAYMENT_PARAMS, side: 0);
        var reward = BuildNode(ctx, REWARD_PARAMS, side: 1);

        if (payment is null && reward is null)
            return null;

        var type =
            payment is null ? WiredContractType.Reward
            : reward is null ? WiredContractType.Payment
            : WiredContractType.Trade;

        return new()
        {
            ContractId = ObjectId,
            Type = type,
            Definition = new()
            {
                YouGive = payment is null ? [] : [new() { Nodes = [payment] }],
                YouGet = reward is null ? null : new() { Nodes = [reward] },
            },
            PaymentMode = WiredContractPaymentMode.Specific,
            LayoutType = "generic",
            RewardCategory = (int)WiredEarningsCategory.Games,
        };
    }

    /// <summary>One side's element, or null when it is off or cannot be made this firing.</summary>
    private TradeRequirementNodeSnapshot? BuildNode(IWiredContext ctx, int offset, int side)
    {
        if (!GetIntParamOrDefault(offset + ENABLED, false))
            return null;

        var amount = (long)GetIntParamOrDefault(offset + VALUE, 1);

        if (GetIntParamOrDefault(offset + IS_VARIABLE, false))
        {
            var reference = new WiredSelectionSet();

            reference.UnionWith(WiredSlotSelection.ForSlot(this, ctx, side + 2));
            reference.UnionWith(WiredSlotSelection.ForUserSlot(this, ctx, side));

            if (!TryReadVariableOperand(side, offset + TARGET, reference, out amount))
                return null;
        }

        if (amount < 1 || amount > MAX_AMOUNT)
            return null;

        if (
            GetIntParamOrDefault(offset + TYPE, TradeRequirementNodeType.Coin)
            == TradeRequirementNodeType.Coin
        )
            return new()
            {
                Type = TradeRequirementNodeType.Coin,
                Amount = (int)amount,
                ItemType = null,
            };

        var furni = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, side)).FirstOrDefault();

        return furni is null
            ? null
            : new()
            {
                Type = TradeRequirementNodeType.Furni,
                Amount = (int)amount,
                ItemType = ChestItemTypes.Of(furni.Definition, furni.Logic.StuffData.GetSnapshot()),
            };
    }

    private static IEnumerable<IWiredParamRule> SideRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredEnumParamRule<TradeRequirementNodeType>(TradeRequirementNodeType.Coin),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(1, MAX_AMOUNT, 1),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
        ];
}
