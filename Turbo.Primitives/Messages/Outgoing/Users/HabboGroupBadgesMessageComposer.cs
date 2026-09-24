using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// Group id to badge code, for every group the client is about to have to draw. It asks for
/// this on every room ready and resolves every group badge it shows through the answer, so a
/// group left out of it draws nothing at all.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboGroupBadgesMessageComposer : IComposer
{
    /// <summary>Only the id and the badge code are written; the rest of the summary is ignored.</summary>
    [Id(0)]
    public required ImmutableArray<GuildSummarySnapshot> Guilds { get; init; }
}
