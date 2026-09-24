using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when the triggering users wear a group badge: any group when the string param is
/// empty, otherwise the group with that id.
/// </summary>
[RoomObjectLogic("wf_cnd_actor_in_group")]
public class WiredConditionGroupMember(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.ACTOR_IS_GROUP_MEMBER;

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        int? wantedGroupId =
            int.TryParse(_wiredData.StringParam, out var parsed) && parsed > 0 ? parsed : null;
        var players = GetPlayers(ctx.GetSelection(this));

        return Quantify(
            players.Select(player =>
            {
                var groupId = player is RoomPlayerAvatar avatar ? avatar.GuildId : -1;

                return wantedGroupId is null ? groupId > 0 : groupId == wantedGroupId;
            }),
            true
        );
    }
}
