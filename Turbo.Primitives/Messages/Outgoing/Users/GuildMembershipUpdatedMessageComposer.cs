using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record GuildMembershipUpdatedMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
