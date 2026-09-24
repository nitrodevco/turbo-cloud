using Orleans;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// One member's standing moved: joined, approved, promoted, demoted or removed. The client
/// redraws that one row rather than asking for the page again.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildMembershipUpdatedMessageComposer : IComposer
{
    [Id(0)]
    public required GuildId GuildId { get; init; }

    [Id(1)]
    public required GuildMemberSnapshot Member { get; init; }
}
