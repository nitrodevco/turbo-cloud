using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Spin the wheel of fortune.</summary>
[GenerateSerializer, Immutable]
public sealed record SpinWheelInteraction : FurnitureInteraction;
