namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// Why a join was refused. The client looks the value up as <c>group.joinfail.&lt;n&gt;</c>, so
/// the members here are the reasons the hotel has text for and nothing else should be sent — an
/// unlisted number shows the player the key instead of a sentence.
///
/// <see cref="ClubRequired"/> is the exception the client special-cases: rather than an alert it
/// opens the Habbo Club window, which is why the hotel publishes no text for it.
/// </summary>
public enum GuildJoinFailedType
{
    /// <summary>The group is at its member limit.</summary>
    GroupFull = 0,

    /// <summary>The joining player is at their own membership limit.</summary>
    TooManyGroups = 1,

    /// <summary>The group is <see cref="GuildType.Private"/>.</summary>
    GroupClosed = 2,

    /// <summary>The group takes no requests at the moment.</summary>
    RequestsNotAccepted = 3,

    /// <summary>Opens the club window rather than an alert.</summary>
    ClubRequired = 4,

    /// <summary>Approving somebody who is at the non-member limit and has no club.</summary>
    TargetNotClubMember = 5,

    /// <summary>Approving somebody who is at the club limit.</summary>
    TargetAtMaxMemberships = 6,
}
