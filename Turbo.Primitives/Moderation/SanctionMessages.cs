using System;
using System.Globalization;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Texts;

namespace Turbo.Primitives.Moderation;

/// <summary>
/// What a banned player is told, in the hotel's own words. The text is a hotel text
/// (<c>moderation.ban.message</c> for a ban with an end, <c>moderation.ban.message.permanent</c>
/// for one without), so a hotel can reword and translate it; <c>%0%</c> is the reason and
/// <c>%1%</c> when it ends.
/// </summary>
public static class SanctionMessages
{
    public const string BAN_MESSAGE = "moderation.ban.message";
    public const string BAN_MESSAGE_PERMANENT = "moderation.ban.message.permanent";

    private const string DEFAULT_BAN_MESSAGE =
        "You are banned from the hotel until %1%. Reason: %0%";
    private const string DEFAULT_BAN_MESSAGE_PERMANENT =
        "You are banned from the hotel. Reason: %0%";

    public static string BanMessage(PlayerSanctionSnapshot ban, IHotelTextProvider texts)
    {
        var permanent = ban.ExpiresAtUtc is null;
        var key = permanent ? BAN_MESSAGE_PERMANENT : BAN_MESSAGE;

        if (!texts.TryGetText(key, out var text))
            text = permanent ? DEFAULT_BAN_MESSAGE_PERMANENT : DEFAULT_BAN_MESSAGE;

        return text.Replace("%0%", ban.Reason, StringComparison.Ordinal)
            .Replace(
                "%1%",
                ban.ExpiresAtUtc?.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
                    ?? string.Empty,
                StringComparison.Ordinal
            );
    }
}
