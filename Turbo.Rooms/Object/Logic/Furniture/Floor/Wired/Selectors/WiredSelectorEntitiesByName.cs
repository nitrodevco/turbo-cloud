using System;
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
    private static readonly char[] NAME_SEPARATORS = ['\t', '\r', '\n'];

    public override int WiredCode => (int)WiredSelectorType.USERS_BY_NAME;

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        // The editor is a text area with one name per line, and it sends the lines joined by
        // tabs (UsersByName.readStringParamFromForm). This split on '/' before, so a list of
        // names matched nobody.
        var names = GetStringParam()
            .Split(
                NAME_SEPARATORS,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .ToHashSet(NameComparer);

        foreach (var avatar in _roomGrain.AvatarModule.Avatars)
        {
            if (!names.Contains(avatar.Name))
                continue;

            output.SelectedAvatarIds.Add(avatar.ObjectId);
        }

        return Task.FromResult((IWiredSelectionSet)output);
    }
}
