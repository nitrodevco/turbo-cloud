using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players.Accounts;

/// <summary>A player to create: their name, motto, gender, and figure (null for the gender's default).</summary>
public sealed record NewPlayer(string Name, string? Motto, AvatarGenderType Gender, string? Figure)
{
    /// <summary>Where the account is asked for from; the registration policies see it.</summary>
    public RegistrationSource Source { get; init; } = RegistrationSource.Hotel;

    /// <summary>
    /// The address it is asked for from (the site's sign-up); empty when not known or staff make
    /// it. Personal data: for the registration policies and events, not for logs.
    /// </summary>
    public string RemoteAddress { get; init; } = string.Empty;
}

/// <summary>Where a new account is asked for from.</summary>
public enum RegistrationSource
{
    /// <summary>The hotel's own code or a plugin's.</summary>
    Hotel,

    /// <summary>A visitor signing up on the public site.</summary>
    Site,

    /// <summary>Staff, in the admin panel.</summary>
    Panel,
}
