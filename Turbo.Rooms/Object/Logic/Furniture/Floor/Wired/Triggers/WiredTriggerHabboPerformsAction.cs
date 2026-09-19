using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires when a player waves, dances, sits and so on. Int param 0 is the action code; the
/// string param narrows a sign ("3") or dance ("dance 2") to one value.
/// </summary>
[RoomObjectLogic("wf_trg_user_performs_action")]
public class WiredTriggerHabboPerformsAction(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.AVATAR_PERFORMS_ACTION;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PlayerPerformsActionEvent)];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<WiredAvatarActionType>(WiredAvatarActionType.Wave)];

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not PlayerPerformsActionEvent action)
            return Task.FromResult(false);

        return Task.FromResult(
            WiredAvatarActionMatcher.Matches(
                action.ActionType,
                action.Value,
                GetIntParamOrDefault(0, WiredAvatarActionType.Wave),
                _wiredData.StringParam
            )
        );
    }

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is PlayerPerformsActionEvent);
}
