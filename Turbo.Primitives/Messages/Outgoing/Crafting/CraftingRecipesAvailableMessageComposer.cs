using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Crafting;

[GenerateSerializer, Immutable]
public sealed record CraftingRecipesAvailableMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
