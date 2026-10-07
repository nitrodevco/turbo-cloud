using Orleans;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Save a wired contract's editor.</summary>
[GenerateSerializer, Immutable]
public sealed record UpdateContractInteraction : FurnitureInteraction
{
    [Id(0)]
    public required WiredContractSnapshot Contract { get; init; }
}
