using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Dress the mannequin in what the acting player wears.</summary>
[GenerateSerializer, Immutable]
public sealed record SetMannequinFigureInteraction : FurnitureInteraction;
