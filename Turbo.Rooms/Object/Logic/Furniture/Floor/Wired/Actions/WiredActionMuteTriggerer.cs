using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Mutes the selected users in the room for param 0 minutes and whispers them the string
/// param. Owners cannot be muted.
/// </summary>
[RoomObjectLogic("wf_act_mute_triggerer")]
public class WiredActionMuteTriggerer(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.MUTE_USER;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredRangeParamRule(0, 10, 1)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override int GetStringParamMaxLength() =>
        _roomGrain._wiredConfig.KickMessageMaxLength;

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var minutes = GetIntParamOrDefault(0, 1);
        var message = await ctx.FormatTextAsync(_wiredData.StringParam?.Trim() ?? string.Empty, ct);
        var muted = false;

        foreach (var player in GetPlayers(ctx.GetSelection(this)))
        {
            if (
                !await _roomGrain.ModerationModule.MutePlayerBySystemAsync(
                    player.PlayerId,
                    minutes,
                    ct
                )
            )
                continue;

            muted = true;

            if (message.Length == 0)
                continue;

            await _roomGrain.ChatSystem.WhisperToPlayerAsync(player, message, ct);
        }

        return muted;
    }
}
