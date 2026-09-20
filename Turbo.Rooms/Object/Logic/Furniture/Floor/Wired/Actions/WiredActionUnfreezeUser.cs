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

/// <summary>Lets the selected users walk again and clears a freeze effect they were given.</summary>
[RoomObjectLogic("wf_act_unfreeze")]
public class WiredActionUnfreezeUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.UNFREEZE_USER;

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var freezeEffects = _roomGrain._wiredConfig.FreezeEffectIds;
        var thawed = false;

        foreach (var avatar in GetAvatars(ctx.GetSelection(this)))
        {
            if (!avatar.IsFrozen)
                continue;

            avatar.SetFrozen(false);

            // Whatever the freeze put on comes back off, and only that: an effect the avatar was
            // already wearing is not a freeze effect and is left alone.
            if (avatar.EffectId > 0 && freezeEffects.Contains(avatar.EffectId))
                await _roomGrain.AvatarModule.SetAvatarEffectAsync(avatar.ObjectId, 0, ct);

            thawed = true;
        }

        return thawed;
    }
}
