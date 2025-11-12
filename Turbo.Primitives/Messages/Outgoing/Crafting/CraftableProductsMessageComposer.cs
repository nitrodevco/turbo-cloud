using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Crafting;

[GenerateSerializer, Immutable]
public sealed record CraftableProductsMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
