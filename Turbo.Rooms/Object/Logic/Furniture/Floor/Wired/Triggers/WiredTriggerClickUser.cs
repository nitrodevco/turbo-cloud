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
/// Fires when a player clicks an avatar. The clicker is the triggering user and the clicked one
/// "the clicked user". The two editor checkboxes (Flash <c>triggerconfs._-T2R</c>: block the
/// avatar menu, do not rotate) are the room's to apply: while it has this trigger the client
/// asks before opening the menu and does not turn the clicker itself
/// (<see cref="Turbo.Rooms.Grains.Systems.RoomWiredSystem"/>, <c>OnAvatarClickedAsync</c>).
/// </summary>
[RoomObjectLogic("wf_trg_click_user")]
public class WiredTriggerClickUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx), IWiredUserSourceProvider
{
    private const int PARAM_BLOCK_MENU = 0;
    private const int PARAM_DO_NOT_ROTATE = 1;

    public override int WiredCode => (int)WiredTriggerType.AVATAR_CLICKS_AVATAR;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PlayerClickedAvatarEvent)];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredBoolParamRule(false), new WiredBoolParamRule(false)];

    /// <summary><c>wiredfurni.params.click_user.block_menu_open</c>.</summary>
    public bool BlocksAvatarMenu => GetIntParamOrDefault(PARAM_BLOCK_MENU, false);

    /// <summary><c>wiredfurni.params.click_user.do_not_rotate</c>.</summary>
    public bool KeepsClickerFacing => GetIntParamOrDefault(PARAM_DO_NOT_ROTATE, false);

    // The editor has no user picker (AS3 triggerconfs: two checkboxes); the clicked user is
    // offered to the boxes on the stack instead.
    public IReadOnlyList<WiredPlayerSourceType> ProvidedUserSources { get; } =
    [WiredPlayerSourceType.ClickedUser];

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is PlayerClickedAvatarEvent);
}
