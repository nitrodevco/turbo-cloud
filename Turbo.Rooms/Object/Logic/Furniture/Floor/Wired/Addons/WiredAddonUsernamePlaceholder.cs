using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>Replaces "$(name)" in the text of the stack actions with its users' names.</summary>
[RoomObjectLogic("wf_xtra_text_output_username")]
public class WiredAddonUsernamePlaceholder(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredNamePlaceholderLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.USERNAME_PLACEHOLDER;

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override List<string> GetNames(IWiredExecutionContext ctx) =>
        [.. GetPlayers(WiredSlotSelection.ForUserSlot(this, ctx, 0)).Select(x => x.Name)];
}
