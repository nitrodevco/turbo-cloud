using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

public sealed class UserTypeVariable(RoomGrain roomGrain)
    : WiredVariable(roomGrain),
        IWiredInternalVariable
{
    public override string VariableName { get; set; } = "@type";

    public override WiredVariableTargetType GetVariableTargetType() => WiredVariableTargetType.User;

    public override WiredAvailabilityType GetVariableAvailabilityType() =>
        WiredAvailabilityType.Internal;

    public override WiredInputSourceType GetVariableInputSourceType() =>
        WiredInputSourceType.UserSource;

    public override WiredVariableFlags GetVariableFlags()
    {
        var flags = base.GetVariableFlags();

        flags = flags.Add(
            WiredVariableFlags.HasValue
                | WiredVariableFlags.AlwaysAvailable
                | WiredVariableFlags.HasTextConnector
        );

        return flags;
    }

    public override Dictionary<int, string> GetTextConnectors() =>
        Enum.GetValues<RoomObjectType>()
            .ToDictionary(v => (int)v, v => RoomObjectTypeExtensions.GetString(v));

    public override bool TryGet(in IWiredVariableBinding binding, out int value)
    {
        value = 0;

        if (!_roomGrain._state.AvatarsByObjectId.TryGetValue(binding.TargetId, out var avatar))
            return false;

        value = (int)avatar.AvatarType;

        return true;
    }
}
