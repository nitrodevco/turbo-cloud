using System;
using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>True when the triggering users wear the badge named in the string param.</summary>
[RoomObjectLogic("wf_cnd_wearing_badge")]
public class WiredConditionHabboHasWearingBadge(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.ACTOR_IS_WEARING_BADGE;

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
            ],
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var badgeCode = _wiredData.StringParam?.Trim() ?? string.Empty;

        if (badgeCode.Length == 0)
            return false;

        var players = GetPlayers(ctx.GetSelection(this));

        return Quantify(
            players.Select(player =>
                player.BadgeCodes.Any(code =>
                    string.Equals(code, badgeCode, StringComparison.OrdinalIgnoreCase)
                )
            ),
            true
        );
    }
}
