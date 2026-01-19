using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.IntParams;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_furni")]
public class WiredVariableFurni(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableType.Furni;

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntRangeRule(0, 1, 0),
            new WiredIntEnumRule<WiredAvailabilityType>(
                WiredAvailabilityType.RoomActive,
                WiredAvailabilityType.RoomActive,
                WiredAvailabilityType.Persistent
            ),
        ];

    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = _variableId,
            VariableName = WiredData.StringParam,
            AvailabilityType = (WiredAvailabilityType)WiredData.IntParams[1],
            TargetType = WiredVariableTargetType.Furni,
            Flags =
                (
                    WiredData.IntParams[0] == 1
                        ? WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue
                        : WiredVariableFlags.None
                )
                | WiredVariableFlags.CanCreateAndDelete
                | WiredVariableFlags.CanInterceptChanges
                | WiredVariableFlags.CanReadCreationTime,
            TextConnectors = [],
        };

    public override bool CanBind(in WiredVariableBinding binding) =>
        binding.TargetType == GetVarSnapshot().TargetType;

    public override bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = 0;

        if (_storageData.TryGet(binding.ToString(), out var stored))
            value = stored;

        return true;
    }
}
