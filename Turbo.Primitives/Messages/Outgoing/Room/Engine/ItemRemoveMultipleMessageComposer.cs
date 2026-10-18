using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

/// <summary>
/// Several wall items leaving the room at once, the wall twin of
/// <see cref="ObjectRemoveMultipleMessageComposer"/>; the client removes each as an ItemRemove.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ItemRemoveMultipleMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<long> ObjectIdsToRemove { get; init; }

    [Id(1)]
    public required PlayerId PickerId { get; init; }

    /// <summary>
    /// Taken away because an area hider hides it (Flash's "invisible furni manager"), not picked
    /// up or gone: the client keeps what it buffers for the furni until it is sent again.
    /// </summary>
    [Id(2)]
    public required bool ByInvisibleFurniManager { get; init; }
}
