using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Furni moved by this stack carry the users on them. Param 0: only users on the moving
/// furni itself, or everyone on its tiles.
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

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.CarryUsers = GetIntParamOrDefault(0, WiredCarryUserType.StandingOnFurni);

        return Task.FromResult(true);
    }
}
