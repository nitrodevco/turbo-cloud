using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when the triggerer is of the wanted kind. Param 0 is a bitmask as the editor saves it
/// (<c>wiredfurni.params.usertype.1</c> / <c>.2</c> / <c>.4</c>: 1 Habbo, 2 pet, 4 bot); the string
/// param names one avatar, empty for any. The selection holds players, pets and bots alike.
/// </summary>
[RoomObjectLogic("wf_cnd_triggerer_match")]
public class WiredConditionExecutorMatches(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int USER_TYPE_PLAYER = 1;
    private const int USER_TYPE_PET = 2;
    private const int USER_TYPE_BOT = 4;

    public override int WiredCode => (int)WiredConditionType.TRIGGERER_MATCHES;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredRangeParamRule(0, 7, 1)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var mask = GetIntParamOrDefault(0, USER_TYPE_PLAYER);
        var wantedName = GetStringParam();

        return Quantify(
            GetAvatars(ctx.GetSelection(this))
                .Select(avatar =>
                    (mask & KindOf(avatar)) != 0
                    && (wantedName.Length == 0 || NamesMatch(avatar.Name, wantedName))
                ),
            true
        );
    }

    private static int KindOf(IRoomAvatar avatar) =>
        avatar switch
        {
            IRoomPlayer => USER_TYPE_PLAYER,
            IRoomPet => USER_TYPE_PET,
            IRoomBot => USER_TYPE_BOT,
            _ => 0,
        };
}
