using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Throws the selected users out of the room after whispering them the string param. The room
/// owner is never kicked. The room drops the avatar, then the presence closes the session
/// through <c>OnRemovedFromRoomAsync</c>, the eviction call that never re-enters the room. It is
/// not awaited: a wired tick must not wait on a presence that may itself be waiting on the room.
/// </summary>
[RoomObjectLogic("wf_act_kick_user")]
public class WiredActionKickUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.KICK_FROM_ROOM;

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override int GetStringParamMaxLength() =>
        _roomGrain._wiredConfig.KickMessageMaxLength;

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var message = _wiredData.StringParam?.Trim() ?? string.Empty;
        message = await ctx.FormatTextAsync(message, ct);

        var kicked = false;

        // Who may be kicked, and how a kicked player's session is closed, is the moderation
        // module's business; this box only chooses the players and the parting words.
        foreach (var player in GetPlayers(ctx.GetSelection(this)).ToList())
            kicked |= await _roomGrain.ModerationModule.KickPlayerBySystemAsync(
                player.PlayerId,
                message,
                ct
            );

        return kicked;
    }
}
