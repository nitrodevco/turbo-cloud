using System.Collections.Generic;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

/// <summary>
/// The notifications a chest's owner can ask for (ChestNotificationSettingsUI), as the
/// NotificationDialog types Habbo's client data knows: <c>notification.wired_chests.&lt;kind&gt;</c>
/// is a persistent bubble with its own <c>chests_icon_*</c> image and
/// <c>notification.wired_chests.&lt;kind&gt;.message</c> its text, filled from <c>%chest_name%</c> and
/// <c>%user_name%</c> (official external variables and texts). Withdrawals, emptying and wired
/// transactions are offered only on a wired chest: the client disables that section unless
/// <c>is_wired_enabled</c> (ChestNotificationSettingsUI.as:106, WIN63-202610201032). Which move
/// raises which kind is inferred from the option texts ("when someone makes a donation", "when
/// someone withdraws from chest", "for any Wired transaction").
/// </summary>
public static class WiredChestNotices
{
    public const string CHEST_FULL = "wired_chests.chest_full";
    public const string CHEST_EMPTY = "wired_chests.chest_empty";
    public const string DONATION = "wired_chests.donation";
    public const string SOMEONE_WITHDRAWS = "wired_chests.someone_withdraws";
    public const string WIRED_TRANSACTION = "wired_chests.chest_wired_transaction";

    public const string PARAM_CHEST_NAME = "chest_name";
    public const string PARAM_USER_NAME = "user_name";
    public const string PARAM_LINK = "linkUrl";

    /// <summary>
    /// The notification types a successful move raises, before the owner's choices: a deposit or
    /// withdrawal by hand by anyone but the owner, any move wired made, and the chest becoming
    /// full or empty.
    /// </summary>
    public static List<string> For(
        bool deposit,
        WiredTransactionType type,
        bool byOwner,
        int amountBefore,
        int amountAfter,
        int capacity
    )
    {
        var kinds = new List<string>();

        if (type == WiredTransactionType.Manual)
        {
            if (!byOwner)
                kinds.Add(deposit ? DONATION : SOMEONE_WITHDRAWS);
        }
        else
            kinds.Add(WIRED_TRANSACTION);

        if (deposit && capacity > 0 && amountBefore < capacity && amountAfter >= capacity)
            kinds.Add(CHEST_FULL);

        if (!deposit && amountBefore > 0 && amountAfter <= 0)
            kinds.Add(CHEST_EMPTY);

        return kinds;
    }

    /// <summary>The stuff data flag that turns a kind on, and whether only a wired chest offers it.</summary>
    public static (string Flag, bool WiredOnly) PreferenceOf(string kind) =>
        kind switch
        {
            CHEST_FULL => (WiredChestData.NOTIFICATION_CHEST_FULL, false),
            DONATION => (WiredChestData.NOTIFICATION_DONATION, false),
            SOMEONE_WITHDRAWS => (WiredChestData.NOTIFICATION_SOMEONE_WITHDRAWS, true),
            CHEST_EMPTY => (WiredChestData.NOTIFICATION_CHEST_EMPTY, true),
            _ => (WiredChestData.NOTIFICATION_WIRED_TRANSACTION, true),
        };
}
