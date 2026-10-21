using Turbo.Primitives.Settings;

namespace Turbo.Operations.Configuration;

/// <summary>
/// The notice players are shown while one kind of countdown runs (a restart, a shutdown or a
/// maintenance): the client's countdown notice under the purse (Habbo's
/// <c>new_feature_notification_countdown</c>), counting down to the end, sent at once to clients
/// that speak Turbo's <c>notices.live</c>. Habbo's own closing alerts are sent as always, beside
/// it. Its text is the hotel text <c>hotel.countdown_notice.&lt;kind&gt;</c>, with <c>{minutes}</c>,
/// <c>{time}</c> (the end, <c>HH:mm</c> in UTC) and <c>{reason}</c> filled in as it is posted.
/// </summary>
[LiveSetting]
public sealed class CountdownNoticeConfig
{
    /// <summary>Whether players are shown the notice for this kind of countdown.</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// How many minutes before the end the notice is put up; it is put up again at each smaller
    /// one, for a player who closed it. Empty: put up as the countdown starts.
    /// </summary>
    public int[] PostAtMinutes { get; init; } = [];

    /// <summary>
    /// Where a click on the notice goes when the countdown names nowhere of its own: a web address
    /// (opened in the browser) or a client link (<c>habbopages/...</c>). Empty: nowhere.
    /// </summary>
    public string DefaultLink { get; init; } = "";

    /// <summary>Whether a player may close the notice.</summary>
    public bool Dismissible { get; init; } = true;

    /// <summary>The notice's icon: one of the client's images by name, or a picture's address. Empty: none.</summary>
    public string Image { get; init; } = "";

    /// <summary>The notice's colour, as <c>#rrggbb</c>. Empty: the client's own.</summary>
    public string Color { get; init; } = "";
}
