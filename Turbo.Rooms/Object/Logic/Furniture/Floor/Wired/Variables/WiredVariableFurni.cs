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

[RoomObjectLogic("wf_var_furni")]
public class WiredVariableFurni(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableType.FURNI_VARIABLE;

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntRangeRule(0, 1, 0),
            new WiredIntEnumRule<WiredAvailabilityType>(WiredAvailabilityType.Temporary),
        ];

    public override WiredVariableTargetType GetVariableTargetType() =>
        WiredVariableTargetType.Furni;

    public override WiredAvailabilityType GetVariableAvailabilityType() =>
        (WiredAvailabilityType)WiredData.IntParams[1];

    public override WiredInputSourceType GetVariableInputSourceType() =>
        WiredInputSourceType.FurniSource;

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        try
        {
            _hasValue = WiredData.IntParams[0] == 1;
        }
        catch { }

        await base.FillInternalDataAsync(ct);
    }
}
