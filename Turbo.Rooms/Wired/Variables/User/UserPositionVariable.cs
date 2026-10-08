using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// <c>@position</c> of a user: x and y in one value, <c>(x &lt;&lt; 8) | y</c>, as the furni one
/// (the official client shows 4106 for x 16, y 10). Writing it moves them there.
/// </summary>
public sealed class UserPositionVariable(RoomGrain roomGrain) : UserPlacementVariable(roomGrain)
{
    protected override string VariableName => "@position";

    // After @altitude (10), as the official list has it.
    protected override ushort Order => 5;

    protected override (int X, int Y) ApplyTile(IRoomAvatar avatar, int value) =>
        ((value >> 8) & 0xFF, value & 0xFF);

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse((avatar.X << 8) | avatar.Y);
}
