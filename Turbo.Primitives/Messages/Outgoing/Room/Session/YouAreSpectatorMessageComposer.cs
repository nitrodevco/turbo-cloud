using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

[GenerateSerializer, Immutable]
public sealed record YouAreSpectatorMessageComposer : IComposer
{
    [Id(0)]
    public required int RoomId { get; init; }
}
