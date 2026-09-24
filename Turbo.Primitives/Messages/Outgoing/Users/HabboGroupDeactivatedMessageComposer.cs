using Orleans;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>The group is gone. The client closes anything it has open for it.</summary>
[GenerateSerializer, Immutable]
public sealed record HabboGroupDeactivatedMessageComposer : IComposer
{
    [Id(0)]
    public required GuildId GuildId { get; init; }
}
