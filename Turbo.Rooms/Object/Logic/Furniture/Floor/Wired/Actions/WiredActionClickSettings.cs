using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Changes what the selected users' clicks do: avatars that are walked behind or clicked
/// through, furni that is clicked through (a game where the floor under a crowd has to stay
/// clickable). It is all the client's doing, which also forgets the setting when the player
/// leaves the room; the room only tells it. Params: the user option, the furni option.
/// </summary>
[RoomObjectLogic("wf_act_click_conf")]
public class WiredActionClickSettings(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.CLICK_SETTINGS;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredClickUserType>(WiredClickUserType.Default),
            new WiredEnumParamRule<WiredClickFurniType>(WiredClickFurniType.Default),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var players = GetPlayers(ctx.GetSelection(this));

        if (players.Count == 0)
            return Task.FromResult(false);

        // Told, not awaited: this runs in the room tick, and a presence may be waiting on the
        // room.
        _roomGrain
            ._grainFactory.SendComposerToPlayersAsync(
                players.Select(x => x.PlayerId),
                new WiredClickSettingsMessageComposer
                {
                    UserOption = GetIntParamOrDefault(0, WiredClickUserType.Default),
                    FurniOption = GetIntParamOrDefault(1, WiredClickFurniType.Default),
                },
                CancellationToken.None
            )
            .LogAndForget(
                _roomGrain._logger,
                $"send wired click settings in room {_roomGrain.RoomId}"
            );

        return Task.FromResult(true);
    }
}
