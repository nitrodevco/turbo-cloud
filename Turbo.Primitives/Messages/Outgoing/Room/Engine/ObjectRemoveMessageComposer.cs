using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectRemoveMessageComposer : IComposer
{
    [Id(0)]
    public required long ObjectId { get; init; }

    [Id(1)]
    public required bool IsExpired { get; init; }

    [Id(2)]
    public required long PickerId { get; init; }

    [Id(3)]
    public required int Delay { get; init; }
}
