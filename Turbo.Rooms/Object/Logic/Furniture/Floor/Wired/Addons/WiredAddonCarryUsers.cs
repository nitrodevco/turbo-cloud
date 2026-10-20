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
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Furni moved by this stack carry the users on them. Param 0: only users on the moving
/// furni itself, or everyone on its tiles. The user source narrows who is carried
/// (<c>wiredfurni.params.sources.users.title.carry</c>, AS3 wired_setup/addons/CarryUsers.as:47);
/// a fresh box carries everyone (official server: users [[0,200,201,900]], default [900],
/// 2026-10-20T17-35-39-Open-wait).
/// </summary>
[RoomObjectLogic("wf_xtra_mov_carry_users")]
public class WiredAddonCarryUsers(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.CARRY_USERS;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<WiredCarryUserType>(WiredCarryUserType.StandingOnFurni)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users.DefaultingTo(WiredPlayerSourceType.AllRoomUsers)];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.CarryUsers = GetIntParamOrDefault(0, WiredCarryUserType.StandingOnFurni);
        ctx.Policy.CarryUserIds = GetPlayerSources()[0].Contains(WiredPlayerSourceType.AllRoomUsers)
            ? null
            : WiredSlotSelection.ForUserSlot(this, ctx, 0).SelectedAvatarIds;

        return Task.FromResult(true);
    }
}
