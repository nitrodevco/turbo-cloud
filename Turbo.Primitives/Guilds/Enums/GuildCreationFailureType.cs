namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// Why a group was not made. It is not <see cref="GuildEditFailedType"/> because not every
/// refusal the server can reach has a client text behind it: the four that do map onto that
/// enum, and <see cref="InsufficientCredits"/> does not.
///
/// The hotel publishes no text and no notification variable for failing to afford a group, so
/// there is nothing truthful to send the client for it. Saying one of the other four instead
/// would put a sentence on screen that is not the reason.
/// </summary>
public enum GuildCreationFailureType
{
    InvalidName,
    TooManyGroups,
    RoomAlreadyHomeroom,
    ClubRequired,

    /// <summary>The creator could not pay. Nothing is sent; see the summary above.</summary>
    InsufficientCredits,
}
