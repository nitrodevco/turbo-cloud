using System;
using System.Collections.Immutable;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Enables wired achievements in the room (Flash <c>AddonCodes.ACHIEVEMENT_ENABLER</c>, editor
/// <c>wired_setup.addons._-Iy</c>): a text area, one achievement name per line, without the
/// <c>ACH_WF_</c> prefix ("Achievement1 (without ACH_WF_)\nAchievement2\n..."). The room sends
/// the names in <c>WiredEnvironment</c>, the Progress Achievement effect may only progress one
/// of them, and staff configure it ("Staff needs to configure the enabled achievements in an
/// Achievement Enabler wired add-on", <c>wiredfurni.error.achievement_not_allowed</c>).
/// </summary>
[RoomObjectLogic("wf_xtra_achievement_enabler")]
public class WiredAddonAchievementEnabler(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.ACHIEVEMENT_ENABLER;

    public override RoomControllerType MinimumControllerLevelToSave => RoomControllerType.Moderator;

    /// <summary>The names of the text area's lines, trimmed, without blanks or repeats.</summary>
    public ImmutableArray<string> EnabledAchievements =>
        [
            .. GetStringParam()
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal),
        ];
}
