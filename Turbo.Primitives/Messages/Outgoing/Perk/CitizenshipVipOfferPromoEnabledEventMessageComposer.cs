using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Perk;

[GenerateSerializer, Immutable]
public sealed record CitizenshipVipOfferPromoEnabledEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
