using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Progresses one reward track task for the selected users (Flash
/// <c>actiontypes.ProgressRewardTrack</c>, <c>ActionTypeCodes.PROGRESS_REWARD_TRACK</c> 58).
/// <para>
/// String param: the track id and the task id joined by a tab
/// (<c>readStringParamFromForm</c>). Int params, as the editor writes them: 1 "Add to existing
/// score" or 0 to set it, then the value-or-variable section (0 the number, 1 a variable), the
/// number (1 and up), and the variable's target. User slot 0 is who is progressed; furni slot 0
/// and user slot 1 are the merged source the variable is read on (<c>mergedSelections [[0, 1]]</c>).
/// </para>
/// A task's count only moves forward, up to its last level, as the player's own actions move it
/// (inferred from the reward track's rules: no official word on a lower "set"; Reset Reward Track
/// takes a track back).
/// </summary>
[RoomObjectLogic("wf_act_progress_reward_track")]
public class WiredActionProgressRewardTrack(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_ADD = 0;
    private const int PARAM_IS_VARIABLE = 1;
    private const int PARAM_VALUE = 2;
    private const int PARAM_TARGET = 3;

    private const int SLOT_REFERENCE_FURNI = 0;
    private const int SLOT_USERS = 0;
    private const int SLOT_REFERENCE_USERS = 1;
    private const int VARIABLE_VALUE = 0;

    public override int WiredCode => (int)WiredActionType.PROGRESS_REWARD_TRACK;

    protected override bool HasPositionalVariableIds => true;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(1, int.MaxValue, 1),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var ids = GetStringParam().Split('\t');

        if (ids.Length != 2 || ids[0].Length == 0 || ids[1].Length == 0)
            return false;

        var players = GetPlayers(WiredSlotSelection.ForUserSlot(this, ctx, SLOT_USERS));

        if (players.Count == 0)
            return false;

        var score = ResolveScore(ctx);
        var add = GetIntParamOrDefault(PARAM_ADD, false);
        var progressed = false;

        foreach (var player in players)
        {
            try
            {
                progressed |= await _grainFactory
                    .GetPlayerRewardTrackGrain(player.PlayerId)
                    .ProgressTaskAsync(ids[0], ids[1], score, add, ct);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "Wired reward track {TrackId}/{TaskId} could not be progressed for player {PlayerId} in room {RoomId}",
                    ids[0],
                    ids[1],
                    player.PlayerId,
                    _roomGrain.RoomId
                );
            }
        }

        return progressed;
    }

    private int ResolveScore(IWiredExecutionContext ctx)
    {
        long value = GetIntParamOrDefault(PARAM_VALUE, 0);

        if (GetIntParamOrDefault(PARAM_IS_VARIABLE, false))
        {
            var reference = new WiredSelectionSet();

            reference.UnionWith(WiredSlotSelection.ForSlot(this, ctx, SLOT_REFERENCE_FURNI));
            reference.UnionWith(WiredSlotSelection.ForUserSlot(this, ctx, SLOT_REFERENCE_USERS));

            if (!TryReadVariableOperand(VARIABLE_VALUE, PARAM_TARGET, reference, out value))
                return 0;
        }

        return (int)Math.Clamp(value, 0, int.MaxValue);
    }
}
