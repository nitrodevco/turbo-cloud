using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Friendfurni;

[GenerateSerializer, Immutable]
public sealed record FriendFurniOtherLockConfirmedMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
