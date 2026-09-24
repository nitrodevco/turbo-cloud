using Orleans;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>One page of a group's roster, answering the member window's search.</summary>
[GenerateSerializer, Immutable]
public sealed record GuildMembersMessageComposer : IComposer
{
    [Id(0)]
    public required GuildMemberPageSnapshot Page { get; init; }
}
