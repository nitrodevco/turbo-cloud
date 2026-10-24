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
/// Fires when a bot sent to a furni arrives on it. String param: the bot's name, matched as Bot
/// Talk matches it; an empty name names no bot, so the box never fires (official client capture
/// 2026-10-24, trigger.13.B1 0/3; "frank " never fired for bot "frank", trigger.13.R1). Picked
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

    // Which bot: the one named in the box, or a selector's, like Bot Reached Avatar (official
    // server: a fresh box offers users [[100,200]], default [100], 2026-10-20T17-31-49-Open-wait).
    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [WiredPlayerSourceType.BotByName, WiredPlayerSourceType.SelectorUsers],
        ];

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not BotReachedItemEvent reached)
            return Task.FromResult(false);

        if (IsBotByName() && !NamesBot(reached.BotName))
            return Task.FromResult(false);

        var stuffIds = GetStuffIds();

        return Task.FromResult(stuffIds.Count == 0 || stuffIds.Contains(reached.FurniId));
    }

    // A selector's bots are not known before the stack runs, so that source does not narrow
    // which bot fires the trigger here; only a name does.
    private bool IsBotByName() =>
        GetPlayerSources() is not [var slot, ..] || slot.Contains(WiredPlayerSourceType.BotByName);

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
