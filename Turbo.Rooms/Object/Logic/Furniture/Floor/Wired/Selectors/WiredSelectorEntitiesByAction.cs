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

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<WiredAvatarActionType>(WiredAvatarActionType.Sit)];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var action = GetIntParamOrDefault(0, WiredAvatarActionType.Sit);

        foreach (var avatar in _roomGrain.AvatarModule.Avatars)
        {
            if (WiredAvatarActionMatcher.IsPerforming(avatar, action, _wiredData.StringParam))
                output.SelectedAvatarIds.Add(avatar.ObjectId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
