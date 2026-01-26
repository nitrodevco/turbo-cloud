using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_quest_chain")]
public class WiredVariableQuestChain(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.QuestChain;

    protected override WiredVariableSnapshot BuildVarSnapshot()
    {
        var variableName = WiredData.StringParam;
        var variableType = WiredVariableType.Created;
        var availabilityType = (WiredAvailabilityType)WiredData.IntParams[0];
        var targetType = WiredVariableTargetType.Global;
        var flags =
            WiredVariableFlags.HasValue
            | WiredVariableFlags.CanWriteValue
            | WiredVariableFlags.CanInterceptChanges
            | WiredVariableFlags.AlwaysAvailable
            | WiredVariableFlags.CanReadLastUpdateTime;
        var textConnectors = GetTextConnectors();
        var variableHash = WiredVariableHashBuilder.HashValues(
            variableName,
            availabilityType,
            targetType,
            flags,
            textConnectors
        );

        return new()
        {
            VariableId = _variableId,
            VariableName = variableName,
            VariableType = variableType,
            VariableHash = variableHash,
            AvailabilityType = availabilityType,
            TargetType = targetType,
            Flags = flags,
            TextConnectors = textConnectors,
        };
    }
}
