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

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Lets the selected users walk again and clears a freeze effect they were given.</summary>
[RoomObjectLogic("wf_act_unfreeze")]
public class WiredActionUnfreezeUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.UNFREEZE_USER;

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
            ],
        ];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var freezeEffects = _roomGrain._roomConfig.WiredFreezeEffectIds;
        var thawed = false;

        foreach (var player in GetPlayers(ctx.GetSelection(this)))
        {
            if (!player.IsFrozen)
                continue;

            player.SetFrozen(false);
            _roomGrain.WiredSystem.SetFreezeCancelsOnTeleport(player.ObjectId, false);

            if (player.EffectId > 0 && freezeEffects.Contains(player.EffectId))
                await _roomGrain.AvatarModule.SetAvatarEffectAsync(player.ObjectId, 0, ct);

            thawed = true;
        }

        return thawed;
    }
}
