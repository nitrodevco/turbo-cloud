using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

[GenerateSerializer, Immutable]
public sealed record RoomChatlogEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
