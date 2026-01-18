using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureWallItemOffsetVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = _variableId,
            VariableName = "@wallitem_offset",
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.Furni,
            Flags = WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue,
            TextConnectors = [],
        };

    public override bool CanBind(in IWiredVariableBinding binding) =>
        base.CanBind(binding) && _roomGrain._state.WallItemsById.ContainsKey(binding.TargetId);

    public override bool TryGet(in IWiredVariableBinding binding, out int value)
    {
        value = 0;

        if (!_roomGrain._state.WallItemsById.TryGetValue(binding.TargetId, out var wallItem))
            return false;

        value = wallItem.WallOffset;

        return true;
    }

    public override Task<bool> SetValueAsync(
        IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    )
    {
        if (!_roomGrain._state.WallItemsById.TryGetValue(binding.TargetId, out var wallItem))
            return Task.FromResult(false);

        return Task.FromResult(true);
    }
}
