using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Marketplace;

[GenerateSerializer, Immutable]
public sealed record MarketplaceBuyOfferResultEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
