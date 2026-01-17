using System;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

public sealed class UserTypeVariable(RoomGrain roomGrain)
    : WiredVariable(roomGrain),
        IWiredInternalVariable
{
    protected override void Configure(IWiredVariableDefinition def)
    {
        def.Name = "@type";
        def.TargetType = WiredVariableTargetType.User;
        def.AvailabilityType = WiredAvailabilityType.Internal;
        def.InputSourceType = WiredInputSourceType.UserSource;
        def.Flags =
            WiredVariableFlags.HasValue
            | WiredVariableFlags.AlwaysAvailable
            | WiredVariableFlags.HasTextConnector;
        def.TextConnectors = Enum.GetValues<RoomObjectType>()
            .ToDictionary(v => (int)v, v => RoomObjectTypeExtensions.GetString(v));
    }

    public override bool CanBind(in IWiredVariableBinding binding) => binding.TargetId is not null;

    public override bool TryGet(
        in IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        out int value
    )
    {
        value = 0;

        if (
            !_roomGrain._state.AvatarsByObjectId.TryGetValue(
                binding.TargetId!.Value,
                out var avatar
            )
        )
            return false;

        value = (int)avatar.AvatarType;

        return true;
    }
}
