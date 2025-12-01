using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

[GenerateSerializer, Immutable]
public sealed record FurniListAddOrUpdateEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
