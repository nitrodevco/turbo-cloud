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

    /// <summary>"Picked an invalid effect!", for an option the editor does not offer (0 to 4, actiontypes/_-Q1s.as:43-47, AS3 WIN63-202610131318).</summary>
    public const string ERROR_INVALID_EFFECT = "wiredfurni.error.invalid_freeze_effect";

    public override int WiredCode => (int)WiredActionType.FREEZE_USER;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, FREEZE_EFFECT_IDS.Length - 1, 0)
            {
                ErrorKey = ERROR_INVALID_EFFECT,
            },
            new WiredBoolParamRule(false),
        ];

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

            // The room hears the user is awake before it sees the effect (official client
            // capture 2026-10-24, Freeze on a rights holder seen by the owner, 1/1).
            AvatarModule.WakeAvatar(player);

            // The effect goes on at the next avatar cycle, and only on a user still frozen then:
            // Freeze and Unfreeze in one execution send no effect at all (official client
            // capture 2026-10-25, LAB-W3 wlab9 action.31.X1, 4/4; AvatarEffect 218 340 to 460 ms
            // after the fire, action.31.B1, action.31.R1b). An effect put on in between (a
            // teleport's) is not overwritten.
            var worn = player.EffectId;

            if (effectId > 0)
                RunAtNextAvatarCycle(token =>
                    IsInRoom(player) && player.IsFrozen && player.EffectId == worn
                        ? AvatarModule.SetAvatarEffectAsync(player.ObjectId, effectId, token)
                        : Task.FromResult(false)
                );

            frozen = true;
        }

        return frozen;
    }
}
