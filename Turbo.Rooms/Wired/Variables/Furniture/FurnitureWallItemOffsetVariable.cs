using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureWallItemOffsetVariable(RoomGrain roomGrain)
    : WiredVariable(roomGrain),
        IWiredInternalVariable
{
    public override string VariableName { get; set; } = "@wallitem_offset";

    public override WiredVariableTargetType GetVariableTargetType() =>
        WiredVariableTargetType.Furni;

    public override WiredAvailabilityType GetVariableAvailabilityType() =>
        WiredAvailabilityType.Internal;

    public override WiredInputSourceType GetVariableInputSourceType() =>
        WiredInputSourceType.FurniSource;

    public override WiredVariableFlags GetVariableFlags()
    {
        var flags = base.GetVariableFlags();

        flags = flags.Add(WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue);

        return flags;
    }

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
