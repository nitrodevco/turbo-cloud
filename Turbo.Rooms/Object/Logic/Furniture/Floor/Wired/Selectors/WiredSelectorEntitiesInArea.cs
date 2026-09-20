using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>Picks the players inside a rectangle. Params: root x, root y, width, height.</summary>
[RoomObjectLogic("wf_slc_users_area")]
public class WiredSelectorEntitiesInArea(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_IN_AREA;

    public override List<IWiredParamRule> GetIntParamRules() =>
        WiredArea.GetParamRules(_roomGrain._wiredConfig);

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var area = WiredArea.Create(
            _roomGrain,
            GetIntParamOrDefault(0, 0),
            GetIntParamOrDefault(1, 0),
            GetIntParamOrDefault(2, 0),
            GetIntParamOrDefault(3, 0)
        );
        foreach (var avatar in _roomGrain.AvatarModule.Avatars)
        {
            if (area.Contains(avatar.X, avatar.Y))
                output.SelectedAvatarIds.Add(avatar.ObjectId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
