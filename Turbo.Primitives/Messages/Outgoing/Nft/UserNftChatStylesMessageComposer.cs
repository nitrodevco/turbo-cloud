using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Nft;

[GenerateSerializer, Immutable]
public sealed record UserNftChatStylesMessageComposer : IComposer
{
    [Id(0)]
    public required List<int> ChatStyleIds { get; init; }
}
