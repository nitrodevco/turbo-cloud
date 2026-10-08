using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>
/// Ask the room's sound machine for its songs and how long it has been playing them; the client
/// plays the list itself from that point.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RequestSoundMachinePlaylistInteraction : FurnitureInteraction;
