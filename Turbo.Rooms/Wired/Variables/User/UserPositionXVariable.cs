using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

public sealed class UserPositionXVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.User,
                "@position.x",
                WiredVariableIdBuilder.WiredVarSubBand.Position,
                20
            ),
            VariableName = "@position.x",
            VariableType = WiredVariableType.Internal,
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.User,
            Flags =
                WiredVariableFlags.HasValue
                | WiredVariableFlags.CanWriteValue
                | WiredVariableFlags.AlwaysAvailable,
            TextConnectors = [],
        };

    public override bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = 0;

        if (
            !CanBind(binding)
            || !_roomGrain._state.AvatarsByPlayerId.TryGetValue(binding.TargetId, out var objectId)
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar)
        )
            return false;

        value = avatar.X;

        return true;
    }

    public override Task<bool> SetValueAsync(
        WiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    )
    {
        return Task.FromResult(true);
    }
}
