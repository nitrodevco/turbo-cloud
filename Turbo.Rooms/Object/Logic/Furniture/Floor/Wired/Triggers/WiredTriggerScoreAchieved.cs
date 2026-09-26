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
            new WiredRangeParamRule(1, 1000, 1),
            new WiredEnumParamRule<GameTeamType>(GameTeamType.None),
        ];

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not GameScoreChangedEvent score)
            return Task.FromResult(false);

        var points = GetIntParamOrDefault(0, 1);
        var team = GetIntParamOrDefault(1, GameTeamType.None);

        if (team != GameTeamType.None && team != score.Team)
            return Task.FromResult(false);

        return Task.FromResult(score.PreviousScore < points && score.Score >= points);
    }

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        if (ctx.Event is not GameScoreChangedEvent score)
            return Task.FromResult(false);

        foreach (var playerId in _roomGrain.GameSystem.GetTeamMembers(score.Team))
        {
            if (_roomGrain.AvatarModule.TryGetPlayer(playerId, out var member))
                ctx.Selected.SelectedAvatarIds.Add(member.ObjectId);
        }

        return Task.FromResult(true);
    }
}
