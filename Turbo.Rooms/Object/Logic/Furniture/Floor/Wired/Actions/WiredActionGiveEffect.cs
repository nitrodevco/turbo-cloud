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
/// Puts an avatar effect on the selected users (Flash <c>ActionTypeCodes.GIVE_EFFECT</c> 52).
/// <para>
/// Int params, as the editor writes them (<c>actiontypes._-Xm.readIntParamsFromForm</c>): the
/// effect id (0 to 10000, the editor starts at 13), the priority (0 to 2, starts at 2) and the
/// type radio (0 or 1). The box is not in Habbo's furnidata and its labels are not in the hotel
/// texts, so what the priority and the type do is unknown: both are kept as saved and the effect
/// is put on regardless (inferred from the box's name and its effect id field). Effect 0 takes
/// the worn effect off, as the room's other effect setters treat 0.
/// </para>
/// The classname is this server's (no official furni exists yet); a hotel may also point a
/// definition's logic column at it.
/// </summary>
[RoomObjectLogic("wf_act_give_effect")]
public class WiredActionGiveEffect(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_EFFECT = 0;

    // The editor's starting values (NumberInputParam(13, 0, 10000), NumberInputParam(2, 0, 2)).
    private const int DEFAULT_EFFECT = 13;
    private const int MAX_PRIORITY = 2;

    public override int WiredCode => (int)WiredActionType.GIVE_EFFECT;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, _roomGrain._wiredConfig.MaxEffectId, DEFAULT_EFFECT),
            new WiredRangeParamRule(0, MAX_PRIORITY, MAX_PRIORITY),
            new WiredBoolParamRule(false),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var effectId = GetIntParamOrDefault(PARAM_EFFECT, DEFAULT_EFFECT);
        var changed = false;

        foreach (var avatar in GetAvatars(ctx.GetSelection(this)))
            changed |= await AvatarModule.SetAvatarEffectAsync(avatar.ObjectId, effectId, ct);

        return changed;
    }
}
