using Orleans;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>What the create-a-group wizard opens on.</summary>
[GenerateSerializer, Immutable]
public sealed record GuildCreationInfoMessageComposer : IComposer
{
    [Id(0)]
    public required GuildCreationInfoSnapshot CreationInfo { get; init; }
}
