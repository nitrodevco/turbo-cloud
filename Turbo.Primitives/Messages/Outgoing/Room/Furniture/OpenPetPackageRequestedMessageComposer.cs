using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record OpenPetPackageRequestedMessageComposer : IComposer
{
    [Id(0)]
    public required int ObjectId { get; init; }
}
