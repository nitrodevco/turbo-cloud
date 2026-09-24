using Orleans;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>Somebody asked to join; the group's managers are told so their window can show it.</summary>
[GenerateSerializer, Immutable]
public sealed record GroupMembershipRequestedMessageComposer : IComposer
{
    [Id(0)]
    public required GuildId GuildId { get; init; }

    [Id(1)]
    public required GuildMemberSnapshot Member { get; init; }
}
