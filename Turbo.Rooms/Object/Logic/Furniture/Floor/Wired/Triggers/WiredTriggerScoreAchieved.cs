using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Game;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires when a team score crosses the configured points. Params: points, then the team
/// (zero for any team).
/// </summary>
[RoomObjectLogic("wf_trg_score_achieved")]
public class WiredTriggerScoreAchieved(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.SCORE_ACHIEVED;
    public override List<Type> SupportedEventTypes { get; } = [typeof(GameScoreChangedEvent)];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(1, 1000, 100),
            new WiredEnumParamRule<GameTeamType>(GameTeamType.None),
        ];

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not GameScoreChangedEvent score)
            return Task.FromResult(false);

        var points = GetIntParamOrDefault(0, 100);
        var team = GetIntParamOrDefault(1, GameTeamType.None);

        if (team != GameTeamType.None && team != score.Team)
            return Task.FromResult(false);

        return Task.FromResult(score.PreviousScore < points && score.Score >= points);
    }

    /// <summary>
    /// The scorer is the triggerer, not the team: Give Score by results! with Hutch. in red too
    /// rotated and whispered results! alone (Hutch. 0/2), and Give Score To Team fired it with no
    /// triggerer (nobody whispered or rotated, 1/1; official client capture 2026-10-25T01-06,
    /// wired matrix trigger.10.R1).
    /// </summary>
    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        if (ctx.Event is not GameScoreChangedEvent score)
            return Task.FromResult(false);

        // A bot scoring fires it too, the bot being the triggerer (trigger.10.R2).
        if (score.ScoredBy != default && AvatarModule.TryGetAvatar(score.ScoredBy, out var scorer))
            ctx.Selected.SelectedAvatarIds.Add(scorer.ObjectId);

        return Task.FromResult(true);
    }
}
