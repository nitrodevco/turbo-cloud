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
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires when a player clicks another avatar. The clicker and the clicked player both become
/// triggering users. The two client checkboxes (block the avatar menu, do not turn) are stored
/// and honoured by the client itself.
/// </summary>
[RoomObjectLogic("wf_trg_click_user")]
public class WiredTriggerClickUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx), IWiredUserSourceProvider
{
    public override int WiredCode => (int)WiredTriggerType.AVATAR_CLICKS_AVATAR;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PlayerClickedAvatarEvent)];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredBoolParamRule(false), new WiredBoolParamRule(false)];

    // The editor has no user picker (AS3 triggerconfs: two checkboxes); the clicked user is
    // offered to the boxes on the stack instead.
    public IReadOnlyList<WiredPlayerSourceType> ProvidedUserSources { get; } =
    [WiredPlayerSourceType.ClickedUser];

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is PlayerClickedAvatarEvent);
}
