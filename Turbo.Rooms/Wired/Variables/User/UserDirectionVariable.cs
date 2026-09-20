using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// Which way the avatar's body faces: zero is north and it counts up an eighth of a turn at a
/// time, clockwise. Writing it turns them in place, head and body together.
/// </summary>
public sealed class UserDirectionVariable(RoomGrain roomGrain) : UserPlacementVariable(roomGrain)
{
    protected override string VariableName => "@direction";

    protected override ushort Order => 20;

    protected override WiredVariableFlags Flags => base.Flags | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        Enum.GetValues<Rotation>()
            .Where(x => x != Rotation.None)
            .ToDictionary(x => WiredVariableValue.Parse((int)x), x => x.ToString());

    protected override bool IsRotation => true;

    // Never reached: the base turns the avatar instead of moving it.
    protected override (int X, int Y) ApplyTile(IRoomAvatar avatar, int value) =>
        (avatar.X, avatar.Y);

    protected override Rotation? ApplyRotation(int value) =>
        Enum.IsDefined((Rotation)value) && (Rotation)value != Rotation.None
            ? (Rotation)value
            : null;

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse((int)avatar.Rotation);
}
