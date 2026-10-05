namespace Turbo.Web.Configuration;

/// <summary>
/// The Discord application the public site signs people in with
/// (<c>https://discord.com/developers/applications</c>): its OAuth2 client id and secret. Its
/// redirect must be the site's <c>/api/auth/discord/callback</c>.
/// </summary>
public sealed class DiscordConfig
{
    public string ClientId { get; init; } = "";

    public string ClientSecret { get; init; } = "";

    /// <summary>Discord's API, overridable for tests.</summary>
    public string ApiUrl { get; init; } = "https://discord.com/api";

    /// <summary>Where Discord asks the person to allow the sign-in.</summary>
    public string AuthorizeUrl { get; init; } = "https://discord.com/oauth2/authorize";

    public bool IsConfigured => ClientId.Length > 0 && ClientSecret.Length > 0;
}
