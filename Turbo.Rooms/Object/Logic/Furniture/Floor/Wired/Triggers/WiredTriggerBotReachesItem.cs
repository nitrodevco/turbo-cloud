using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Bot;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires when a bot sent to a furni arrives. String param: bot name, empty for any; picked
/// furni narrow which destinations count.
/// </summary>
[RoomObjectLogic("wf_trg_bot_reached_stf")]
public class WiredTriggerBotReachesItem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.BOT_DESTINATION_REACHED;
    public override List<Type> SupportedEventTypes { get; } = [typeof(BotReachedItemEvent)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not BotReachedItemEvent reached)
            return Task.FromResult(false);

        var botName = GetStringParam();

        if (botName.Length > 0 && !NamesMatch(reached.BotName, botName))
            return Task.FromResult(false);

        var stuffIds = GetStuffIds();

        return Task.FromResult(stuffIds.Count == 0 || stuffIds.Contains(reached.FurniId));
    }

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        if (ctx.Event is not BotReachedItemEvent reached)
            return Task.FromResult(false);

        var selection = ctx.GetSelection(this);

        return Task.FromResult(
            !selection.HasFurni || selection.SelectedFurniIds.Contains(reached.FurniId)
        );
    }
}
