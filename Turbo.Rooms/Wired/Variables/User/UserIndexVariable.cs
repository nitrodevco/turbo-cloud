using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

public sealed class UserIndexVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.User,
                "@index",
                WiredVariableIdBuilder.WiredVarSubBand.Base,
                20
            ),
            VariableName = "@index",
            VariableType = WiredVariableType.Internal,
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.User,
            Flags = WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable,
            TextConnectors = [],
        };

    public override bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = default;

        var snapshot = GetVarSnapshot();

        if (
            (binding.TargetType != snapshot.TargetType)
            || !_roomGrain._state.AvatarsByPlayerId.TryGetValue(binding.TargetId, out var objectId)
        )
            return false;

        value = objectId.Value;

        return true;
    }
}
