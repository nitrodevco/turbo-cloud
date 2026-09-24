using System;
using Orleans;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// One row of a group's roster, as the member list, a membership request and a rank change all
/// send it. The figure and the name are the player's, not the membership's, so they are
/// resolved when the snapshot is built rather than stored on the row.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildMemberSnapshot
{
    [Id(0)]
    public required GuildMemberRank Rank { get; init; }

    [Id(1)]
    public required PlayerId PlayerId { get; init; }

    [Id(2)]
    public required string PlayerName { get; init; }

    [Id(3)]
    public required string Figure { get; init; }

    /// <summary>When they joined, or asked to. The client shows it as given and does no parsing.</summary>
    [Id(4)]
    public required DateTime MemberSince { get; init; }
}
