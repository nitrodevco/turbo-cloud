using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

public sealed class UserPositionXVariable(RoomGrain roomGrain)
    : WiredVariable(roomGrain),
        IWiredInternalVariable
{
    protected override void Configure(IWiredVariableDefinition def)
    {
        def.Name = "@position.x";
        def.TargetType = WiredVariableTargetType.User;
        def.AvailabilityType = WiredAvailabilityType.Internal;
        def.InputSourceType = WiredInputSourceType.UserSource;
        def.Flags =
            WiredVariableFlags.HasValue
            | WiredVariableFlags.CanWriteValue
            | WiredVariableFlags.AlwaysAvailable;
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
                out var avatarItem
            )
        )
            return false;

        value = avatarItem.X;

        return true;
    }

    public override Task<bool> SetValueAsync(
        IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    )
    {
        return Task.FromResult(true);
    }
}
