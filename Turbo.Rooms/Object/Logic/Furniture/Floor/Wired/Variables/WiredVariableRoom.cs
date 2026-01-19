using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.IntParams;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_room")]
public class WiredVariableRoom(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableType.Global;

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntEnumRule<WiredAvailabilityType>(
                WiredAvailabilityType.RoomActive,
                WiredAvailabilityType.RoomActive,
                WiredAvailabilityType.Persistent,
                WiredAvailabilityType.Shared
            ),
        ];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        [
            new WiredVariableInfoAndValueSnapshot()
            {
                ContextType = WiredContextType.VariableInfoAndValue,
                Variable = GetVarSnapshot(),
                Value = TryGet(
                    new WiredVariableBinding()
                    {
                        TargetType = WiredVariableTargetType.Global,
                        TargetId = -1,
                    },
                    out var value
                )
                    ? value
                    : 0,
            },
        ];

    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = _variableId,
            VariableName = WiredData.StringParam,
            AvailabilityType = (WiredAvailabilityType)WiredData.IntParams[0],
            TargetType = WiredVariableTargetType.Global,
            Flags =
                WiredVariableFlags.HasValue
                | WiredVariableFlags.CanWriteValue
                | WiredVariableFlags.CanInterceptChanges
                | WiredVariableFlags.AlwaysAvailable
                | WiredVariableFlags.CanReadLastUpdateTime,
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
