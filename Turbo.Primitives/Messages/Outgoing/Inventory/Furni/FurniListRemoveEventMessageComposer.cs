using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

[GenerateSerializer, Immutable]
public sealed record FurniListRemoveEventMessageComposer : IComposer
{
    [Id(0)]
    public required int ItemId { get; init; }
}
