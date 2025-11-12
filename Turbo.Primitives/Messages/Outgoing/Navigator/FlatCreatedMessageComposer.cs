using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record FlatCreatedMessageComposer : IComposer
{
    [Id(0)]
    public required int RoomId { get; init; }

    [Id(1)]
    public required string Name { get; init; }
}
