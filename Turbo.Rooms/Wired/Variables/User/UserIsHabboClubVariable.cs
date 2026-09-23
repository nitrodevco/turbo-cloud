using System;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// Whether the player holds a Habbo Club membership right now. The avatar carries the moment the
/// membership runs out rather than a flag, so this stays right for a player who is still standing
/// in the room when theirs expires, with nothing to push and no timer to run.
/// </summary>
public sealed class UserIsHabboClubVariable(RoomGrain roomGrain)
    : UserFlagVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@is_hc";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    // Between @achievement_score (50) and @has_rights (30): what the player has earned above,
    // what they are allowed to do below. The neighbouring free slots, 60 and 20, stay reserved
    // for @level and @is_group_admin.
    protected override ushort Order => 40;

    protected override bool HasFlag(IRoomPlayer avatar) =>
        avatar.HabboClubExpiresAt is { } expiresAt && expiresAt > DateTime.UtcNow;
}
