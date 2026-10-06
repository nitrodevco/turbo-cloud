namespace Turbo.Players.Configuration;

/// <summary>
/// Who the hotel's owner is (<c>Turbo:Owner</c>). An owner is in the <c>admin</c> group and holds
/// <c>permissions.superuser</c>, which lets them make other admins. Nothing here is a secret, but
/// a name can be taken by whoever signs up first: on a public hotel prefer
/// <see cref="DiscordId"/>, or close registration until the owner has signed up.
/// </summary>
public sealed class OwnerConfig
{
    public const string SECTION_NAME = "Turbo:Owner";

    /// <summary>The owner's player name, whatever its case. Empty names no one.</summary>
    public string Name { get; init; } = "";

    /// <summary>The owner's Discord user id, for a hotel that signs people in with Discord. Empty names no one.</summary>
    public string DiscordId { get; init; } = "";

    /// <summary>
    /// With neither of the above set, whether the first player created on a hotel with no one else
    /// becomes the owner. Only ever in the Development environment: a hotel that is live never
    /// hands ownership to whoever turns up first.
    /// </summary>
    public bool FirstPlayerInDevelopment { get; init; } = true;

    public bool NamesAnOwner => Name.Trim().Length > 0 || DiscordId.Trim().Length > 0;
}
