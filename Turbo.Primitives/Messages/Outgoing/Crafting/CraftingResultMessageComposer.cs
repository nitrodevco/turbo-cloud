using Orleans;
using Turbo.Primitives.Crafting;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Crafting;

/// <summary>Whether a craft worked, and what it made (Flash CraftingResultMessageParser.as:11).</summary>
[GenerateSerializer, Immutable]
public sealed record CraftingResultMessageComposer : IComposer
{
    [Id(0)]
    public required bool Success { get; init; }

    [Id(1)]
    public CraftableProductSnapshot? Product { get; init; }
}
