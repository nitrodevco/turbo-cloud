using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Room.Pets;

[GenerateSerializer, Immutable]
public sealed record PetInfoMessageComposer : IComposer
{
    [Id(0)]
    public required PetInfoSnapshot Info { get; init; }
}
