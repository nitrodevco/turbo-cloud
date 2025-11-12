using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record CustomUserNotificationMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
