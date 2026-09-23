using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>How high the avatar stands, in hundredths of a tile, as furni altitudes are counted.</summary>
public sealed class UserAltitudeVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomAvatar>(roomGrain)
{
    protected override string VariableName => "@altitude";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Position;

    protected override ushort Order => 10;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse(avatar.Z.ToInt());
}
