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
using Turbo.Rooms.Wired.IntParams;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

[RoomObjectLogic("wf_xtra_mov_carry_users")]
public class WiredAddonCarryUsers(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.CARRY_USERS;

    protected WiredCarryUserType _carryUserType = WiredCarryUserType.StandingOnFurni;

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [new WiredIntEnumRule<WiredCarryUserType>(WiredCarryUserType.StandingOnFurni)];

    public override Task<bool> MutatePolicyAsync(WiredProcessingContext ctx, CancellationToken ct)
    {
        return Task.FromResult(true);
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        try
        {
            _carryUserType = (WiredCarryUserType)WiredData.IntParams[0];
        }
        catch { }
    }
}
