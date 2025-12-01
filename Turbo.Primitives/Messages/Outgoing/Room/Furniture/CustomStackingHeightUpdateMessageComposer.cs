using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record CustomStackingHeightUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required int FurniId { get; init; }

    [Id(1)]
    public required int Height { get; init; }
}
