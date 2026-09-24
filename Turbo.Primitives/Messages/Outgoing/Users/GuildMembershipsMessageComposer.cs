using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// The groups the player belongs to. The client keeps this list to fill its own group pickers —
/// the catalog's guild furni page and the wired editor's group boxes both read it — so it is
/// sent on request rather than pushed.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildMembershipsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<GuildInfoSnapshot> Guilds { get; init; }
}
