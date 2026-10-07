using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.WiredTrading;

/// <summary>
/// The map keys a wired chest's stuff data carries, which the client's chest window, infostand
/// and room object read (<c>WiredChestWrapperView</c>). Flags are "1" or "0".
/// </summary>
public static class WiredChestData
{
    public const string LOCKED = "locked";
    public const string AUTO_LOCK = "auto_lock";
    public const string CAPACITY = "capacity";
    public const string CONTENTS_COUNT = "contents_count";
    public const string CAPACITY_LEVEL = "capacity_level";
    public const string NAME = "chest_name";
    public const string DESCRIPTION = "chest_desc";
    public const string EVERYONE_CAN_OPEN = "everyone_can_open";
    public const string EVERYONE_CAN_DONATE = "everyone_can_donate";
    public const string STATE_CONTROL_MODE = "state_control_mode";
    public const string IS_WIRED_ENABLED = "is_wired_enabled";
    public const string NOTIFY_MODE = "notify_mode";
    public const string PREVIEW_MODE = "preview_mode";
    public const string PREVIEW_AMOUNT = "preview_amount";

    /// <summary>The items an open furni chest shows above itself (see <see cref="FormatVisuals"/>).</summary>
    public const string VISUALS = "visuals";

    public const string NOTIFICATION_CHEST_FULL = "notification_chest_full";
    public const string NOTIFICATION_DONATION = "notification_donation";
    public const string NOTIFICATION_SOMEONE_WITHDRAWS = "notification_someone_withdraws";
    public const string NOTIFICATION_CHEST_EMPTY = "notification_chest_empty";
    public const string NOTIFICATION_WIRED_TRANSACTION = "notification_wired_transaction";

    public const string TRUE = "1";
    public const string FALSE = "0";

    /// <summary>The state the client draws a chest open in; closed is the one below it.</summary>
    public const int OPEN_STATE = 1;
    public const int CLOSED_STATE = 0;

    public static string Flag(bool value) => value ? TRUE : FALSE;

    /// <summary>
    /// <c>isWall,typeId[,posterId]</c> per item, separated by <c>;</c>. Unlike the flags, the
    /// wall flag here is the word <c>true</c> or <c>false</c>: the client compares it to "true".
    /// </summary>
    public static string FormatVisuals(IEnumerable<ChestItemTypeSnapshot> types) =>
        string.Join(
            ';',
            types.Select(type =>
            {
                var wall = type.IsWallItem ? "true" : "false";

                return type.LegacyPosterId.Length > 0
                    ? $"{wall},{type.TypeId},{type.LegacyPosterId}"
                    : $"{wall},{type.TypeId}";
            })
        );
}
