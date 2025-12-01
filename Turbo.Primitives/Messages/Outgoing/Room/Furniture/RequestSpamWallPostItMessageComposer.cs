using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record RequestSpamWallPostItMessageComposer : IComposer
{
    [Id(0)]
    public required int ItemId { get; init; }

    [Id(1)]
    public required string Location { get; init; }
}
