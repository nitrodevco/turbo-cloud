using Orleans;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>A request was turned down; the member window drops the row.</summary>
[GenerateSerializer, Immutable]
public sealed record GuildMembershipRejectedMessageComposer : IComposer
{
    [Id(0)]
    public required GuildId GuildId { get; init; }

    [Id(1)]
    public required PlayerId PlayerId { get; init; }
}
