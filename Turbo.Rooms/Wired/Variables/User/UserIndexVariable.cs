using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

public sealed class UserIndexVariable(RoomGrain roomGrain)
    : WiredVariable(roomGrain),
        IWiredInternalVariable
{
    protected override void Configure(IWiredVariableDefinition def)
    {
        def.Name = "@index";
        def.TargetType = WiredVariableTargetType.User;
        def.AvailabilityType = WiredAvailabilityType.Internal;
        def.InputSourceType = WiredInputSourceType.UserSource;
        def.Flags = WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;
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
            !_roomGrain._state.AvatarsByPlayerId.TryGetValue(
                binding.TargetId!.Value,
                out var objectId
            )
        )
            return false;

        value = objectId.Value;

        return true;
    }
}
