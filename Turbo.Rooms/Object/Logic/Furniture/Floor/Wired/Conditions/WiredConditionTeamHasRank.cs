using System;
using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when a team holds a placement. Param 0 is the team (zero: the triggerer's team),
/// param 1 the placement index (zero for first).
/// </summary>
[RoomObjectLogic("wf_cnd_team_has_rank")]
public class WiredConditionTeamHasRank(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.TEAM_IS_WINNING;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<GameTeamType>(GameTeamType.None), new WiredRangeParamRule(0, 3, 0)];

    // The users' quantifier radio (quantifierType 2 in every official Open of codes 6, 17, 31
    // and 34, official client capture).
    protected override WiredQuantifierType QuantifierType => WiredQuantifierType.Users;

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var placement = GetIntParamOrDefault(1, 0) + 1;

        return MatchesTeams(ctx, team => GameSystem.GetPlacement(team) == placement);
    }

    // Whose team "Triggerer's team" is: the triggering user by default, or a selector's or a
    // signal's users (official form contracts of Team Is Winning and Team Has Score).
    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    /// <summary>
    /// The configured team, or when set to "Triggerer's team" (wiredfurni.params.team.triggerer)
    /// the team of each user in the user slot, folded by the quantifier: a user in no team fails,
    /// and with no user at all "all match" holds and "any matches" does not. A named team is no
    /// user's, so the quantifier leaves it alone (official client capture 2026-10-24: under
    /// Periodically, triggerer's team passed with All and failed with Any, explicit red passed
    /// with both; a triggerer in no team failed, condition.31.R1/R2, condition.34.R1/R2).
    /// </summary>
    protected bool MatchesTeams(IWiredProcessingContext ctx, Func<GameTeamType, bool> matches)
    {
        var configured = GetIntParamOrDefault(0, GameTeamType.None);

        if (configured != GameTeamType.None)
            return matches(configured);

        return Quantify(
            GetUsers(WiredSlotSelection.ForUserSlot(this, ctx, 0))
                .Select(x => GameSystem.GetTeam(x.ObjectId))
                .Select(team => team != GameTeamType.None && matches(team)),
            QuantifierRequiresAll
        );
    }
}
