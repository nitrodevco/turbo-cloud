using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms;

/// <summary>
/// The ban duration identifiers the client sends in <c>BanUserWithDuration</c>, as named in its
/// avatar menu actions.
/// </summary>
public static class RoomBanTypes
{
    public const string HOUR = "RWUAM_BAN_USER_HOUR";
    public const string DAY = "RWUAM_BAN_USER_DAY";
    public const string PERMANENT = "RWUAM_BAN_USER_PERM";

    public static bool TryParse(string? value, out RoomBanDurationType duration)
    {
        switch (value)
        {
            case HOUR:
                duration = RoomBanDurationType.Hour;
                return true;
            case DAY:
                duration = RoomBanDurationType.Day;
                return true;
            case PERMANENT:
                duration = RoomBanDurationType.Permanent;
                return true;
            default:
                duration = default;
                return false;
        }
    }
}
