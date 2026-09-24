namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// Why a create or edit was refused. Looked up as <c>group.edit.fail.&lt;n&gt;</c>, so as with
/// <see cref="GuildJoinFailedType"/> only the members here have text behind them.
///
/// <see cref="ClubRequired"/> opens the Habbo Club window instead of an alert.
/// </summary>
public enum GuildEditFailedType
{
    /// <summary>The chosen room is already some group's homeroom.</summary>
    RoomAlreadyHomeroom = 0,

    InvalidName = 1,

    /// <summary>Opens the club window rather than an alert.</summary>
    ClubRequired = 2,

    /// <summary>The creator is at the limit of groups one player may belong to.</summary>
    TooManyGroups = 3,

    AccountLocked = 4,
}
