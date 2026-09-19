using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>Fires when a player enters. String param: a name to react to only that player.</summary>
[RoomObjectLogic("wf_trg_enter_room")]
public class WiredTriggerHabboJoinRoom(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.AVATAR_ENTERS_ROOM;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PlayerEnterEvent)];

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        if (ctx.Event is not PlayerEnterEvent enter)
            return Task.FromResult(false);

        if (string.IsNullOrWhiteSpace(_wiredData.StringParam))
            return Task.FromResult(true);

        return Task.FromResult(
            TryGetPlayer(enter.PlayerId, out var player)
                && string.Equals(
                    player.Name,
                    _wiredData.StringParam.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }
}
