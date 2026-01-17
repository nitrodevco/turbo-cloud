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
    protected override void Configure(IWiredVariableDefinition def)
    {
        def.Name = "@wallitem_offset";
        def.TargetType = WiredVariableTargetType.Furni;
        def.AvailabilityType = WiredAvailabilityType.Internal;
        def.InputSourceType = WiredInputSourceType.FurniSource;
        def.Flags = WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue;
    }

    public override bool CanBind(in IWiredVariableBinding binding) => binding.TargetId is not null;

    public override bool TryGet(
        in IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        out int value
    )
    {
        value = 0;

        if (!_roomGrain._state.WallItemsById.TryGetValue(binding.TargetId!.Value, out var wallItem))
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
        if (!_roomGrain._state.WallItemsById.TryGetValue(binding.TargetId!.Value, out var wallItem))
            return Task.FromResult(false);

        return Task.FromResult(true);
    }
}
