using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Advertisement;

[GenerateSerializer, Immutable]
public sealed record RoomAdErrorEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
