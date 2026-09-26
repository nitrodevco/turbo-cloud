using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

[RoomObjectLogic("wf_slc_users_byname")]
public class WiredSelectorEntitiesByName(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_BY_NAME;

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var names = _wiredData.StringParam.Split('/').Select(n => n.Trim().ToLower()).ToHashSet();

        foreach (var avatar in _roomGrain.AvatarModule.Avatars)
        {
            if (!names.Contains(avatar.Name.ToLower()))
                continue;

            output.SelectedAvatarIds.Add(avatar.ObjectId);
        }

        return Task.FromResult((IWiredSelectionSet)output);
    }
}
