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

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>Fires when a following bot catches up with its avatar. String param: bot name, empty for any.</summary>
[RoomObjectLogic("wf_trg_bot_reached_avtr")]
public class WiredTriggerBotReachesHabbo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.BOT_AVATAR_REACHED;
    public override List<Type> SupportedEventTypes { get; } = [typeof(BotReachedAvatarEvent)];

    // Which bot: the one named in the box, or a selector's (official client capture of the
    // box's sources). The reached user is the triggering user either way.
    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [WiredPlayerSourceType.BotByName, WiredPlayerSourceType.SelectorUsers],
        ];

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct) =>
        Task.FromResult(
            evt is BotReachedAvatarEvent reached
                && (
                    !IsBotByName()
                    || GetStringParam().Length == 0
                    || NamesMatch(reached.BotName, GetStringParam())
                )
        );

    // A selector's bots are not known before the stack runs, so that source does not narrow
    // which bot fires the trigger here; only a name does.
    private bool IsBotByName() =>
        GetPlayerSources() is not [var slot, ..] || slot.Contains(WiredPlayerSourceType.BotByName);

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is BotReachedAvatarEvent);
}
