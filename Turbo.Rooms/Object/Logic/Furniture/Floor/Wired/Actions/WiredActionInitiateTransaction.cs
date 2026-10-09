using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Offers the users a contract: the first picked contract, or a custom contract addon on the
/// same stack. A reward is paid out at once; a payment or trade opens the trade window, and the
/// chests take the payment and give the reward when the user confirms.
/// <para>
/// Int params, as the client's editor writes them: the mode (normal, multiplier,
/// auto-multiplier), the multiplier as a value-or-variable input (number, switch, variable
/// target), whether the trade times out, and after how many seconds. Furni slot 0 is the chests,
/// slot 1 the contracts; furni slot 2 and user slot 1 are where the multiplier variable is read,
/// and user slot 0 is who is offered the contract.
/// </para>
/// </summary>
[RoomObjectLogic("wf_act_init_transaction")]
public class WiredActionInitiateTransaction(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_MODE = 0;
    private const int PARAM_MULTIPLIER = 1;
    private const int PARAM_MULTIPLIER_IS_VARIABLE = 2;
    private const int PARAM_MULTIPLIER_TARGET = 3;
    private const int PARAM_TIMEOUT_ENABLED = 4;
    private const int PARAM_TIMEOUT_SECONDS = 5;

    private const int SLOT_CHESTS = 0;
    private const int SLOT_CONTRACTS = 1;
    private const int SLOT_REFERENCE_FURNI = 2;
    private const int SLOT_USERS = 0;
    private const int SLOT_REFERENCE_USERS = 1;
    private const int VARIABLE_MULTIPLIER = 0;

    // The bounds of the editor's timeout input.
    private const int TIMEOUT_MIN_SECONDS = 30;
    private const int TIMEOUT_MAX_SECONDS = 3600;
    private const int TIMEOUT_DEFAULT_SECONDS = 300;

    public override int WiredCode => (int)WiredActionType.INITIATE_TRANSACTION;

    protected override bool HasPositionalVariableIds => true;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<TradeRequirementRulesType>(TradeRequirementRulesType.Single),
            new WiredRangeParamRule(1, _roomGrain._wiredChestConfig.MaxTransactionMultiplier, 1),
            new WiredBoolParamRule(false),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(
                TIMEOUT_MIN_SECONDS,
                TIMEOUT_MAX_SECONDS,
                TIMEOUT_DEFAULT_SECONDS
            ),
        ];

    // The contracts may be the triggering furni: the Wired Faculty's "Automatic shop with
    // contracts" (18/03/2026) offers the contract the user clicked.
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni, WiredSources.Furni, WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var players = GetPlayers(WiredSlotSelection.ForUserSlot(this, ctx, SLOT_USERS));

        if (players.Count == 0)
            return false;

        var (contract, sourceId) = ResolveContract(ctx);
        var chestIds = WiredSlotSelection
            .ForSlot(this, ctx, SLOT_CHESTS)
            .SelectedFurniIds.Select(RoomObjectId.Parse)
            .ToImmutableArray();
        var chests = WiredTransactionSystem.UsableChests(chestIds);
        var failure =
            contract is null ? WiredTransactionFailureType.Misconfig
            : chests.Count == 0 ? WiredTransactionFailureType.NoOrLockedChests
            : (WiredTransactionFailureType?)null;

        if (failure is { } refused || contract is null)
        {
            foreach (var player in players)
                await WiredTransactionSystem.FailAsync(
                    player.PlayerId,
                    sourceId,
                    failure ?? WiredTransactionFailureType.Misconfig,
                    notify: true,
                    ct
                );

            return false;
        }

        var mode = GetIntParamOrDefault(PARAM_MODE, TradeRequirementRulesType.Single);
        var multiplier = mode == TradeRequirementRulesType.Single ? 1 : ResolveMultiplier(ctx);

        if (contract.Type == WiredContractType.Reward)
        {
            var rewarded = false;

            foreach (var player in players)
                rewarded |=
                    await WiredTransactionSystem.RewardAsync(
                        player,
                        chests,
                        contract,
                        sourceId,
                        multiplier,
                        ct
                    )
                        is null;

            return rewarded;
        }

        var request = new WiredContractTradeRequest
        {
            RoomId = _roomGrain.RoomId,
            SourceId = sourceId,
            Contract = contract,
            RulesType = mode,
            Multiplier = multiplier,
            ChestIds = chestIds,
            TimeoutSeconds = GetIntParamOrDefault(PARAM_TIMEOUT_ENABLED, false)
                ? GetIntParamOrDefault(PARAM_TIMEOUT_SECONDS, TIMEOUT_DEFAULT_SECONDS)
                : 0,
        };

        // Told, not awaited: the trade grain awaits this room when the trade is confirmed.
        foreach (var player in players)
            _grainFactory
                .GetWiredTradeGrain(player.PlayerId)
                .StartContractAsync(request, CancellationToken.None)
                .LogAndForget(
                    _roomGrain._logger,
                    "offer contract {SourceId} to a player",
                    sourceId.Value
                );

        return true;
    }

    /// <summary>
    /// A custom contract addon on the stack wins, and the transaction is then this box's; a
    /// picked contract is its own. Null when there is neither, or more than one contract: Habbo
    /// names that failure "Misconfig Too Many Or No Contracts" (Creator Tools, failure reason 21).
    /// </summary>
    private (WiredContractSnapshot? Contract, RoomObjectId SourceId) ResolveContract(
        IWiredExecutionContext ctx
    )
    {
        if (ctx.Policy.CustomContract is { } custom)
            return (custom.BuildContract(ctx), ObjectId);

        var contracts = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_CONTRACTS))
            .Select(x => x.Logic)
            .OfType<FurnitureWiredContractLogic>()
            .Take(2)
            .ToList();

        return contracts is [var contract]
            ? (contract.Contract, contract.Context.ObjectId)
            : (null, ObjectId);
    }

    private int ResolveMultiplier(IWiredExecutionContext ctx)
    {
        long value = GetIntParamOrDefault(PARAM_MULTIPLIER, 1);

        if (GetIntParamOrDefault(PARAM_MULTIPLIER_IS_VARIABLE, false))
        {
            var reference = new WiredSelectionSet();

            reference.UnionWith(WiredSlotSelection.ForSlot(this, ctx, SLOT_REFERENCE_FURNI));
            reference.UnionWith(WiredSlotSelection.ForUserSlot(this, ctx, SLOT_REFERENCE_USERS));

            if (
                !TryReadVariableOperand(
                    VARIABLE_MULTIPLIER,
                    PARAM_MULTIPLIER_TARGET,
                    reference,
                    out value
                )
            )
                value = 1;
        }

        return (int)Math.Clamp(value, 1, _roomGrain._wiredChestConfig.MaxTransactionMultiplier);
    }
}
