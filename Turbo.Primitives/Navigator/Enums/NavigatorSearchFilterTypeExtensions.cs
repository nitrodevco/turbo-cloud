using System;

namespace Turbo.Primitives.Navigator.Enums;

public static class NavigatorSearchFilterTypeExtensions
{
    public static string ToLegacyString(this NavigatorSearchFilterType filter) =>
        filter switch
        {
            NavigatorSearchFilterType.Anything => "",
            NavigatorSearchFilterType.RoomName => "roomname",
            NavigatorSearchFilterType.Owner => "owner",
            NavigatorSearchFilterType.Tag => "tag",
            NavigatorSearchFilterType.Group => "group",
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
        };

    public static NavigatorSearchFilterType FromLegacyString(this string filter) =>
        filter switch
        {
            "" => NavigatorSearchFilterType.Anything,
            "roomname" => NavigatorSearchFilterType.RoomName,
            "owner" => NavigatorSearchFilterType.Owner,
            "tag" => NavigatorSearchFilterType.Tag,
            "group" => NavigatorSearchFilterType.Group,
            _ => NavigatorSearchFilterType.Anything,
        };
}
