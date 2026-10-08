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
    /// <summary>
    /// The effect each option paints, as the editor names them: <c>wiredfurni.params.freeze.effect.0</c>
    /// to <c>.4</c> are <c>${fx_218}</c>, <c>${fx_12}</c>, <c>${fx_11}</c>, <c>${fx_53}</c>, <c>${fx_163}</c>.
    /// </summary>
    public static readonly int[] FREEZE_EFFECT_IDS = [218, 12, 11, 53, 163];

    public override int WiredCode => (int)WiredActionType.FREEZE_USER;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(0, 4, 0), new WiredBoolParamRule(false)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var effectIndex = GetIntParamOrDefault(0, 0);
        var cancelOnTeleport = GetIntParamOrDefault(1, false);
        var effectIds = FREEZE_EFFECT_IDS;
        var effectId = effectIndex < effectIds.Length ? effectIds[effectIndex] : 0;
        var frozen = false;

        foreach (var player in GetAvatars(ctx.GetSelection(this)))
        {
            await AvatarModule.StopWalkingAsync(player, ct);

            player.SetFrozen(true, cancelOnTeleport);

            if (effectId > 0)
                await AvatarModule.SetAvatarEffectAsync(player.ObjectId, effectId, ct);

            frozen = true;
        }

        return frozen;
    }
}
