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
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.User,
                "@type",
                WiredVariableIdBuilder.WiredVarSubBand.Base,
                10
            ),
            VariableName = "@type",
            VariableType = WiredVariableType.Internal,
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.User,
            Flags =
                WiredVariableFlags.HasValue
                | WiredVariableFlags.AlwaysAvailable
                | WiredVariableFlags.HasTextConnector,
            TextConnectors = Enum.GetValues<RoomObjectType>()
                .ToDictionary(v => (int)v, v => RoomObjectTypeExtensions.GetString(v)),
        };

    public override bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = default;

        var snapshot = GetVarSnapshot();

        if (
            (binding.TargetType != snapshot.TargetType)
            || !_roomGrain._state.AvatarsByPlayerId.TryGetValue(binding.TargetId, out var objectId)
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar)
        )
            return false;

        value = (int)avatar.AvatarType;

        return true;
    }
}
