using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>Picks the players currently in the state of the action in param 0.</summary>
[RoomObjectLogic("wf_slc_users_byaction")]
public class WiredSelectorEntitiesByAction(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_PERFORMING_ACTION;

    public override bool SelectsFurni => false;

    public override bool SelectsUsers => true;

    // Ascending room index, bots included, not the user who entered last first (official client
    // capture 2026-10-24T18-20-40, W4 lab selector.9.R4: "Frank,Hutch.,frank,results!" for
    // indices 0,2,3,6 with results! re-entered last, 2/2).
    public override WiredUserOrderType UserOrder => WiredUserOrderType.RoomIndex;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<WiredAvatarActionType>(WiredAvatarActionType.Wave)];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var action = GetIntParamOrDefault(0, WiredAvatarActionType.Wave);

        var now = NowMs();

        foreach (var avatar in AvatarModule.Avatars)
        {
            if (WiredAvatarActionMatcher.IsPerforming(avatar, action, _wiredData.StringParam, now))
                output.SelectedAvatarIds.Add(avatar.ObjectId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
