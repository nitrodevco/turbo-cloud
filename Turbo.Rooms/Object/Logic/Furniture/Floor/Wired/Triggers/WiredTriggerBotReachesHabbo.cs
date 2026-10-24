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
/// Fires when a following bot catches up with its avatar. String param: the bot's name, matched
/// as Bot Talk matches it; an empty name names no bot, so the box never fires (official client
/// capture 2026-10-24, trigger.14.B1 0/2).
/// </summary>
[RoomObjectLogic("wf_trg_bot_reached_avtr")]
public class WiredTriggerBotReachesHabbo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx), IWiredUserSourceProvider
{
    public override int WiredCode => (int)WiredTriggerType.BOT_AVATAR_REACHED;
    public override List<Type> SupportedEventTypes { get; } = [typeof(BotReachedAvatarEvent)];

    // Which bot: the one named in the box, or a selector's (official client capture of the
    // box's sources). The reached user is the triggering user either way.
    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [WiredPlayerSourceType.BotByName, WiredPlayerSourceType.SelectorUsers],
        ];

    // Every bot box stacked with it offers "The reached user" (wiredfurni.params.sources.users.10):
    // all seven did on a stack with this trigger and Bot Reached Furni (official client capture
    // 2026-10-24T14-21-24, all placed on one tile 14-20-52) and none did once moved off it
    // (14-29-58, 14-31-08, 14-32-32). Which of the two triggers names the user is inference: this
    // is the one that reaches a user.
    public IReadOnlyList<WiredPlayerSourceType> ProvidedUserSources { get; } =
    [WiredPlayerSourceType.ReachedUser];

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct) =>
        Task.FromResult(
            evt is BotReachedAvatarEvent reached && (!IsBotByName() || NamesBot(reached.BotName))
        );

    // A selector's bots are not known before the stack runs, so that source does not narrow
    // which bot fires the trigger here; only a name does.
    private bool IsBotByName() =>
        GetPlayerSources() is not [var slot, ..] || slot.Contains(WiredPlayerSourceType.BotByName);

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is BotReachedAvatarEvent);
}
