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
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Takes the selected users back to the start of a reward track (Flash
/// <c>actiontypes._-F22</c>, <c>ActionTypeCodes.RESET_REWARD_TRACK</c> 59; its one field is the
/// track id, "Reward track:"). Their task counts, points and claimed prizes go; a premium they
/// bought stays (inferred: it was paid for). The client is sent the tracks again with
/// <c>reload</c> set.
/// </summary>
[RoomObjectLogic("wf_act_reset_reward_track")]
public class WiredActionResetRewardTrack(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.RESET_REWARD_TRACK;

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var trackId = GetStringParam();

        if (trackId.Length == 0)
            return false;

        var players = GetPlayers(WiredSlotSelection.ForUserSlot(this, ctx, 0));
        var reset = false;

        foreach (var player in players)
        {
            try
            {
                reset |= await _grainFactory
                    .GetPlayerRewardTrackGrain(player.PlayerId)
                    .ResetTrackAsync(trackId, ct);
            }
            catch (Exception ex)
            {
                _roomGrain._logger.LogError(
                    ex,
                    "Wired reward track {TrackId} could not be reset for player {PlayerId} in room {RoomId}",
                    trackId,
                    player.PlayerId,
                    _roomGrain.RoomId
                );
            }
        }

        return reset;
    }
}
