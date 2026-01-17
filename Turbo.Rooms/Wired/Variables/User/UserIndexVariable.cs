using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

public sealed class UserIndexVariable(RoomGrain roomGrain)
    : WiredVariable(roomGrain),
        IWiredInternalVariable
{
    public override string VariableName { get; set; } = "@index";

    public override WiredVariableTargetType GetVariableTargetType() => WiredVariableTargetType.User;

    public override WiredAvailabilityType GetVariableAvailabilityType() =>
        WiredAvailabilityType.Internal;

    public override WiredInputSourceType GetVariableInputSourceType() =>
        WiredInputSourceType.UserSource;

    public override WiredVariableFlags GetVariableFlags()
    {
        var flags = base.GetVariableFlags();

        flags = flags.Add(WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable);

        return flags;
    }

    public override bool TryGet(in IWiredVariableBinding binding, out int value)
    {
        value = 0;

        if (!_roomGrain._state.AvatarsByPlayerId.TryGetValue(binding.TargetId, out var objectId))
            return false;

        value = objectId.Value;

        return true;
    }
}
