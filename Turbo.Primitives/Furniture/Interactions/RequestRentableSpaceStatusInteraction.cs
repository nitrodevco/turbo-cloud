using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Ask a rentable space for its price and renter.</summary>
[GenerateSerializer, Immutable]
public sealed record RequestRentableSpaceStatusInteraction : FurnitureInteraction;
