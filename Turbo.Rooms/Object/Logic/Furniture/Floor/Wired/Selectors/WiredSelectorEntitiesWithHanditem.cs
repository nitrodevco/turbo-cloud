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

/// <summary>
/// Picks the users carrying the hand item in param 0; 0, what the editor saves for none, picks
/// the users holding nothing (official client capture 2026-10-24, W4 lab selector.14.R5 / R2:
/// an empty-handed user selected 1/1 and 4/4), as Has Hand Item reads it.
/// </summary>
[RoomObjectLogic("wf_slc_users_handitem")]
public class WiredSelectorEntitiesWithHanditem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_WITH_HANDITEM;

    public override bool SelectsFurni => false;

    public override bool SelectsUsers => true;

    public override List<IWiredParamRule> GetIntParamRules() => [WiredRules.HandItem(WiredConfig)];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var handItemId = GetIntParamOrDefault(0, 0);

        foreach (var avatar in AvatarModule.Avatars)
        {
            if (avatar.HandItemId == handItemId)
                output.SelectedAvatarIds.Add(avatar.ObjectId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
