using Orleans;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// What came of trying to join. A join that succeeds lands on either
/// <see cref="GuildMembershipStatus.Member"/> or <see cref="GuildMembershipStatus.Pending"/>,
/// depending on the group's type, and the client draws a different thing for each.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildJoinResultSnapshot
{
    [Id(0)]
    public required GuildMembershipStatus Status { get; init; }

    [Id(1)]
    public GuildJoinFailedType? Failure { get; init; }

    public bool Succeeded => Failure is null;

    public static GuildJoinResultSnapshot Joined() =>
        new() { Status = GuildMembershipStatus.Member };

    public static GuildJoinResultSnapshot Requested() =>
        new() { Status = GuildMembershipStatus.Pending };

    public static GuildJoinResultSnapshot Failed(GuildJoinFailedType failure) =>
        new() { Status = GuildMembershipStatus.NotMember, Failure = failure };
}
