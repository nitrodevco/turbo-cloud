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
/// True when the triggering users wear a group badge: the room's own group when the string
/// param is empty (the editor's "Current group", <c>wiredfurni.params.grouptype.0</c>), otherwise
/// the group with that id. A room without a group has no current group to match.
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

    // The users' quantifier radio (quantifierType 2 in the official Open of code 10 and 21, 2026-10-20T17-37-19).
    protected override WiredQuantifierType QuantifierType => WiredQuantifierType.Users;

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var wantedGroupId = GetPositiveIdParam() ?? State.RoomSnapshot?.Guild?.GuildId.Value;
        // A bot is in no group: one triggering fails the box (official client capture
        // 2026-10-24, W3 lab condition.10.B2, 4/4).
        var users = GetUsers(ctx.GetSelection(this));

        if (wantedGroupId is not > 0)
            return Quantify(users.Select(_ => false), QuantifierRequiresAll);

        return Quantify(
            users.Select(player =>
            {
                var groupId = player is RoomPlayerAvatar avatar ? avatar.GuildId : -1;

                return groupId == wantedGroupId;
            }),
            QuantifierRequiresAll
        );
    }
}
