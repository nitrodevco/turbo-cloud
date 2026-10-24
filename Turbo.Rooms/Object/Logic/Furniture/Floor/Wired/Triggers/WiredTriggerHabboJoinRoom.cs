using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Bot;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires when a player enters, or a bot is placed (<see cref="BotPlacedEvent"/>). String param:
/// a name to react to only that user.
/// </summary>
[RoomObjectLogic("wf_trg_enter_room")]
public class WiredTriggerHabboJoinRoom(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.AVATAR_ENTERS_ROOM;
    public override List<Type> SupportedEventTypes { get; } =
    [typeof(PlayerEnterEvent), typeof(BotPlacedEvent)];

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        if (ctx.Event is not (PlayerEnterEvent or BotPlacedEvent))
            return Task.FromResult(false);

        var name = GetStringParam();

        if (name.Length == 0)
            return Task.FromResult(true);

        var entered = ctx.Event switch
        {
            PlayerEnterEvent enter when TryGetPlayer(enter.PlayerId, out var player) => player.Name,
            BotPlacedEvent placed => placed.BotName,
            _ => null,
        };

        return Task.FromResult(entered is not null && NamesMatch(entered, name));
    }
}
