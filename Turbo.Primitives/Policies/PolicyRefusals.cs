using System.Collections.Immutable;
using Turbo.Primitives.Messages.Outgoing.Notifications;

namespace Turbo.Primitives.Policies;

/// <summary>
/// How a game policy's reason reaches the player: Habbo's notification pop-up
/// (<c>NotificationDialog</c>) of a type of the policy's kind, its <c>message</c> the reason, as a
/// play policy's refusal at login is shown. A hotel styles each with
/// <c>notification.&lt;type&gt;</c> as any notification type.
/// </summary>
public static class PolicyRefusals
{
    public const string PURCHASE = "turbo.purchase.refused";
    public const string ROOM_ENTRY = "turbo.room_entry.refused";
    public const string PLACEMENT = "turbo.placement.refused";

    public static NotificationDialogMessageComposer Notification(string type, string reason) =>
        new()
        {
            NotificationType = type,
            Parameters = ImmutableDictionary<string, string>
                .Empty.Add("display", "POP_UP")
                .Add("message", reason),
        };
}
