using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record ConvertedRoomIdMessageComposer : IComposer
{
    [Id(0)]
    public required string GlobalId { get; init; }

    [Id(1)]
    public required int ConvertedId { get; init; }
}
