using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Walk through a one-way gate.</summary>
[GenerateSerializer, Immutable]
public sealed record EnterOneWayDoorInteraction : FurnitureInteraction;
