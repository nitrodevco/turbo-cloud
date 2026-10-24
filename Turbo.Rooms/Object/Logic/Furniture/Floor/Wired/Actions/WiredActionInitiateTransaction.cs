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
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
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
    private const int DEFAULT_MULTIPLIER = 10;

    public override int WiredCode => (int)WiredActionType.INITIATE_TRANSACTION;

    protected override bool AllowsWallFurni => true;

    protected override bool HasPositionalVariableIds => true;

    public override int GetMaxVariableIds() => 1;

    // A fresh box asks x10 and reads the multiplier from a context variable (official client
    // capture: defaultIntParams [0,10,0,-20,0,300]).
    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<TradeRequirementRulesType>(TradeRequirementRulesType.Single),
            new WiredRangeParamRule(
                1,
                _roomGrain._wiredChestConfig.MaxTransactionMultiplier,
                Math.Min(DEFAULT_MULTIPLIER, _roomGrain._wiredChestConfig.MaxTransactionMultiplier)
            ),
            new WiredBoolParamRule(false),
            WiredRules.VariableTarget(WiredVariableTargetType.Context),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(
                TIMEOUT_MIN_SECONDS,
                TIMEOUT_MAX_SECONDS,
                TIMEOUT_DEFAULT_SECONDS
            ),
        ];

    // The contracts may be the triggering furni: the Wired Faculty's "Automatic shop with
    // contracts" (18/03/2026) offers the contract the user clicked. The chests default to the
    // picked furni, the contracts to the secondary picks and the variable reference to the
    // selectors (official client capture).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary,
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.SecondaryItems),
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.SelectorItems),
        ];

    // The chests slot takes Wired chests (require_chests), the contracts slot contracts, custom
    // ones too (require_contracts); neither has to hold anything to save (official client
    // capture 2026-10-22: a custom contract in the chests pick refused, in the contracts pick
    // saved with no chest; a lamp in the contracts pick refused).
    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        slot switch
        {
            SLOT_CHESTS when item.Logic is not FurnitureWiredChestLogic { IsWiredEnabled: true } =>
                WiredSaveErrors.REQUIRE_CHESTS,
            SLOT_CONTRACTS
                when item.Logic is not (FurnitureWiredContractLogic or IWiredContractSource) =>
                WiredSaveErrors.REQUIRE_CONTRACTS,
            _ => null,
        };

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users.DefaultingTo(WiredPlayerSourceType.SelectorUsers)];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var players = GetPlayers(WiredSlotSelection.ForUserSlot(this, ctx, SLOT_USERS));

        var (contract, sourceId) = ResolveContract(ctx);
        var chestIds = WiredSlotSelection
            .ForSlot(this, ctx, SLOT_CHESTS)
            .SelectedFurniIds.Select(RoomObjectId.Parse)
            .ToImmutableArray();
        var anyChest = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_CHESTS))
            .Any(x => x.Logic is FurnitureWiredChestLogic);

        // Nothing picked, no chest, or a contract that cannot make a transaction: no transaction
        // starts, so no player is told and Transaction Failed does not fire; only the room hears
        // of it (official client capture 2026-10-24, wired matrix action.47.R1/R1b/B3). With no
        // user it is inferred to be the same, as for the give boxes (action.45.R1).
        if (
            players.Count == 0
            || contract is null
            || !anyChest
            || !WiredContractOffers.CanMakeTransaction(contract)
        )
        {
            WiredTransactionSystem.ReportMisconfigured();

            return false;
        }

        var chests = WiredTransactionSystem.UsableChests(chestIds);

        // Only locked chests: told as the give boxes tell it (action.45.B1); inferred for this box.
        if (chests.Count == 0)
        {
            await WiredTransactionSystem.RefuseLockedChestsAsync(
                players.Select(x => x.PlayerId),
                ct
            );

            return false;
        }

        var givingAway = contract.Type != WiredContractType.Reward;
        var traders = new List<IRoomPlayer>();

        // The transaction started: a user who may not trade fails it with 8, which Transaction
        // Failed hears, before whether the chests can cover it (action.47.B1/B4, 7 of 7).
        foreach (var player in players)
        {
            if (await WiredTransactionSystem.MayTradeAsync(player.PlayerId, givingAway, ct))
                traders.Add(player);
            else
                await WiredTransactionSystem.FailAsync(
                    player.PlayerId,
                    sourceId,
                    WiredTransactionFailureType.UserCantTrade,
                    notify: true,
                    ct
                );
        }

        if (traders.Count == 0)
            return false;

        players = traders;

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
                .LogAndForget(Logger, "offer contract {SourceId} to a player", sourceId.Value);

        return true;
    }

    /// <summary>
    /// A custom contract addon on the stack wins over the picked contracts, a picked custom
    /// contract counting as one of them. The transaction is
    /// the contract's, the custom one's too ("Contracts are special furniture that define the
    /// contents of a transaction", <c>special_items.chests.body.wf_xtra_custom_contract.desc</c>),
    /// and this box's only when no contract was involved, as
    /// <c>wiredfurni.error.require_contracts_or_trade_wired</c> puts it. Null when there is
    /// neither, or more than one contract: Habbo names that failure "Misconfig Too Many Or No
    /// Contracts" (Creator Tools, failure reason 21).
    /// </summary>
    private (WiredContractSnapshot? Contract, RoomObjectId SourceId) ResolveContract(
        IWiredExecutionContext ctx
    )
    {
        if (ctx.Policy.CustomContract is { } custom)
            return (custom.BuildContract(ctx), custom.ContractFurniId);

        var contracts = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_CONTRACTS))
            .Select(x => x.Logic)
            .Where(x => x is FurnitureWiredContractLogic or IWiredContractSource)
            .Take(2)
            .ToList();

        return contracts switch
        {
            [FurnitureWiredContractLogic contract] => (
                contract.Contract,
                contract.Context.ObjectId
            ),
            [IWiredContractSource picked] => (picked.BuildContract(ctx), picked.ContractFurniId),
            _ => (null, ObjectId),
        };
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
