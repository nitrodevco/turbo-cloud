namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// What joining a group takes, as the client's <c>HabboGroupDetailsData</c> numbers it. Its
/// editor offers only the first three; <see cref="Large"/> and <see cref="Open"/> exist on the
/// wire for groups the hotel makes rather than a player.
///
/// The client derives its join, request and leave buttons from this together with the viewer's
/// <see cref="GuildMembershipStatus"/>, so a server that accepts a join the client would not
/// offer, or refuses one it would, is the mismatch the player sees.
/// </summary>
public enum GuildType
{
    /// <summary>Anyone joins at once.</summary>
    Regular = 0,

    /// <summary>Joining asks; an admin approves or rejects.</summary>
    Exclusive = 1,

    /// <summary>Closed. Nobody is taken, by request or otherwise.</summary>
    Private = 2,

    /// <summary>Not offered in the editor.</summary>
    Large = 3,

    /// <summary>Not offered in the editor; joins like <see cref="Regular"/>.</summary>
    Open = 4,
}
