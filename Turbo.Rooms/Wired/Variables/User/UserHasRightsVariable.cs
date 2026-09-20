using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether the player was given rights in this room. The owner needs none, so this is false for them; use @is_owner for that.</summary>
public sealed class UserHasRightsVariable(RoomGrain roomGrain)
    : UserVariable<IRoomPlayer>(roomGrain)
{
    private const int TRUE = 1;
    private const int FALSE = 0;

    protected override string VariableName => "@has_rights";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    protected override ushort Order => 30;

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override WiredVariableValue GetValueForAvatar(IRoomPlayer avatar) =>
        WiredVariableValue.Parse(
            _roomGrain.SecurityModule.HasRights(avatar.PlayerId) ? TRUE : FALSE
        );
}
