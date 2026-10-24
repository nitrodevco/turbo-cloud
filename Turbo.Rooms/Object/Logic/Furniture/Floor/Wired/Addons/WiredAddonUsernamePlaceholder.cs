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

/// <summary>
/// Replaces "$(name)" in the text of the stack actions with its users' names, a bot's included:
/// a bot triggering printed its own name (official client capture 2026-10-24, W3 lab
/// trigger.0.R2 / 7.R2 / 16.R2, "P T=Frank").
/// </summary>
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
        [.. GetUsers(WiredSlotSelection.ForUserSlot(this, ctx, 0)).Select(x => x.Name)];
}
