using System;
using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when the triggerer is of the wanted kind. Param 0 is a bitmask (1 users, 2 bots,
/// 4 pets); the string param names one avatar. Triggering selections only carry players, so
/// bot and pet only masks never match.
/// </summary>
[RoomObjectLogic("wf_cnd_triggerer_match")]
public class WiredConditionExecutorMatches(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int USER_TYPE_PLAYER = 1;

    public override int WiredCode => (int)WiredConditionType.TRIGGERER_MATCHES;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredRangeParamRule(0, 7, 1)];

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
        var mask = GetIntParamOrDefault(0, USER_TYPE_PLAYER);

        if ((mask & USER_TYPE_PLAYER) == 0)
            return false;

        var players = GetPlayers(ctx.GetSelection(this));
        var wantedName = _wiredData.StringParam?.Trim() ?? string.Empty;

        return Quantify(
            players.Select(p =>
                wantedName.Length == 0
                || string.Equals(p.Name, wantedName, StringComparison.OrdinalIgnoreCase)
            ),
            true
        );
    }
}
