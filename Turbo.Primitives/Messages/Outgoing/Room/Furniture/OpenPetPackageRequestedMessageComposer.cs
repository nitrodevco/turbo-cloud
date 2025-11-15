using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record OpenPetPackageRequestedMessageComposer : IComposer
{
    [Id(0)]
    public required int ObjectId { get; init; }
}
