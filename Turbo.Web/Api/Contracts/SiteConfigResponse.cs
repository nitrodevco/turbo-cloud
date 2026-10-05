namespace Turbo.Web.Api.Contracts;

/// <summary>What the site shows before anyone signs in: the hotel's name, and what works.</summary>
public sealed record SiteConfigResponse(
    string HotelName,
    bool RegistrationOpen,
    bool DiscordReady,
    bool PlayReady
);
