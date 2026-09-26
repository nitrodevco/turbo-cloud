using Turbo.Primitives.Navigator.Enums;

namespace Turbo.Primitives.Rooms.Enums;

public static class RoomControllerTypeExtensions
{
    /// <summary>
    /// Whether a controller of this level may do what a room's moderation setting governs (who
    /// may kick, mute or ban). The owner always may; below that the setting decides, and a
    /// setting this does not name allows nobody else. Kicking, muting and the ban list each
    /// wrote this table out, so it lives beside the level it is asked of.
    /// </summary>
    public static bool IsAllowedBy(this RoomControllerType level, ModSettingType setting) =>
        level >= RoomControllerType.Owner
        || setting switch
        {
            ModSettingType.All => true,
            ModSettingType.Rights => level >= RoomControllerType.Rights,
            ModSettingType.GroupRights => level >= RoomControllerType.GroupRights,
            ModSettingType.RightsOrGroup => level >= RoomControllerType.Rights,
            _ => false,
        };
}
