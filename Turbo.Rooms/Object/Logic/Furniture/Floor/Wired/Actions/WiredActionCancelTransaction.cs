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
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Cancels the users' ongoing payment or trade: one for the picked contracts, or any. The trade
/// ends after a short grace period, so an Initiate Transaction on the same stack replaces it
/// without the window closing (<c>wiredfurni.params.cancel_transaction.usage_info</c>).
/// Int param: 0 for the picked contracts, 1 for any transaction.
/// </summary>
[RoomObjectLogic("wf_act_cancel_transaction")]
public class WiredActionCancelTransaction(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_ANY_TRANSACTION = 0;

    public override int WiredCode => (int)WiredActionType.CANCEL_TRANSACTION;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var players = GetPlayers(WiredSlotSelection.ForUserSlot(this, ctx, 0));

        if (players.Count == 0)
            return Task.FromResult(false);

        ImmutableArray<RoomObjectId> contracts = GetIntParamOrDefault(PARAM_ANY_TRANSACTION, false)
            ? []
            :
            [
                .. GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0))
                    .Where(x => x.Logic is FurnitureWiredContractLogic)
                    .Select(x => x.ObjectId),
            ];

        // Picked contracts, none of which stand here: nothing to cancel.
        if (!GetIntParamOrDefault(PARAM_ANY_TRANSACTION, false) && contracts.IsEmpty)
            return Task.FromResult(false);

        // Told, not awaited: the trade grain awaits this room when it ends the trade.
        foreach (var player in players)
            _grainFactory
                .GetWiredTradeGrain(player.PlayerId)
                .CancelByWiredAsync(contracts, CancellationToken.None)
                .LogAndForget(
                    _roomGrain._logger,
                    "cancel a wired trade in room {RoomId}",
                    _roomGrain.RoomId
                );

        return Task.FromResult(true);
    }
}
