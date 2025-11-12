using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record RoomInfoUpdatedMessageComposer : IComposer
{
    [Id(0)]
    public required int RoomId { get; init; }
}
