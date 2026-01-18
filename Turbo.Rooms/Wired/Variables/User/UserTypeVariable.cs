using System;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

public sealed class UserTypeVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = _variableId,
            VariableName = "@type",
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.User,
            Flags =
                WiredVariableFlags.HasValue
                | WiredVariableFlags.AlwaysAvailable
                | WiredVariableFlags.HasTextConnector,
            TextConnectors = Enum.GetValues<RoomObjectType>()
                .ToDictionary(v => (int)v, v => RoomObjectTypeExtensions.GetString(v)),
        };

    public override bool TryGet(in IWiredVariableBinding binding, out int value)
    {
        value = 0;

        if (!_roomGrain._state.AvatarsByObjectId.TryGetValue(binding.TargetId, out var avatar))
            return false;

        value = (int)avatar.AvatarType;

        return true;
    }
}
