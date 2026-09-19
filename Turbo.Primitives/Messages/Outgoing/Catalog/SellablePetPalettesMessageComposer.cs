using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record SellablePetPalettesMessageComposer : IComposer
{
    [Id(0)]
    public required string ProductCode { get; init; }

    [Id(1)]
    public required ImmutableArray<PetBreedSnapshot> Palettes { get; init; }
}
