using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// Whether the player has rights in this room: given rights, or the room's own owner, whom the
/// official client's inspection shows holding it beside <c>@is_owner</c>.
/// </summary>
public sealed class UserHasRightsVariable(RoomGrain roomGrain)
    : UserFlagVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@has_rights";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    protected override ushort Order => 30;

    protected override bool HasFlag(IRoomPlayer avatar) =>
        SecurityModule.HasRights(avatar.PlayerId) || SecurityModule.IsOwnedBy(avatar.PlayerId);
}
