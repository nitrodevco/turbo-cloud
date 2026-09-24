using System;
using Orleans;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// A group as it looks to one viewer: everything the group grain itself knows, which is the
/// group plus where this viewer stands in it. The same group is a different view to a member,
/// an admin and a stranger, so it is built per request rather than cached.
///
/// What the group grain does not know is not here. The homeroom's name, the owner's name and
/// whether the viewer wears this badge belong to the room, the player directory and the
/// player's own guild grain; the handler reads those beside this one and the composer carries
/// them. That split is not tidiness — it is what keeps the group grain from calling the room
/// grain, which from phase 5 calls back into the group grain, and grains are not reentrant.
///
/// The client derives its join, request and leave buttons from <see cref="GuildSummarySnapshot.Type"/>
/// and <see cref="Status"/> together, so those two have to agree with what the server would
/// actually allow.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildViewSnapshot
{
    [Id(0)]
    public required GuildSummarySnapshot Guild { get; init; }

    [Id(1)]
    public required string Description { get; init; }

    [Id(2)]
    public required DateTime CreatedAt { get; init; }

    /// <summary>Members, not counting pending requests or blocked players.</summary>
    [Id(3)]
    public required int MemberCount { get; init; }

    /// <summary>
    /// Requests waiting on an admin, and zero for a viewer who could not act on them: the client
    /// turns any figure above zero into a link they would not be allowed to follow.
    /// </summary>
    [Id(4)]
    public required int PendingMemberCount { get; init; }

    [Id(5)]
    public required GuildMembershipStatus Status { get; init; }

    [Id(6)]
    public required bool IsOwner { get; init; }

    [Id(7)]
    public required bool IsAdmin { get; init; }

    /// <summary>
    /// Whether members may decorate the homeroom: <see cref="GuildRightsLevel"/> reduced to the
    /// one boolean the client draws an icon from.
    /// </summary>
    [Id(8)]
    public required bool MembersCanDecorate { get; init; }
}
