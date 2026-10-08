using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Ask the room's jukebox which disks it holds, in playing order.</summary>
[GenerateSerializer, Immutable]
public sealed record RequestJukeboxPlaylistInteraction : FurnitureInteraction;
