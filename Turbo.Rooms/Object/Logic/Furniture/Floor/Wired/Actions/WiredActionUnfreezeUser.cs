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

/// <summary>Lets the selected users walk again and puts back the effect they wore before the freeze.</summary>
[RoomObjectLogic("wf_act_unfreeze")]
public class WiredActionUnfreezeUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.UNFREEZE_USER;

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var freezeEffects = WiredActionFreezeUser.FREEZE_EFFECT_IDS;
        var thawed = false;

        foreach (var avatar in GetAvatars(ctx.GetSelection(this)))
        {
            if (!avatar.IsFrozen)
                continue;

            avatar.SetFrozen(false);

            // At the next avatar cycle a freeze effect still worn makes way for the effect worn
            // before the freeze: the 223 the user wore comes back (official client capture
            // 2026-10-25, LAB-W3 wlab9 action.32.R1b, 1/1), nothing worn sends 0 (action.32.R3,
            // action.32.B3). Any other effect put on since is left alone.
            RunAtNextAvatarCycle(token =>
                IsInRoom(avatar) && !avatar.IsFrozen && freezeEffects.Contains(avatar.EffectId)
                    ? AvatarModule.SetAvatarEffectAsync(
                        avatar.ObjectId,
                        avatar.EffectBeforeFreeze,
                        token
                    )
                    : Task.FromResult(false)
            );

            thawed = true;
        }

        return Task.FromResult(thawed);
    }
}
