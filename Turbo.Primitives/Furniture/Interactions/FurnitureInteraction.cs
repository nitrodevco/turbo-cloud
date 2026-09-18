using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>
/// A furniture action the client sends as its own packet rather than a plain use. Each subtype
/// carries that packet's payload; the item's logic decides whether it responds to it and whether
/// the acting player may. Routed through <c>IRoomGrain.InteractWithItemAsync</c>.
/// </summary>
[GenerateSerializer, Immutable]
public abstract record FurnitureInteraction;
