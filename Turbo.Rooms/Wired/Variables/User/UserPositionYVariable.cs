using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>The tile the avatar stands on, along y. Writing it moves them there.</summary>
public sealed class UserPositionYVariable(RoomGrain roomGrain) : UserPlacementVariable(roomGrain)
{
    protected override string VariableName => "@position.y";

    protected override ushort Order => 30;

    protected override (int X, int Y) ApplyTile(IRoomAvatar avatar, int value) => (avatar.X, value);

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse(avatar.Y);
}
