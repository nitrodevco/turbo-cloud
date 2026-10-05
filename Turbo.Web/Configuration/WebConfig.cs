namespace Turbo.Web.Configuration;

/// <summary>
/// The public site's API (<c>Turbo:Web</c>): where it listens, the site it answers, Discord
/// sign-in, new accounts, and the client it opens. Off unless enabled, and on its own address,
/// not the admin API's, so the public domain reaches nothing of the panel.
/// </summary>
public sealed class WebConfig
{
    public const string SECTION_NAME = "Turbo:Web";

    public bool Enabled { get; init; }

    /// <summary>Where the API listens. Loopback by default, for the site's reverse proxy.</summary>
    public string Url { get; init; } = "http://127.0.0.1:8092";

    /// <summary>
    /// The public site's address. Discord sends people back to its <c>/api/auth/discord/callback</c>,
    /// and sign-ins end on its pages. Cookies are marked secure when it is https.
    /// </summary>
    public string SiteUrl { get; init; } = "http://localhost:5175";

    /// <summary>The hotel's name, shown on the site.</summary>
    public string HotelName { get; init; } = "Turbo";

    /// <summary>
    /// The client's address with <c>{ticket}</c> where the login ticket goes, such as
    /// <c>https://play.example.com/?sso={ticket}</c>. The site loads it in a frame, so the
    /// client's host must allow being framed by the site.
    /// </summary>
    public string ClientUrl { get; init; } = "";

    /// <summary>
    /// A picture of a player's look, with <c>{figure}</c> for their figure string, such as a
    /// habbo-imaging address. Empty shows their initials.
    /// </summary>
    public string AvatarImageUrl { get; init; } = "";

    /// <summary>Whether a Discord account with no player yet may make one. Sign-ins work either way.</summary>
    public bool RegistrationOpen { get; init; } = true;

    /// <summary>How long a sign-in to the site lasts.</summary>
    public int SessionDays { get; init; } = 30;

    /// <summary>How long the ticket Play hands the client works; it is used up by its login anyway.</summary>
    public int PlayTicketMinutes { get; init; } = 5;

    /// <summary>How long someone has to pick their name after signing in with Discord the first time.</summary>
    public int SignUpMinutes { get; init; } = 15;

    /// <summary>Sign-in and sign-up requests one address may make a minute.</summary>
    public int SignInAttemptsPerMinute { get; init; } = 20;

    public DiscordConfig Discord { get; init; } = new();
}
