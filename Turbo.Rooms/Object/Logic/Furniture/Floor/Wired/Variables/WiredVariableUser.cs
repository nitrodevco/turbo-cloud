using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.IntParams;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_user")]
public class WiredVariableUser(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableType.USER_VARIABLE;

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntEnumRule<WiredAvailabilityType>(WiredAvailabilityType.Temporary),
            new WiredIntRangeRule(0, 1, 0),
        ];

    public override WiredVariableTargetType GetVariableTargetType() => WiredVariableTargetType.User;

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        try
        {
            StorageType = (WiredAvailabilityType)WiredData.IntParams[0];
            _hasValue = WiredData.IntParams[1] == 1;
        }
        catch { }
    }
}
