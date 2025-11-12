using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Nft;

[GenerateSerializer, Immutable]
public sealed record UserNftChatStylesMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
