using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>The tile the avatar stands on, along x. Writing it moves them there.</summary>
public sealed class UserPositionXVariable(RoomGrain roomGrain) : UserPlacementVariable(roomGrain)
{
    protected override string VariableName => "@position.x";

    protected override ushort Order => 40;

    protected override (int X, int Y) ApplyTile(IRoomAvatar avatar, int value) => (value, avatar.Y);

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse(avatar.X);
}
