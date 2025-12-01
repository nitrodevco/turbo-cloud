using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Nft;

[GenerateSerializer, Immutable]
public sealed record UserNftWardrobeSelectionMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
