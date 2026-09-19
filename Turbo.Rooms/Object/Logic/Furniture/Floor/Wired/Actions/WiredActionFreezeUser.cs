using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Stops the selected users walking until unfrozen. Params: the freeze effect choice (an index
/// into the configured effect ids) and whether a wired teleport thaws them.
/// </summary>
[RoomObjectLogic("wf_act_freeze")]
public class WiredActionFreezeUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.FREEZE_USER;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(0, 4, 0), new WiredBoolParamRule(false)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var effectIndex = GetIntParamOrDefault(0, 0);
        var cancelOnTeleport = GetIntParamOrDefault(1, false);
        var effectIds = _roomGrain._wiredConfig.FreezeEffectIds;
        var effectId = effectIndex < effectIds.Length ? effectIds[effectIndex] : 0;
        var frozen = false;

        foreach (var player in GetPlayers(ctx.GetSelection(this)))
        {
            await _roomGrain.AvatarModule.StopWalkingAsync(player, ct);

            player.SetFrozen(true, cancelOnTeleport);

            if (effectId > 0)
                await _roomGrain.AvatarModule.SetAvatarEffectAsync(player.ObjectId, effectId, ct);

            frozen = true;
        }

        return frozen;
    }
}
