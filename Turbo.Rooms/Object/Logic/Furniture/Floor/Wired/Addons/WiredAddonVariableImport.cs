using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// "Variable Add-on: Import" (<c>wf_xtra_var_import</c>, AddonCodes.VARIABLE_ADDON_IMPORT 1004,
/// AS3 <c>VariableAddonImportAddon</c> WIN63-202610201032): placed on a "WIRED Variable: From
/// Another Room", it imports the add-ons of the ticked categories that a share add-on in the
/// variable's own room shares (Wired Faculty 19/10/2026; <c>addon_import.usage_info</c>).
/// Placing, moving, saving or removing it loads the variable boxes it stood or stands on again,
/// so they take or drop what it imports.
/// </summary>
[RoomObjectLogic("wf_xtra_var_import")]
public class WiredAddonVariableImport(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredAddonVariableSharing(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.VARIABLE_ADDON_IMPORT;

    protected override async Task OnWiredStackChangedAsync(
        ActionContext ctx,
        List<int> ids,
        CancellationToken ct
    )
    {
        await base.OnWiredStackChangedAsync(ctx, ids, ct);

        List<int> boxIds =
        [
            .. ids.SelectMany(FurniModule.GetFloorItemsOnTile)
                .Where(item => item.Logic is FurnitureWiredVariableLogic)
                .Select(item => item.ObjectId.Value),
        ];

        if (boxIds.Count == 0)
            return;

        await _ctx.PublishRoomEventAsync(
            new WiredVariableBoxChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                BoxIds = boxIds,
            },
            ct
        );
    }
}
